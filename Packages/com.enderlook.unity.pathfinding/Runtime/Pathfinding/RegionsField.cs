using Enderlook.Collections.Pooled.LowLevel;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

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

        public const ushort BORDER_REGION_FLAG = 1 << 15;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBorderRegion(ushort region) => (region & BORDER_REGION_FLAG) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort SetOnBorderRegion(ushort region) => (ushort)(region | BORDER_REGION_FLAG);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort SetOffBorderRegion(ushort region) => (ushort)(region & ~BORDER_REGION_FLAG);

        private RegionsField(ushort[] regions, int regionsCount)
        {
            this.regions = regions;
            this.regionsCount = regionsCount;
        }

        /// <summary>
        /// Calculates the regions of the specified distance field.
        /// </summary>
        /// <param name="distanceField">Distance field whose regions is being calculated.</param>
        /// <param name="openHeightField">Open height field owner of the <paramref name="distanceField"/>.</param>
        /// <param name="options">Stores configuration information.</param>
        /// <returns>The generated regions field.</return>
        public static async ValueTask<RegionsField> Create(DistanceField distanceField, CompactOpenHeightField openHeightField, MeshGenerationOptions options)
        {
            Resolution resolution = options.Resolution;
            openHeightField.DebugAssert(nameof(openHeightField), resolution, $"{nameof(options)}.{nameof(resolution)}");
            distanceField.DebugAssert(nameof(distanceField), resolution,  $"{nameof(options)}.{nameof(resolution)}");

            ReadOnlyArraySlice<ushort> distances = distanceField.Distances;
            ReadOnlyArraySlice<CompactOpenHeightField.HeightSpan> spans = openHeightField.Spans;

            RegionsField self = new RegionsField(ArrayPool<ushort>.Shared.Rent(distances.Length), distances.Length);
            Array.Clear(self.regions, 0, self.regionsCount);
            Debug.Assert(self.regions[0] == NULL_REGION);
            if (options.CheckIfMustYield())
                await options.Yield();

            ushort regionId = 1;
            regionId = await PaintRectangleRegionAsBorder(openHeightField, self.regions, regionId, options);

            RawPooledList<Region> regions = RawPooledList<Region>.Create();
            int[] tmp = ArrayPool<int>.Shared.Rent(0);
            int agentSize = options.AgentSize;
            bool yield = options.HasTimeSlice && !options.UseMultithreading;
            for (int waterLevel = distanceField.MaximumDistance; waterLevel >= agentSize; waterLevel--)
            {
                if (yield)
                    (regions, tmp) = await self.ExpandAllRegionsEqually<MeshGenerationOptions.WithYield>(distances, spans, regions, tmp, waterLevel, options);
                else
                    (regions, tmp) = await self.ExpandAllRegionsEqually<MeshGenerationOptions.WithoutYield>(distances, spans, regions, tmp, waterLevel, options);

                self.FindNewBasins(distances, spans, ref regions, ref regionId, ref tmp, waterLevel);
            }

            Debug.Assert(regionId <= SetOffBorderRegion(ushort.MaxValue));

            // TODO: Handle small regions by deleting them or merging them.

            regions = await self.NullifySmallRegions(regions, options);

            ArrayPool<int>.Shared.Return(tmp);
            if (options.UseMultithreading)
                Parallel.For(0, regions.Count, i => regions[i].Dispose());
            else
            {
                const int unroll = 16;
                int i = 0;
                for (; (i + unroll) < regions.Count; i += unroll)
                {
                    // TODO: Is fine this loop unrolling? The idea is to rarely check the yield.

                    regions[i + 0].Dispose();
                    regions[i + 1].Dispose();
                    regions[i + 2].Dispose();
                    regions[i + 3].Dispose();
                    regions[i + 4].Dispose();
                    regions[i + 5].Dispose();
                    regions[i + 6].Dispose();
                    regions[i + 7].Dispose();
                    regions[i + 8].Dispose();
                    regions[i + 9].Dispose();
                    regions[i + 10].Dispose();
                    regions[i + 11].Dispose();
                    regions[i + 12].Dispose();
                    regions[i + 13].Dispose();
                    regions[i + 14].Dispose();
                    regions[i + 15].Dispose();

                    if (options.CheckIfMustYield())
                        await options.Yield();
                }
                for (; i < regions.Count; i++)
                    regions[i].Dispose();
            }
            regions.Dispose();

            return self;
        }

        /// <summary>
        /// Debug assert that this instance is valid.
        /// </summary>
        /// <param name="parameterName">Name of the instance.</param>
        [System.Diagnostics.Conditional("Debug")]
        public void DebugAssert(string parameterName) => Debug.Assert(!(regions is null), $"{parameterName} is default");

        private static async ValueTask<ushort> PaintRectangleRegionAsBorder(CompactOpenHeightField openHeightField, ushort[] regions, ushort regionId, MeshGenerationOptions options)
        {
            int border = options.RegionBorderThickness;
            if (border == 0)
                return regionId;

            Resolution resolution = options.Resolution;

            ushort region0 = SetOnBorderRegion(regionId++);
            ushort region1 = SetOnBorderRegion(regionId++);
            ushort region2 = SetOnBorderRegion(regionId++);
            ushort region3 = SetOnBorderRegion(regionId++);

            int xBorder = Math.Min(border, resolution.Width);
            int zBorder = Math.Min(border, resolution.Depth);

            if (options.UseMultithreading)
                MultiThread();
            else
                await SingleThread();

            return regionId;

            void MultiThread()
            {
                Parallel.For(0, resolution.Width * zBorder, i =>
                {
                    int x = i / zBorder;
                    int z = i % zBorder;
                    openHeightField.Columns[resolution.GetIndex(x, z)].Span<ushort>(regions).Fill(region0);
                    openHeightField.Columns[resolution.GetIndex(x, resolution.Depth - z - 1)].Span<ushort>(regions).Fill(region1);
                });
                Parallel.For(0, resolution.Depth * xBorder, i =>
                {
                    int z = i / xBorder;
                    int x = i % xBorder;
                    openHeightField.Columns[resolution.GetIndex(x, z)].Span<ushort>(regions).Fill(region2);
                    openHeightField.Columns[resolution.GetIndex(resolution.Width - x - 1, z)].Span<ushort>(regions).Fill(region3);
                });
            }

            async ValueTask SingleThread()
            {
                // TODO: Should be specialize another function for single thread without slices?

                for (int x = 0; x < resolution.Width; x++)
                {
                    int index = resolution.GetIndex(x, 0);
                    for (int z = 0; z < zBorder; z++)
                    {
                        Debug.Assert(index == resolution.GetIndex(x, z));
                        openHeightField.Columns[index++].Span<ushort>(regions).Fill(region0);
                    }

                    index = resolution.GetIndex(x, resolution.Depth - zBorder - 1) + 1; // TODO: Why +1?
                    for (int z = resolution.Depth - zBorder; z < resolution.Depth; z++)
                    {
                        Debug.Assert(index == resolution.GetIndex(x, z));
                        openHeightField.Columns[index++].Span<ushort>(regions).Fill(region1);
                    }

                    // TODO: Should the yield check be done inside the inner loops?
                    if (options.CheckIfMustYield())
                        await options.Yield();
                }

                for (int z = 0; z < resolution.Depth; z++)
                {
                    int index = resolution.GetIndex(0, z);
                    for (int x = 0; x < xBorder; x++)
                    {
                        Debug.Assert(index == resolution.GetIndex(x, z));
                        openHeightField.Columns[index].Span<ushort>(regions).Fill(region2);
                        index += resolution.Width;
                    }

                    index = resolution.GetIndex(resolution.Width - xBorder - 1, z) + resolution.Depth; // TODO: Why + resolution.Depth?
                    for (int x = resolution.Width - xBorder; x < resolution.Width; x++)
                    {
                        Debug.Assert(index == resolution.GetIndex(x, z));
                        openHeightField.Columns[index].Span<ushort>(regions).Fill(region3);
                    }

                    // TODO: Should the yield check be done inside the inner loops?
                    if (options.CheckIfMustYield())
                        await options.Yield();
                }
            }
        }

        private async ValueTask<(RawPooledList<Region> regions, int[] UnderlyingArray)> ExpandAllRegionsEqually<TYield>(ReadOnlyArraySlice<ushort> distances, ReadOnlyArraySlice<CompactOpenHeightField.HeightSpan> spans, RawPooledList<Region> regions, int[] tmp, int waterLevel, MeshGenerationOptions options)
        {
            ushort[] regions_ = this.regions;
            RawPooledList<int> tmp_ = RawPooledList<int>.FromEmpty(tmp);
            bool change = true;
            while (change)
            {
                change = false;
                for (int i = 0; i < regions.Count; i++)
                {
                    // Flood fill mark region.
                    Region region = regions[i];

                    tmp_.Clear();
                    tmp_ = region.SwapBorder(tmp_);
                    for (int j = 0; j < tmp_.Count; j++)
                    {
                        ExpandRegionsBodyLoop(regions_, distances, spans, waterLevel, ref tmp_, ref change, ref region, j);
                        if (options.CheckIfMustYield<TYield>())
                            await options.Yield();
                    }

                    regions[i] = region;
                }
            }
            return (regions, tmp_.UnderlyingArray);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExpandRegionsBodyLoop(ushort[] regions, ReadOnlyArraySlice<ushort> distances, ReadOnlyArraySlice<CompactOpenHeightField.HeightSpan> spans, int waterLevel, ref RawPooledList<int> tmp_, ref bool change, ref Region region, int j)
        {
            int k = tmp_[j];
            ref readonly CompactOpenHeightField.HeightSpan span = ref spans[k];

            bool canKeepGrowing = false;

            ExpandRegionCheckNeighbour(regions, distances, waterLevel, ref region, span.Left, ref change, ref canKeepGrowing);
            ExpandRegionCheckNeighbour(regions, distances, waterLevel, ref region, span.Forward, ref change, ref canKeepGrowing);
            ExpandRegionCheckNeighbour(regions, distances, waterLevel, ref region, span.Right, ref change, ref canKeepGrowing);
            ExpandRegionCheckNeighbour(regions, distances, waterLevel, ref region, span.Backward, ref change, ref canKeepGrowing);

            if (canKeepGrowing)
                region.AddSpanToBorder(k);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExpandRegionCheckNeighbour(ushort[] regions, ReadOnlyArraySlice<ushort> distances, int waterLevel, ref Region region, int neighbour, ref bool didGrow, ref bool canGrow)
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

        private void FindNewBasins(ReadOnlySpan<ushort> distances, ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, ref RawPooledList<Region> regions, ref ushort regionId, ref int[] tmp, int waterLevel)
        {
            // TODO: If we stored a sorted copy of distances span, this would not require to loop the whole span. Research if it's worth the optimization.

            ushort[] thisRegions = this.regions;
            for (int i = 0; i < distances.Length; i++)
            {
                ref readonly CompactOpenHeightField.HeightSpan span = ref spans[i];
                if (distances[i] == waterLevel && thisRegions[i] == NULL_REGION)
                {
                    regions.Add(new Region(regionId));
                    ref Region region = ref regions[regions.Count - 1];
                    region.AddSpanToBorder(i);
                    Debug.Assert(thisRegions[i] != regionId);
                    region.count++;
                    thisRegions[i] = regionId;
                    regionId++;
                    FloodRegion(distances, spans, waterLevel, i, ref region, ref tmp);
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
                    ushort neighbourRegion = regions[neighbour];
                    if (neighbourRegion == NULL_REGION)
                        continue;
                    if (neighbourRegion != region.id)
                        ar = neighbourRegion;

                    ref readonly CompactOpenHeightField.HeightSpan span_ = ref spans[neighbour];
                    int neighbour_ = span_.GetSide(CompactOpenHeightField.HeightSpan.RotateClockwise(neighbour));
                    if (neighbour_ == CompactOpenHeightField.HeightSpan.NULL_SIDE)
                        continue;
                    ushort neighbourRegion_ = regions[neighbour_];
                    if (neighbourRegion_ != region.id)
                        ar = neighbourRegion_;
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

                /*ushort ar = 0;
                ushort regionId = region.id;
                A<Side.Left>(spans, regionId, ref ar, span.Left);
                A<Side.Forward>(spans, regionId, ref ar, span.Forward);
                A<Side.Right>(spans, regionId, ref ar, span.Right);
                A<Side.Backward>(spans, regionId, ref ar, span.Backward);

                if (ar != 0)
                {
                    regions[value] = NULL_REGION;
                    continue;
                }

                B<Side.Left>(distances, waterLevel, regionId, span, ref stack);
                B<Side.Forward>(distances, waterLevel, regionId, span, ref stack);
                B<Side.Right>(distances, waterLevel, regionId, span, ref stack);
                B<Side.Backward>(distances, waterLevel, regionId, span, ref stack);*/
                
                FloodRegionCheckNeighbour<Side.Left>(spans, distances, waterLevel, ref region, ref stack, span.Left);
                FloodRegionCheckNeighbour<Side.Forward>(spans, distances, waterLevel, ref region, ref stack, span.Forward);
                FloodRegionCheckNeighbour<Side.Right>(spans, distances, waterLevel, ref region, ref stack, span.Right);
                FloodRegionCheckNeighbour<Side.Backward>(spans, distances, waterLevel, ref region, ref stack, span.Backward);
            }

            tmp = stack.UnderlyingArray;
        }

        /*[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void A<T>(ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, int regionId, ref ushort ar, int neighbour)
        {
            Side.DebugAssert<T>();

            if (neighbour == CompactOpenHeightField.HeightSpan.NULL_SIDE)
                return;

            ushort neighbourRegion = regions[neighbour];
            if (neighbourRegion == NULL_REGION)
                return;

            if (neighbourRegion != regionId)
                ar = neighbourRegion;

            ref readonly CompactOpenHeightField.HeightSpan span = ref spans[neighbour];

            int neighbour_ = span.GetSideRotatedClockwise<T>();
            if (neighbour_ == CompactOpenHeightField.HeightSpan.NULL_SIDE)
                return;

            ushort neighbourRegion_ = regions[neighbour_];
            if (neighbourRegion_ == NULL_REGION)
                return;

            if (neighbourRegion_ != regionId)
                ar = neighbourRegion_;
        }*/

        /*[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void B<T>(ReadOnlySpan<ushort> distances, int waterLevel, ushort regionId, in CompactOpenHeightField.HeightSpan span, ref RawPooledStack<int> stack)
        {
            Side.DebugAssert<T>();

            int neighbour = span.GetSide<T>();

            if (neighbour == CompactOpenHeightField.HeightSpan.NULL_SIDE)
                return;
            if (distances[neighbour] >= waterLevel && regions[neighbour] == NULL_REGION)
            {
                regions[neighbour] = regionId;
                Unsafe.AsRef(distances[neighbour]) = 0;
                stack.Push(neighbour);
            }
        }*/

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FloodRegionCheckNeighbour<T>(ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, ReadOnlySpan<ushort> distances, int waterLevel, ref Region region, ref RawPooledStack<int> stack, int neighbour)
        {
            Side.DebugAssert<T>();

            if (neighbour == CompactOpenHeightField.HeightSpan.NULL_SIDE)
                return;

            if (distances[neighbour] >= waterLevel && regions[neighbour] == NULL_REGION)
            {
                Debug.Assert(regions[neighbour] != region.id);
                regions[neighbour] = region.id;
                region.count++;
                region.AddSpanToBorder(neighbour);
                stack.Push(neighbour);
            }

            ref readonly CompactOpenHeightField.HeightSpan span = ref spans[neighbour];

            int neighbour_ = span.GetSideRotatedClockwise<T>();
            if (neighbour_ == CompactOpenHeightField.HeightSpan.NULL_SIDE)
                return;

            if (distances[neighbour_] >= waterLevel && regions[neighbour_] == NULL_REGION)
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

        private async ValueTask<RawPooledList<Region>> NullifySmallRegions(RawPooledList<Region> regionsBuilder, MeshGenerationOptions options)
        {
            int minimumRegionSurface = options.MinimumRegionSurface;
            if (minimumRegionSurface <= 1)
                return regionsBuilder;

            options.PushTask(regionsCount, "Nullify Small Regions");
            {
                if (options.UseMultithreading)
                {
                    ushort[] regions = this.regions;
                    Parallel.For(0, regionsCount, i =>
                    {
                        NullifySmallRegionsLoopBody(regions, regionsBuilder, minimumRegionSurface, i);
                        options.StepTask();
                    });
                }
                else if (options.HasTimeSlice)
                {
                    for (int i = 0; i < regionsCount; i++)
                    {
                        // TODO: Should unroll this loop? The idea would be to reduce the amount of yield checks.
                        NullifySmallRegionsLoopBody(regions, regionsBuilder, minimumRegionSurface, i);
                        if (options.StepTaskAndCheckIfMustYield())
                            await options.Yield();
                    }
                }
                else
                {
                    for (int i = 0; i < regionsCount; i++)
                    {
                        NullifySmallRegionsLoopBody(regions, regionsBuilder, minimumRegionSurface, i);
                        options.StepTask();
                    }
                }
            }
            options.PopTask();

            return regionsBuilder;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void NullifySmallRegionsLoopBody(ushort[] regions, RawPooledList<Region> regionsBuilder, int minimumRegionSurface, int i)
        {
            ref ushort region = ref regions[i];
            Debug.Assert(NULL_REGION == 0);
            ushort region_ = SetOffBorderRegion(region);
            // The 1 is because NULL_REGION is 0, and 4 because the first 4 regions are borders and aren't stored on the list.
            if (region_ < (1 + 4))
                return;
            ushort index = (ushort)(region_ - 1 - 4);
            if (regionsBuilder[index].count < minimumRegionSurface)
                region = NULL_REGION;
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
