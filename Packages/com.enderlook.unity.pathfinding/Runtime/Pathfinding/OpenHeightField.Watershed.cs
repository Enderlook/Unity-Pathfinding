using Enderlook.Collections.LowLevel;
using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Mathematics;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    internal partial struct OpenHeightField
    {
        public void BuildRegions()
        {
            int lengthXZ = resolution.x * resolution.z;
            //int[] spanIndexStartAt = ArrayPool<int>.Shared.Rent(lengthXZ);
            int spanCount = columns[0].AsSpan().Length;
            //spanIndexStartAt[0] = 0;
            for (int i = 1; i < (lengthXZ - 1); i++)
            {
                //spanIndexStartAt[i] = spanCount;
                spanCount += columns[i].AsSpan().Length;
            }
            //spanIndexStartAt[lengthXZ - 1] = spanCount;

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

        private void ExpandRegions(
            int waterLevel,
            int[] sourceRegion,
            int[] sourceDistance,
            int[] destinationRegion,
            int[] destinationDistance,
            RawPooledList<(int x, int z, int i)> stack
        )
        {
            // Find cells revealed by the raised level
            int columnIndex = 0;
            int spanIndex = 0;
            for (int z = 0; z < resolution.z; z++)
            {
                for (int x = 0; x < resolution.x; x++)
                {
                    ref HeightColumn column = ref columns[columnIndex++];
                    Span<HeightSpan> spans = column.AsSpan();
                    for (int i = 0; i < spans.Length; i++)
                    {
                        ref HeightSpan span = ref spans[i];
                        if (span.Distance >= waterLevel && sourceRegion[spanIndex++] == 0 && span.Area != 0)
                            stack.Add((x, z, i));
                    }
                }
            }

            int iter = 0;
            while (stack.Count > 0)
            {
                int failed = 0;

                for (int j = 0; j < stack.Count; j++)
                {
                    (int x, int z, int i) = stack[j];

                    if (i < 0)
                    {
                        failed++;
                        continue;
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
        }
    }
}