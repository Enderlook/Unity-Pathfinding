using Enderlook.Collections.Pooled.LowLevel;

using System;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    internal partial struct OpenHeightField
    {
        /*public void BuildRegions()
        {
            int lengthXZ = resolution.x * resolution.z;
            int[] spanIndexStartAt = ArrayPool<int>.Shared.Rent(lengthXZ);
            int spanCount = columns[0].AsSpan().Length;
            spanIndexStartAt[0] = 0;
            for (int i = 1; i < (lengthXZ - 1); i++)
            {
                spanIndexStartAt[i] = spanCount;
                spanCount += columns[i].AsSpan().Length;
            }
            spanIndexStartAt[lengthXZ - 1] = spanCount;

            int[] sourceRegion = ArrayPool<int>.Shared.Rent(spanCount);
            int[] sourceDistance = ArrayPool<int>.Shared.Rent(spanCount);
            int[] destinationRegion = ArrayPool<int>.Shared.Rent(spanCount);
            int[] destinationDistance = ArrayPool<int>.Shared.Rent(spanCount);
            RawPooledStack<(int x, int z, int i)> stack = RawPooledStack<(int x, int z, int i)>.Create(1024);

            int level = (maximumDistance + 1) & ~1; // Ensures that value is always an even number.
            while (level > 0)
            {
                level = level >= 2 ? level - 2 : 0;

                // Expand current regions until no empty connected cells are found.

            }
        }

        private const ushort NULL_AREA = 0;

        private void ExpandRegions(
            int waterLevel,
            int[] spanIndexStartAt,
            ushort[] sourceRegion,
            int[] sourceDistance,
            int[] destinationRegion,
            int[] destinationDistance,
            byte[] areas,
            ushort[] regions,
            RawPooledList<(int x, int z, int columnIndex, int i, int spanIndex)> stack
        )
        {
            // Find cells revealed by the raised level
            {
                int columnIndex = 0;
                int spanIndex = 0;
                for (int z = 0; z < resolution.z; z++)
                {
                    for (int x = 0; x < resolution.x; x++, columnIndex++)
                    {
                        ref HeightColumn column = ref columns[columnIndex];
                        Span<HeightSpan> spans = column.AsSpan();
                        for (int i = 0; i < spans.Length; i++, spanIndex++)
                        {
                            ref HeightSpan span = ref spans[i];
                            if (span.Distance >= waterLevel && sourceRegion[spanIndex] == 0 && areas[spanIndex] == NULL_AREA)
                                stack.Add((x, z, columnIndex, i, spanIndex));
                        }
                    }
                }
            }

            int iter = 0;
            while (stack.Count > 0)
            {
                int failed = 0;

                for (int j = 0; j < stack.Count; j++)
                {
                    (int x, int z, int i, int columnIndex, int spanIndex) = stack[j];

                    if (i < 0)
                    {
                        failed++;
                        continue;
                    }

                    ushort region = sourceRegion[i];
                    ushort d2 = 0xFFFF;
                    byte area = areas[spanIndex];
                    ref HeightSpan span = ref columns[columnIndex].AsSpan()[spanIndex];

                    {
                        if (span.Left == HeightSpan.NULL_SIDE)
                            continue;
                        int aX = x - 1;
                        int aZ = z;
                        Debug.Assert(columnIndex - resolution.z == GetIndex(x - 1, z));
                        int aColumnIndex = columnIndex - resolution.z;
                        //ref HeightSpan spanNeighbour = ref columns[aColumnIndex].AsSpan()[span.Left];
                        int aSpanIndex = spanIndexStartAt[aColumnIndex] + span.Left;
                        if (areas[aSpanIndex] != area)
                            continue;
                        if (sourceRegion[aSpanIndex] > 0 && (sourceRegion[aSpanIndex] && )
                    }
                }

            }
        }


        public void CalculateRegionsAndContour(int agentSize)
        {
            RawPooledList<Region> regions = RawPooledList<Region>.Create();

            int lenghtXZ = resolution.x * resolution.z;

            for (int waterLevel = maximumDistance; waterLevel >= agentSize; waterLevel--)
            {
                // Grow all regions equally.
                GrowRegions(ref regions, waterLevel);

                // Find new basins.
                for (int i = 0; i < lenghtXZ; i++)
                {
                    HeightColumn column = columns[lenghtXZ];
                    Span<HeightSpan> spans = column.AsSpan();
                    for (int j = 0; j < spans.Length; j++)
                    {
                        ref HeightSpan span = ref spans[j];
                        if (span.Distance == waterLevel && span.Region == 0)
                        {
                            regions.Add(new Region(regions.Count + 1));
                            regions[regions.Count - 1].AddSpan(i, j);
                            span.Region = regions.Count;
                            FloodRegion(ref regions[regions.Count - 1], waterLevel);
                        }
                    }
                }
            }

            HandleSmallRegions(ref regions);
        }

        private struct Region
        {
            private int id;
            private RawPooledList<(int i, int j)> spans;

            public Region(int id)
            {
                this.id = id;
                spans = RawPooledList<(int i, int j)>.Create();
            }

            public void AddSpan(int i, int j)
                => spans.Add((i, j));
        }

        private void GrowRegions(ref RawPooledList<Region> regions, int waterLevel)
        {
            int lenghtXZ = resolution.x * resolution.z;
            for (int i = 0; i < lenghtXZ; i++)
            {
                HeightColumn column = columns[lenghtXZ];
                Span<HeightSpan> spans = column.AsSpan();
                for (int j = 0; j < spans.Length; j++)
                {
                    ref HeightSpan span = ref spans[j];

                }
            }
        }*/

        public void CalculateRegions(int agentSize)
        {
            RawPooledList<Region> regions = RawPooledList<Region>.Create();
            try
            {
                RawPooledList<(int i, int j)> tmp = RawPooledList<(int i, int j)>.Create();
                try
                {
                    RawPooledStack<(int i, int j)> stack = RawPooledStack<(int i, int j)>.Create();
                    try
                    {
                        int lenghtXZ = resolution.x * resolution.z;

                        for (int waterLevel = maximumDistance; waterLevel >= agentSize; waterLevel--)
                        {
                            // Grow all regions equally.
                            GrowRegions(ref regions, waterLevel, ref tmp);

                            // Find new basins.
                            for (int i = 0; i < lenghtXZ; i++)
                            {
                                HeightColumn column = columns[i];
                                Span<HeightSpan> spans = column.AsSpan();
                                for (int j = 0; j < spans.Length; j++)
                                {
                                    ref HeightSpan span = ref spans[j];
                                    if (span.Distance == waterLevel && span.Region == HeightSpan.NULL_REGION)
                                    {
                                        regions.Add(new Region((ushort)(regions.Count + 1)));
                                        regions[regions.Count - 1].AddSpan(i, j);
                                        span.Region = (ushort)regions.Count;
                                        FloodRegion(waterLevel, i, j, ref regions[regions.Count - 1], ref stack);
                                    }
                                }
                            }
                        }

                        //HandleSmallRegions(ref regions);
                    }
                    finally
                    {
                        stack.Dispose();
                    }
                }
                finally
                {
                    tmp.Dispose();
                }
            }
            finally
            {
                for (int i = 0; i < regions.Count; i++)
                    regions[i].Dispose();
                regions.Dispose();
            }
        }

        private void FloodRegion(int waterLevel, int index, int spanIndex, ref Region region, ref RawPooledStack<(int i, int j)> stack)
        {
            stack.Push((index, spanIndex));

            while (stack.TryPop(out (int index, int spanIndex) value))
            {
                ref HeightSpan span = ref columns[value.index].AsSpan()[value.spanIndex];
                FloodRegionCheckNeighbour(waterLevel, index, ref region, ref stack, span.Left, -resolution.z);
                FloodRegionCheckNeighbour(waterLevel, index, ref region, ref stack, span.Right, resolution.z);
                FloodRegionCheckNeighbour(waterLevel, index, ref region, ref stack, span.Backward, -1);
                FloodRegionCheckNeighbour(waterLevel, index, ref region, ref stack, span.Foward, 1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FloodRegionCheckNeighbour(int waterLevel, int index, ref Region region, ref RawPooledStack<(int i, int j)> stack, int j, int d)
        {
            if (j != HeightSpan.NULL_SIDE)
            {
                int i = index + d;
                ref HeightSpan neighbour = ref columns[i].AsSpan()[j];
                if (neighbour.Distance == waterLevel && neighbour.Region == HeightSpan.NULL_REGION)
                {
                    neighbour.Region = region.id;
                    region.AddSpan(i, j);
                    stack.Push((i, j));
                }
            }
        }

        private void GrowRegions(ref RawPooledList<Region> regions, int waterLevel, ref RawPooledList<(int i, int j)> tmp)
        {
            bool change = true;
            while (change)
            {
                change = false;
                for (int i = 0; i < regions.Count; i++)
                {
                    ref Region region = ref regions[i];
                    change = GrowRegion(waterLevel, ref tmp, ref region);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool GrowRegion(int waterLevel, ref RawPooledList<(int i, int j)> tmp, ref Region region)
        {
            tmp.Clear();
            tmp = region.Swap(tmp);
            bool change = false;
            for (int j = 0; j < tmp.Count; j++)
            {
                (int i, int j) location = tmp[j];
                ref HeightSpan span = ref columns[location.i].AsSpan()[location.j];
                change |= GrowRegionCheckNeighbour(waterLevel, location.i, ref region, span.Left, -resolution.z);
                change |= GrowRegionCheckNeighbour(waterLevel, location.i, ref region, span.Right, resolution.z);
                change |= GrowRegionCheckNeighbour(waterLevel, location.i, ref region, span.Backward, -1);
                change |= GrowRegionCheckNeighbour(waterLevel, location.i, ref region, span.Foward, 1);
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
                if (neighbour.Distance == waterLevel && neighbour.Region != HeightSpan.NULL_REGION && neighbour.Region != region.id)
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
            public RawPooledList<(int i, int j)> spans;

            public Region(ushort id)
            {
                this.id = id;
                spans = RawPooledList<(int i, int j)>.Create();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddSpan(int i, int j)
                => spans.Add((i, j));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RawPooledList<(int i, int j)> Swap(RawPooledList<(int i, int j)> other)
            {
                RawPooledList<(int i, int j)> tmp = spans;
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