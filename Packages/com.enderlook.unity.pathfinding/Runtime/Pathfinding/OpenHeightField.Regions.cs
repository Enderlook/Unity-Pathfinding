using Enderlook.Collections.Pooled.LowLevel;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    internal partial struct OpenHeightField
    {
        public void CalculateRegions(int agentSize)
        {
            RawPooledList<Region> regions = RawPooledList<Region>.Create();
            try
            {
                (int xz, int y)[] tmp = ArrayPool<(int xz, int y)>.Shared.Rent(0);
                try
                {
                    int lenghtXZ = resolution.x * resolution.z;

                    for (int waterLevel = maximumDistance; waterLevel >= agentSize; waterLevel--)
                    {
                        // Grow all regions equally.
                        GrowRegions(ref regions, waterLevel, ref tmp);

                        // Find new basins.
                        for (int xz = 0; xz < lenghtXZ; xz++)
                        {
                            HeightColumn column = columns[xz];
                            Span<HeightSpan> spans = column.AsSpan();
                            for (int y = 0; y < spans.Length; y++)
                            {
                                ref HeightSpan span = ref spans[y];
                                if (span.Distance == waterLevel && span.Region == HeightSpan.NULL_REGION)
                                {
                                    regions.Add(new Region((ushort)(regions.Count + 1)));
                                    regions[regions.Count - 1].AddSpan(xz, y);
                                    span.Region = (ushort)regions.Count;
                                    FloodRegion(waterLevel, xz, y, ref regions[regions.Count - 1], ref tmp);
                                }
                            }
                        }
                    }

                    // TODO: Handle small regions by deleting them or merging them.
                }
                finally
                {
                    ArrayPool<(int xz, int y)>.Shared.Return(tmp);
                }
            }
            finally
            {
                for (int i = 0; i < regions.Count; i++)
                    regions[i].Dispose();
                regions.Dispose();
            }
        }

        private void FloodRegion(int waterLevel, int columnIndex, int spanIndex, ref Region region, ref (int xz, int y)[] tmp)
        {
            RawPooledStack<(int xz, int y)> stack = RawPooledStack<(int xz, int y)>.FromEmpty(tmp);

            stack.Push((columnIndex, spanIndex));

            while (stack.TryPop(out (int xz, int y) value))
            {
                ref HeightSpan span = ref columns[value.xz].AsSpan()[value.y];
                FloodRegionCheckNeighbour(waterLevel, ref region, ref stack, value.xz, span.Left, -resolution.z);
                FloodRegionCheckNeighbour(waterLevel, ref region, ref stack, value.xz, span.Right, resolution.z);
                FloodRegionCheckNeighbour(waterLevel, ref region, ref stack, value.xz, span.Backward, -1);
                FloodRegionCheckNeighbour(waterLevel, ref region, ref stack, value.xz, span.Forward, 1);
            }

            tmp = stack.UnderlyingArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FloodRegionCheckNeighbour(int waterLevel, ref Region region, ref RawPooledStack<(int xz, int y)> stack, int xz, int y, int d)
        {
            if (y == HeightSpan.NULL_SIDE)
                return;

            xz += d;
            ref HeightSpan neighbour = ref columns[xz].AsSpan()[y];
            if (neighbour.Distance == waterLevel && neighbour.Region == HeightSpan.NULL_REGION)
            {
                neighbour.Region = region.id;
                region.AddSpan(xz, y);
                stack.Push((xz, y));
            }
        }

        private void GrowRegions(ref RawPooledList<Region> regions, int waterLevel, ref (int xz, int y)[] tmp)
        {
            RawPooledList<(int xz, int y)> tmp_ = RawPooledList<(int xz, int y)>.FromEmpty(tmp);
            bool change = true;
            while (change)
            {
                change = false;
                for (int i = 0; i < regions.Count; i++)
                {
                    ref Region region = ref regions[i];
                    change = GrowRegion(waterLevel, ref tmp_, ref region);
                }
            }
            tmp = tmp_.UnderlyingArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool GrowRegion(int waterLevel, ref RawPooledList<(int xz, int y)> tmp, ref Region region)
        {
            tmp.Clear();
            tmp = region.Swap(tmp);
            bool change = false;
            for (int j = 0; j < tmp.Count; j++)
            {
                (int xz, int y) location = tmp[j];
                ref HeightSpan span = ref columns[location.xz].AsSpan()[location.y];
                bool c = false;
                bool a = GrowRegionCheckNeighbour(waterLevel, location.xz, ref region, span.Left, -resolution.z);
                change |= a;
                c &= a;
                a = GrowRegionCheckNeighbour(waterLevel, location.xz, ref region, span.Right, resolution.z);
                change |= a;
                c &= a;
                a = GrowRegionCheckNeighbour(waterLevel, location.xz, ref region, span.Backward, -1);
                change |= a;
                c &= a;
                a = GrowRegionCheckNeighbour(waterLevel, location.xz, ref region, span.Forward, 1);
                change |= a;
                c &= a;
                if (!c)
                    region.AddSpan(location.xz, location.y);
            }
            return change;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool GrowRegionCheckNeighbour(int waterLevel, int index, ref Region region, int j, int d)
        {
            if (j != HeightSpan.NULL_SIDE)
            {
                int i = index + d;
                ref HeightSpan neighbour = ref columns[i].AsSpan()[j];
                if (neighbour.Distance == waterLevel && neighbour.Region == HeightSpan.NULL_REGION)
                {
                    neighbour.Region = region.id;
                    region.AddSpan(i, j);
                    return true;
                }
            }
            return false;
        }

        private struct Region : IDisposable
        {
            public readonly ushort id;
            public RawPooledList<(int xz, int y)> spans;

            public Region(ushort id)
            {
                this.id = id;
                spans = RawPooledList<(int xz, int y)>.Create();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddSpan(int xz, int y)
                => spans.Add((xz, y));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RawPooledList<(int xz, int y)> Swap(RawPooledList<(int xz, int y)> other)
            {
                RawPooledList<(int xz, int y)> tmp = spans;
                spans = other;
                return tmp;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose() => spans.Dispose();
        }

        public void DrawGizmosOfRegions(Vector3 center, Vector3 cellSize)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(center, new Vector3(cellSize.x * resolution.x, cellSize.y * resolution.y, cellSize.z * resolution.z));
            Vector3 offset = (new Vector3(resolution.x * (-cellSize.x), resolution.y * (-cellSize.y), resolution.z * (-cellSize).z) * .5f) + (cellSize * .5f);
            offset.y -= cellSize.y / 2;
            offset += center;

            int i = 0;
            for (int x = 0; x < resolution.x; x++)
            {
                for (int z = 0; z < resolution.z; z++)
                {
                    Vector2 position_ = new Vector2(x * cellSize.x, z * cellSize.z);
                    ReadOnlySpan<HeightSpan> heightSpans = columns[i++].AsSpan();

                    if (heightSpans.Length > 0)
                    {
                        int j = 0;

                        HeightSpan heightSpan = heightSpans[j++];
                        if (heightSpan.Floor != -1)
                            Draw(heightSpan.Floor - .1f, heightSpan.Region);

                        for (; j < heightSpans.Length - 1; j++)
                        {
                            heightSpan = heightSpans[j];
                            Draw(heightSpan.Floor - .1f, heightSpan.Region);
                        }

                        if (heightSpans.Length > 1) // Shouldn't this be 2?
                        {
                            Debug.Assert(j == heightSpans.Length - 1);
                            heightSpan = heightSpans[j];
                            Draw(heightSpan.Floor, heightSpan.Region);
                        }
                    }

                    void Draw(float y, int region)
                    {
                        // https://gamedev.stackexchange.com/a/46469/99234 from https://gamedev.stackexchange.com/questions/46463/how-can-i-find-an-optimum-set-of-colors-for-10-players
                        const float goldenRatio = 1.61803398874989484820458683436f; // (1 + Math.Sqrt(5)) / 2
                        const float div = 1 / goldenRatio;
                        Gizmos.color = Color.HSVToRGB(region * div % 1f, .5f, Mathf.Sqrt(1 - (region * div % .5f)));

                        Vector3 position = new Vector3(position_.x, cellSize.y * y, position_.y);
                        Vector3 center_ = offset + position;
                        Vector3 size = new Vector3(cellSize.x, cellSize.y * .1f, cellSize.z);
                        Gizmos.DrawCube(center_, size);
                    }
                }
            }
        }
    }
}