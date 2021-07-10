using Enderlook.Collections.Pooled.LowLevel;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    /// <summary>
    /// Stores the regions of a <see cref="DistanceField"/>.
    /// </summary>
    internal readonly struct RegionsField : IDisposable
    {
        private readonly ushort[] regions;
        private readonly int regionsCount;

        public ReadOnlySpan<ushort> Regions => regions.AsSpan(0, regionsCount);

        /* Do not change this value!
         * We are taking advantage that this value is 0 to perform some operations.*/
        public const ushort NULL_REGION = 0;

        /// <summary>
        /// Calculates the regions of the specified distance field.
        /// </summary>
        /// <param name="distanceField">Distance field whose regions is being calculated.</param>
        /// <param name="openHeightField">Open height field owner of the <paramref name="distanceField"/>.</param>
        /// <param name="options">Stores configuration information.</param>
        /// <returns>The generated regions field.</return>
        public RegionsField(in DistanceField distanceField, in CompactOpenHeightField openHeightField, MeshGenerationOptions options)
        {
            Resolution resolution = options.Resolution;
            openHeightField.DebugAssert(nameof(openHeightField), resolution, $"{nameof(options)}.{nameof(resolution)}");
            distanceField.DebugAssert(nameof(distanceField), resolution,  $"{nameof(options)}.{nameof(resolution)}");

            ReadOnlySpan<ushort> distances = distanceField.Distances;
            regionsCount = distances.Length;
            ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans = openHeightField.Spans;
            regions = ArrayPool<ushort>.Shared.Rent(regionsCount);
            try
            {
                if (regionsCount == 0)
                    return;

                Array.Clear(this.regions, 0, regionsCount);
                Debug.Assert(this.regions[0] == NULL_REGION);

                RawPooledList<Region> regions = RawPooledList<Region>.Create();
                try
                {
                    int[] tmp = ArrayPool<int>.Shared.Rent(0);
                    try
                    {
                        int agentSize = options.AgentSize;
                        for (int waterLevel = distanceField.MaximumDistance; waterLevel >= agentSize; waterLevel--)
                        {
                            ExpandAllRegionsEqually(distances, spans, ref regions, ref tmp, waterLevel);
                            FindNewBasins(distances, spans, ref regions, ref tmp, waterLevel);
                        }

                        // TODO: Handle small regions by deleting them or merging them.

                        Debug.Assert(regions.Count < ushort.MaxValue);
                        NullifySmallRegions(ref regions, (ushort)regions.Count, options.MinimumRegionSurface);
                    }
                    finally
                    {
                        ArrayPool<int>.Shared.Return(tmp);
                    }
                }
                finally
                {
                    for (int i = 0; i < regions.Count; i++)
                        regions[i].Dispose();
                    regions.Dispose();
                }
            }
            catch
            {
                ArrayPool<ushort>.Shared.Return(regions);
                throw;
            }
        }

        /// <summary>
        /// Debug assert that this instance is valid.
        /// </summary>
        /// <param name="parameterName">Name of the instance.</param>
        [System.Diagnostics.Conditional("Debug")]
        public void DebugAssert(string parameterName) => Debug.Assert(!(regions is null), $"{parameterName} is default");

        private void ExpandAllRegionsEqually(ReadOnlySpan<ushort> distances, ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, ref RawPooledList<Region> regions, ref int[] tmp, int waterLevel)
        {
            RawPooledList<int> tmp_ = RawPooledList<int>.FromEmpty(tmp);
            bool change = true;
            while (change)
            {
                change = false;
                for (int i = 0; i < regions.Count; i++)
                {
                    ref Region region = ref regions[i];
                    change = ExpandRegion(distances, spans, waterLevel, ref tmp_, ref region);
                }
            }
            tmp = tmp_.UnderlyingArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ExpandRegion(ReadOnlySpan<ushort> distances, ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, int waterLevel, ref RawPooledList<int> tmp, ref Region region)
        {
            // Flood fill mark region.

            tmp.Clear();
            tmp = region.SwapBorder(tmp);
            bool change = false;
            for (int j = 0; j < tmp.Count; j++)
            {
                int i = tmp[j];
                ref readonly CompactOpenHeightField.HeightSpan span = ref spans[i];

                bool canKeepGrowing = false;

                ExpandRegionCheckNeighbour(distances, waterLevel, ref region, span.Left, ref change, ref canKeepGrowing);
                ExpandRegionCheckNeighbour(distances, waterLevel, ref region, span.Forward, ref change, ref canKeepGrowing);
                ExpandRegionCheckNeighbour(distances, waterLevel, ref region, span.Right, ref change, ref canKeepGrowing);
                ExpandRegionCheckNeighbour(distances, waterLevel, ref region, span.Backward, ref change, ref canKeepGrowing);

                if (canKeepGrowing)
                    region.AddSpanToBorder(i);
            }
            return change;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExpandRegionCheckNeighbour(ReadOnlySpan<ushort> distances, int waterLevel, ref Region region, int neighbour, ref bool didGrow, ref bool canGrow)
        {
            if (neighbour != CompactOpenHeightField.HeightSpan.NULL_SIDE && regions[neighbour] == NULL_REGION)
            {
                if (distances[neighbour] == waterLevel)
                {
                    Debug.Assert(regions[neighbour] != region.id);
                    regions[neighbour] = region.id;
                    region.AddSpanToBorder(neighbour);
                    region.count++;
                    didGrow = true;
                }
                else
                    canGrow = true;
            }
        }

        private void FindNewBasins(ReadOnlySpan<ushort> distances, ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, ref RawPooledList<Region> regions, ref int[] tmp, int waterLevel)
        {
            // TODO: If we stored a sorted copy of distances span, this would not require to loop the whole span. Research if it's worth the optimization.

            ushort[] thisRegions = this.regions;
            for (int i = 0; i < distances.Length; i++)
            {
                ref readonly CompactOpenHeightField.HeightSpan span = ref spans[i];
                if (distances[i] == waterLevel && thisRegions[i] == NULL_REGION)
                {
                    regions.Add(new Region((ushort)(regions.Count + 1)));
                    ref Region region = ref regions[regions.Count - 1];
                    region.AddSpanToBorder(i);
                    Debug.Assert(thisRegions[i] != (ushort)regions.Count);
                    region.count++;
                    thisRegions[i] = (ushort)regions.Count;
                    FloodRegion(distances, spans, waterLevel, i, ref regions[regions.Count - 1], ref tmp);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FloodRegion(ReadOnlySpan<ushort> distances, ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, int waterLevel, int index, ref Region region, ref int[] tmp)
        {
            RawPooledStack<int> stack = RawPooledStack<int>.FromEmpty(tmp);

            stack.Push(index);

            while (stack.TryPop(out int value))
            {
                ref readonly CompactOpenHeightField.HeightSpan span = ref spans[value];

                /*// Check if any of the neighbours already have a valid region set.
                ushort ar = 0;
                for (int direction = 0; direction < 4; direction++)
                {
                    int neighbour = span.GetSide(direction);
                    if (neighbour == CompactOpenHeightField.HeightSpan.NULL_SIDE)
                        continue;
                    ushort nr = regions[neighbour];
                    if (nr == NULL_REGION)
                        continue;
                    if (nr != region.id)
                        ar = nr;

                    ref readonly CompactOpenHeightField.HeightSpan span_ = ref spans[neighbour];
                    int neighbour_ = span_.GetSide(CompactOpenHeightField.HeightSpan.RotateClockwise(neighbour));
                    if (neighbour_ == CompactOpenHeightField.HeightSpan.NULL_SIDE)
                        continue;
                    ushort nr_ = regions[neighbour_];
                    if (nr_ != region.id)
                        ar = nr_;
                }
                if (ar != 0)
                {
                    regions[value] = NULL_REGION;
                    continue;
                }

                for (int direction = 0; direction < 4; direction++)
                {
                    int neighbour = span.GetSide(direction);

                    if (neighbour == CompactOpenHeightField.HeightSpan.NULL_SIDE)
                        continue;
                    if (distances[neighbour] >= waterLevel && regions[neighbour] == NULL_REGION)
                    {
                        regions[neighbour] = region.id;
                        Unsafe.AsRef(distances[neighbour]) = 0;
                        stack.Push(neighbour);
                    }
                }*/

                FloodRegionCheckNeighbour<Side.Left>(spans, distances, waterLevel, ref region, ref stack, span.Left);
                FloodRegionCheckNeighbour<Side.Forward>(spans, distances, waterLevel, ref region, ref stack, span.Forward);
                FloodRegionCheckNeighbour<Side.Right>(spans, distances, waterLevel, ref region, ref stack, span.Right);
                FloodRegionCheckNeighbour<Side.Backward>(spans, distances, waterLevel, ref region, ref stack, span.Backward);
            }

            tmp = stack.UnderlyingArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FloodRegionCheckNeighbour<T>(ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, ReadOnlySpan<ushort> distances, int waterLevel, ref Region region, ref RawPooledStack<int> stack, int neighbour)
        {
            Side.DebugAssert<T>();

            if (neighbour == CompactOpenHeightField.HeightSpan.NULL_SIDE)
                return;

            if (distances[neighbour] == waterLevel && regions[neighbour] == NULL_REGION)
            {
                Debug.Assert(regions[neighbour] != region.id);
                regions[neighbour] = region.id;
                region.count++;
                region.AddSpanToBorder(neighbour);
                stack.Push(neighbour);
            }

            ref readonly CompactOpenHeightField.HeightSpan span = ref spans[neighbour];

            int neighbour_;

            if (typeof(T) == typeof(Side.Left))
                neighbour_ = span.Forward;
            else if (typeof(T) == typeof(Side.Forward))
                neighbour_ = span.Right;
            else if (typeof(T) == typeof(Side.Right))
                neighbour_ = span.Backward;
            else if (typeof(T) == typeof(Side.Backward))
                neighbour_ = span.Left;
            else
            {
                Debug.Assert(false, "Impossible state.");
                return;
            }

            if (neighbour_ == CompactOpenHeightField.HeightSpan.NULL_SIDE)
                return;

            if (distances[neighbour_] == waterLevel && regions[neighbour_] == NULL_REGION)
            {
                Debug.Assert(regions[neighbour_] != region.id);
                regions[neighbour_] = region.id;
                region.count++;
                region.AddSpanToBorder(neighbour_);
                stack.Push(neighbour_);
            }
        }

        private struct Region : IDisposable
        {
            public readonly ushort id;
            public RawPooledList<int> border;
            public int count;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Region(ushort id)
            {
                this.id = id;
                border = RawPooledList<int>.Create();
                count = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddSpanToBorder(int i) => border.Add(i);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RawPooledList<int> SwapBorder(RawPooledList<int> other)
            {
                RawPooledList<int> tmp = border;
                border = other;
                return tmp;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose() => border.Dispose();
        }

        /*private void FixIncompleteNullRegionConnections(in CompactOpenHeightField openHeightField)
        {
            /* If a region touches null region only diagonally,
             * then contour detection algorithms may not properly detect the null region connection.
             * This can adversely effect other algorithms in the pipeline.
             * 
             * Example: Before algorithm is applied:
             *  b b a a a a
             *  b b a a a a
             *  a a x x x x
             *  a a x x x x
             * 
             * Example: After algorithm is applied:
             *  b b a a a a
             *  b b b a a a <-- Span transferred to region B.
             *  a a x x x x
             *  a a x x x x
             * 
             * In order to check this, we can use the following naive algorithm (TODO: this is not very robust and can damage the regions on edge cases...):
             * We iterate over all spans, and skip non-null regions. For example we may stop at:
             *  b b a a a a
             *  b b a a a a
             *  a a(x)x x x
             *  a a x x x x
             * 
             * Now we iterate over its 4 axis neighbour, for example the Left one:
             *  b b a a a a
             *  b b a a a a
             *  a(a)x x x x
             *  a a x x x x
             * 
             * Since Left is in the horizontal axis, we must check the vertical axis of this neighbour (to find diagonal neigbours).
             * For example Forward:
             *  b b a a a a
             *  b(b)a a a a
             *  a a x x x x
             *  a a x x x x
             * 
             * The new neighbour doesn't have the same region as the old one.
             * So it's in danger. We must check the region of the other neighbour from null that can touch this neighbour.
             *  b b a a a a
             *  b b(a)a a a
             *  a a x x x x
             *  a a x x x x
             * 
             * This other neighbour doesn't have the same region as the dangered one. So we replace its region to fix the other.
             *  b b a a a a
             *  b b(b)a a a
             *  a a x x x x
             *  a a x x x x
             * 
             * TODO: However this can fail, imagine the following case:
             *  b b a a c c
             *  b b a a c c
             *  a a x c c x
             *  a a x c c x
             * 
             * This algorithm would produce:
             *  b b a a c c
             *  b b(b)A c c
             *  a a x c c x
             *  a a x c c x
             * 
             * Which would make invalid `A`.
             * Instead we should do:
             *  b b a a c c
             *  b(a)a a c c
             *  a a x c c x
             *  a a x c c x
             * However we cannot just blindly replace `b` to `a`, because in other cases it could also invalidate other `b` neighbours.
             */

        /*ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans = openHeightField.Spans;
        for (int index = 0; index < regionsCount; index++)
        {
            if (regions[index] != NULL_REGION)
                continue;

            ref readonly CompactOpenHeightField.HeightSpan span = ref spans[index];

            int neighbourIndex1 = span.Left;
            if (neighbourIndex1 != CompactOpenHeightField.HeightSpan.NULL_SIDE)
            {
                int neighbourRegion1 = regions[neighbourIndex1];
                ref readonly CompactOpenHeightField.HeightSpan neighbourSpan = ref spans[neighbourIndex1];
                int neighbourIndex2 = neighbourSpan.Forward;
                if (neighbourIndex2 != CompactOpenHeightField.HeightSpan.NULL_SIDE)
                {
                    int neighbourRegion2 = regions[neighbourIndex2];

                }
            }
        }
    }*/

        /// <summary>
        /// Removes all regions which consists on less than <paramref name="minRegionSurface"/> voxels.
        /// </summary>
        /// <param name="highestRegionId">The highest region id number.</param>
        /// <param name="minRegionSurface">Minimal amount of voxels required by a region to survive.</param>
        private void NullifySmallRegions(ref RawPooledList<Region> regionsBuilder, ushort highestRegionId, int minRegionSurface)
        {
            if (minRegionSurface < 1)
                return;
            if (highestRegionId == NULL_REGION)
                return;

            for (int i = 0; i < regionsCount; i++)
            {
                ref ushort region = ref regions[i];
                if (region == NULL_REGION)
                    continue;
                Debug.Assert(NULL_REGION == 0);
                // The -1 is because NULL_REGION is 0.
                if (regionsBuilder[region - 1].count < minRegionSurface)
                    region = NULL_REGION;
            }
        }

        public void DrawGizmos(in Resolution resolution, in CompactOpenHeightField openHeightField)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(resolution.Center, new Vector3(resolution.CellSize.x * resolution.Width, resolution.CellSize.y * resolution.Height, resolution.CellSize.z * resolution.Depth));
            Vector3 offset = (new Vector3(resolution.Width * (-resolution.CellSize.x), resolution.Height * (-resolution.CellSize.y), resolution.Depth * (-resolution.CellSize).z) * .5f) + (resolution.CellSize * .5f);
            offset.y -= resolution.CellSize.y / 2;
            offset += resolution.Center;

            ReadOnlySpan<CompactOpenHeightField.HeightColumn> columns = openHeightField.Columns;
            ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans = openHeightField.Spans;

            int i = 0;
            for (int x = 0; x < resolution.Width; x++)
            {
                for (int z = 0; z < resolution.Depth; z++)
                {
                    Vector2 position_ = new Vector2(x * resolution.CellSize.x, z * resolution.CellSize.z);
                    CompactOpenHeightField.HeightColumn column = columns[i++];

                    if (!column.IsEmpty)
                    {
                        int j = column.First;

                        ref readonly CompactOpenHeightField.HeightSpan heightSpan = ref spans[j];
                        if (heightSpan.Floor != -1)
                            Draw(resolution, heightSpan.Floor - .1f, regions[j]);
                        j++;

                        for (; j < column.Last - 1; j++)
                        {
                            heightSpan = ref spans[j];
                            Draw(resolution, heightSpan.Floor - .1f, regions[j]);
                        }

                        if (column.Count > 1) // Shouldn't this be 2?
                        {
                            Debug.Assert(j == column.Last - 1);
                            heightSpan = ref spans[j];
                            Draw(resolution, heightSpan.Floor, regions[j]);
                        }
                    }

                    void Draw(in Resolution resolution_, float y, int region)
                    {
                        if (region == NULL_REGION)
                            Gizmos.color = Color.black;
                        else
                        {
                            // https://gamedev.stackexchange.com/a/46469/99234 from https://gamedev.stackexchange.com/questions/46463/how-can-i-find-an-optimum-set-of-colors-for-10-players
                            const float goldenRatio = 1.61803398874989484820458683436f; // (1 + Math.Sqrt(5)) / 2
                            const float div = 1 / goldenRatio;
                            Gizmos.color = Color.HSVToRGB(region * div % 1f, .5f, Mathf.Sqrt(1 - (region * div % .5f)));
                        }

                        Vector3 position = new Vector3(position_.x, resolution_.CellSize.y * y, position_.y);
                        Vector3 center_ = offset + position;
                        Vector3 size = new Vector3(resolution_.CellSize.x, resolution_.CellSize.y * .1f, resolution_.CellSize.z);
                        Gizmos.DrawCube(center_, size);
                    }
                }
            }
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose() => ArrayPool<ushort>.Shared.Return(regions);
    }
}
