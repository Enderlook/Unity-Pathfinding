using Enderlook.Collections.Pooled.LowLevel;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    /// <summary>
    /// Stores the contours of <see cref="RegionsField"/>.
    /// </summary>
    internal readonly struct Contours : IDisposable
    {
        private const byte LEFT_IS_REGIONAL = 1 << (CompactOpenHeightField.HeightSpan.LEFT_DIRECTION + 1);
        private const byte FORWARD_IS_REGIONAL = 1 << (CompactOpenHeightField.HeightSpan.FORWARD_DIRECTION + 1);
        private const byte RIGHT_IS_REGIONAL = 1 << (CompactOpenHeightField.HeightSpan.RIGHT_DIRECTION + 1);
        private const byte BACKWARD_IS_REGIONAL = 1 << (CompactOpenHeightField.HeightSpan.BACKWARD_DIRECTION + 1);
        private const byte IS_USED = 1 << 7;

        private readonly RawPooledList<RawPooledList<ContourPoint>> contours;

        /// <summary>
        /// Calculates the contours of the specified regions.
        /// </summary>
        /// <param name="regionsField">Regions whose contours is being calculated.</param>
        /// <param name="openHeightField">Open height field owner of the <paramref name="regionsField"/>.</param>
        /// <param name="resolution">Resolution of the <paramref name="regionsField"/>.</param>
        /// <param name="maximumEdgeDeviation">Determines the maximum distance the edges of meshes may deviate from the source geometry.<br/>
        /// A lower value will result in mesh edges following the xz-plane geometry contour more accurately at the expense of an increased triangle count.<br/>
        /// A value to zero is not recommended since it can result in a large increase in the number of polygons in the final meshes at a high processing cost.</param>
        /// <param name="maximumEdgeLength">The maximum length of polygon edges that represent the border of meshses.<br/>
        /// More vertices will be added to border edges if this avlue is exceeded for a particualr edge.<br/>
        /// In certain cases this will reduce the number of long thin triangles.<br/>
        /// A value of zero will disable this feature.</param>
        /// <param name="maxIterations">Maximum amount of iterations used to walk along the contours.</param>
        public Contours(in RegionsField regionsField, in CompactOpenHeightField openHeightField, in Resolution resolution, int maximumEdgeDeviation, int maximumEdgeLength = 0, int maxIterations = 40000)
        {
            regionsField.DebugAssert(nameof(regionsField));
            openHeightField.DebugAssert(nameof(openHeightField), resolution, nameof(resolution));
            Debug.Assert(maximumEdgeDeviation >= 0, $"{nameof(maximumEdgeDeviation)} can't be negative.");
            Debug.Assert(maximumEdgeLength >= 0, $"{nameof(maximumEdgeLength)} can't be negative.");

            ReadOnlySpan<ushort> regions = regionsField.Regions;
            ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans = openHeightField.Spans;
            ReadOnlySpan<CompactOpenHeightField.HeightColumn> columns = openHeightField.Columns;
            Debug.Assert(regions.Length == spans.Length);

            byte[] edgeFlags = CreateFlags(regions, spans);
            try
            {
                contours = RawPooledList<RawPooledList<ContourPoint>>.Create();
                try
                {
                    RawPooledList<ContourPoint> edgeContour = RawPooledList<ContourPoint>.Create();
                    try
                    {
                        FindContours(resolution, regions, spans, columns, edgeFlags, ref edgeContour, ref contours, maxIterations, maximumEdgeDeviation, maximumEdgeLength);
                    }
                    finally
                    {
                        edgeContour.Dispose();
                    }
                }
                catch
                {
                    for (int i = 0; i < contours.Count; i++)
                        contours[i].Dispose();
                    contours.Dispose();
                    throw;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(edgeFlags);
            }
        }

        private void FindContours(in Resolution resolution, ReadOnlySpan<ushort> regions, ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, ReadOnlySpan<CompactOpenHeightField.HeightColumn> columns, byte[] edgeFlags, ref RawPooledList<ContourPoint> edgeContour, ref RawPooledList<RawPooledList<ContourPoint>> contours, int maxIterations, int maximumEdgeDeviation, int maximumEdgeLength)
        {
            int spanIndex = 0;
            int columnIndex = 0;
            for (int x = 0; x < resolution.Width; x++)
            {
                for (int z = 0; z < resolution.Depth; z++)
                {
                    Debug.Assert(columnIndex == resolution.GetIndex(x, z));
                    CompactOpenHeightField.HeightColumn column = columns[columnIndex++];

                    for (int i = column.First; i < column.Last; i++)
                    {
                        ref byte flags = ref edgeFlags[spanIndex];
                        if (flags == 0 || (flags & IS_USED) != 0)
                        {
                            flags = 0;
                            spanIndex++;
                            continue;
                        }

                        ushort region = regions[spanIndex];
                        if (region == RegionsField.NULL_REGION)
                        {
                            spanIndex++;
                            continue;
                        }

                        WalkContour(regions, spans, edgeFlags, ref edgeContour, x, z, spanIndex, ref flags, maxIterations);
                        //RawPooledList<ContourPoint> simplified = SimplifyContour(ref edgeContour, maximumEdgeDeviation, maximumEdgeLength);
                        try
                        {
                            //contours.Add(simplified);
                            contours.Add(RawPooledList<ContourPoint>.Create(edgeContour.AsSpan()));
                        }
                        catch
                        {
                            //simplified.Dispose();
                            throw;
                        }
                        spanIndex++;
                    }
                }
            }
        }

        private static byte[] CreateFlags(ReadOnlySpan<ushort> regions, ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans)
        {
            byte[] edgeFlags = ArrayPool<byte>.Shared.Rent(spans.Length);
            try
            {
                Array.Clear(edgeFlags, 0, spans.Length);
                for (int i = 0; i < spans.Length; i++)
                {
                    ref readonly CompactOpenHeightField.HeightSpan span = ref spans[i];
                    ref byte edgeFlag = ref edgeFlags[i];

                    NeighbourMarkFlag(regions, ref edgeFlag, i, span.Left, LEFT_IS_REGIONAL);
                    NeighbourMarkFlag(regions, ref edgeFlag, i, span.Forward, FORWARD_IS_REGIONAL);
                    NeighbourMarkFlag(regions, ref edgeFlag, i, span.Right, RIGHT_IS_REGIONAL);
                    NeighbourMarkFlag(regions, ref edgeFlag, i, span.Backward, BACKWARD_IS_REGIONAL);
                }
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(edgeFlags);
                throw;
            }
            return edgeFlags;
        }

        private void WalkContour(ReadOnlySpan<ushort> regions, ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, byte[] edgeFlags, ref RawPooledList<ContourPoint> edgeContour, int x, int z, int spanIndex, ref byte initialFlags, int maxIterations)
        {
            ref readonly CompactOpenHeightField.HeightSpan heightSpan = ref spans[spanIndex];
            int py = heightSpan.Floor;
            // Choose first edge.
            int direction;
            if (IsRegion(initialFlags, LEFT_IS_REGIONAL))
            {
                direction = CompactOpenHeightField.HeightSpan.LEFT_DIRECTION;
                py = GetIndexOfSideCheckNeighbours<Side.Left>(spans, spanIndex, py, heightSpan);
            }
            else if (IsRegion(initialFlags, FORWARD_IS_REGIONAL))
            {
                direction = CompactOpenHeightField.HeightSpan.FORWARD_DIRECTION;
                py = GetIndexOfSideCheckNeighbours<Side.Forward>(spans, spanIndex, py, heightSpan);
            }
            else if (IsRegion(initialFlags, RIGHT_IS_REGIONAL))
            {
                direction = CompactOpenHeightField.HeightSpan.RIGHT_DIRECTION;
                py = GetIndexOfSideCheckNeighbours<Side.Right>(spans, spanIndex, py, heightSpan);
            }
            else if (IsRegion(initialFlags, BACKWARD_IS_REGIONAL))
            {
                direction = CompactOpenHeightField.HeightSpan.BACKWARD_DIRECTION;
                py = GetIndexOfSideCheckNeighbours<Side.Backward>(spans, spanIndex, py, heightSpan);
            }
            else
            {
                Debug.Assert(false, "Impossible state.");
                return;
            }

            edgeContour.Clear();
            GetPoints(x, z, direction, out int px, out int pz);
            bool isBorderVertex = IsBorderVertex(spans, regions, spanIndex, direction);
            edgeContour.Add(new ContourPoint(px, py, pz, regions[spanIndex], isBorderVertex));
            initialFlags |= IS_USED;

            int startSpan = spanIndex;
            int startDirection = direction;

            Loop(regions, spans, ref edgeContour);

            void Loop(ReadOnlySpan<ushort> regions_, ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans_, ref RawPooledList<ContourPoint> edgeContour_)
            {
                int iterations = 0;
                do
                {
                    ref byte flags = ref edgeFlags[spanIndex];

                    if (IsRegion(flags, ToFlag(direction)))
                    {
                        GetPoints(x, z, direction, out px, out pz);
                        bool isBorderVertex_ = IsBorderVertex(spans_, regions_, spanIndex, direction);

                        edgeContour_.Add(new ContourPoint(px, py, pz, regions_[spanIndex], isBorderVertex_));
                        flags |= IS_USED;
                        direction = CompactOpenHeightField.HeightSpan.RotateClockwise(direction);
                    }
                    else
                    {
                        GetIndexOfSide(spans_, ref spanIndex, ref x, ref z, out py, direction);

                        if (spanIndex == CompactOpenHeightField.HeightSpan.NULL_SIDE)
                        {
                            // Should not happen?
                            Debug.Assert(false, "Impossible state");
                            break;
                        }

                        direction = CompactOpenHeightField.HeightSpan.RotateCounterClockwise(direction);
                    }
                } while (iterations++ < maxIterations && !(startSpan == spanIndex && startDirection == direction));

                /*while (true)
                {

                    {
                        ref byte flags = ref edgeFlags[spanIndex];

                        if (!IsRegion(flags, ToFlag(direction)))
                            goto end;
                        GetPoints(x, z, direction, out px, out pz);

                        edgeContour_.Add((px, pz, py));
                        direction = CompactOpenHeightField.HeightSpan.RotateClockwise(direction);
                        flags |= IS_USED;
                        if (startSpan == spanIndex && startDirection == direction)
                            break;

                        if (!IsRegion(flags, ToFlag(direction)))
                            goto end;
                        GetPoints(x, z, direction, out px, out pz);
                        edgeContour_.Add((px, pz, py));
                        direction = CompactOpenHeightField.HeightSpan.RotateClockwise(direction);
                        if (startSpan == spanIndex && startDirection == direction)
                            break;

                        if (!IsRegion(flags, ToFlag(direction)))
                            goto end;
                        GetPoints(x, z, direction, out px, out pz);
                        edgeContour_.Add((px, pz, py));
                        direction = CompactOpenHeightField.HeightSpan.RotateClockwise(direction);
                        if (startSpan == spanIndex && startDirection == direction)
                            break;

                        if (!IsRegion(flags, ToFlag(direction)))
                            goto end;
                        GetPoints(x, z, direction, out px, out pz);
                        edgeContour_.Add((px, pz, py));
                        direction = CompactOpenHeightField.HeightSpan.RotateClockwise(direction);
                        if (startSpan == spanIndex && startDirection == direction)
                            break;

                        end:
                        GetIndexOfSide(spans_, ref spanIndex, ref x, ref z, out py, direction);

                        if (spanIndex == CompactOpenHeightField.HeightSpan.NULL_SIDE)
                        {
                            // Should not happen?
                            Debug.Assert(false);
                            break;
                        }

                        direction = CompactOpenHeightField.HeightSpan.RotateCounterClockwise(direction);
                    }
                }*/
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsBorderVertex(ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, ReadOnlySpan<ushort> regions, int spanIndex, int direction)
        {
            ref readonly CompactOpenHeightField.HeightSpan span = ref spans[spanIndex];
            int direction_ = CompactOpenHeightField.HeightSpan.RotateClockwise(direction);

            ushort region0 = regions[spanIndex];
            ushort region1 = 0;
            ushort region2 = 0;
            ushort region3 = 0;

            int neighbour = span.GetSide(direction);
            if (neighbour != CompactOpenHeightField.HeightSpan.NULL_SIDE)
            {
                region1 = regions[neighbour];

                neighbour = spans[neighbour].GetSide(direction_);
                if (neighbour != CompactOpenHeightField.HeightSpan.NULL_SIDE)
                    region2 = regions[neighbour];
            }

            neighbour = span.GetSide(direction_);
            if (neighbour != CompactOpenHeightField.HeightSpan.NULL_SIDE)
            {
                region3 = regions[neighbour];

                neighbour = spans[neighbour].GetSide(direction);
                if (neighbour != CompactOpenHeightField.HeightSpan.NULL_SIDE)
                    region2 = regions[neighbour];
            }

            // Check if the vertex is special edge vertex, these vertices will be removed later.
            return IsSpecialEdgeVertex(region0, region1, region2, region3)
                || IsSpecialEdgeVertex(region1, region2, region3, region0)
                || IsSpecialEdgeVertex(region2, region3, region0, region1)
                || IsSpecialEdgeVertex(region3, region0, region1, region2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSpecialEdgeVertex(ushort regionA, ushort regionB, ushort regionC, ushort regionD)
        {
            // The vertex is a border vertex if there are two same exterior cells in a row,
            // followed by two interior cells and none of the regions are out of bounds.
            bool twoSameExts = (regionA + regionB) == RegionsField.NULL_REGION;
            Debug.Assert(twoSameExts == (regionA == RegionsField.NULL_REGION && regionB == RegionsField.NULL_REGION && regionA == regionB));
            bool twoInts = regionC != RegionsField.NULL_REGION && regionD != RegionsField.NULL_REGION;
            bool noZeros = regionA != 0 && regionB != 0 && regionC != 0 && regionD != 0;
            if (twoSameExts && twoInts && noZeros)
                return true;
            return false;
        }

        private RawPooledList<ContourPoint> SimplifyContour(ref RawPooledList<ContourPoint> edgeContour, int maximumEdgeDeviation, int maximumEdgeLength)
        {
            // Add initial points.
            bool hasConnections = false;
            int edgeContourCount = edgeContour.Count;
            for (int i = 0; i < edgeContourCount; i++)
            {
                if (edgeContour[i].Region != RegionsField.NULL_REGION)
                {
                    hasConnections = true;
                    break;
                }
            }

            RawPooledList<ContourPoint> simplified = RawPooledList<ContourPoint>.Create();
            try
            {
                if (hasConnections)
                {
                    // The contour has some portals (connections) to other regions.
                    // Add a new point to every location where the region changes.
                    for (int i = 0; i < edgeContourCount - 1; i++)
                    {
                        int ii = (i + 1) % edgeContourCount;
                        ContourPoint point = edgeContour[i];
                        bool differentRegions = point.Region != edgeContour[ii].Region;
                        if (differentRegions)
                            simplified.Add(new ContourPoint(point.X, point.Y, point.Z, i));
                    }
                }

                if (simplified.Count == 0)
                {
                    // If there are not connections, create some initial points for the simplification process.
                    // Find lower-left and upper-right vertices of the contour.
                    ContourPoint point = edgeContour[0];
                    int lowerLeftX = point.X;
                    int lowerLeftY = point.Y;
                    int lowerLeftZ = point.Z;
                    int lowerLeftI = 0;
                    int upperRightX = point.X;
                    int upperRightY = point.Y;
                    int upperRightZ = point.Z;
                    int upperRightI = 0;
                    for (int i = 0; i < edgeContourCount; i++)
                    {
                        point = edgeContour[i];
                        if (point.X < lowerLeftX || (point.X == lowerLeftX && point.Z < lowerLeftZ))
                        {
                            lowerLeftX = point.X;
                            lowerLeftY = point.Y;
                            lowerLeftZ = point.Z;
                            lowerLeftI = i;
                        }
                        if (point.X > upperRightX || (point.X == upperRightX && lowerLeftZ > upperRightZ))
                        {
                            upperRightX = point.X;
                            upperRightY = point.Y;
                            upperRightZ = point.Z;
                            upperRightI = i;
                        }
                    }
                    simplified.Add(new ContourPoint(lowerLeftX, lowerLeftY, lowerLeftZ, lowerLeftI));
                    simplified.Add(new ContourPoint(upperRightX, upperRightY, upperRightZ, upperRightI));
                }

                // Add points until all raw points are within error tolerance of the simplified shape.
                for (int i = 0; i < simplified.Count;)
                {
                    int ii = (i + 1) % simplified.Count;

                    ContourPoint pointA = simplified[i];
                    ContourPoint pointB = simplified[ii];

                    // Find maximum deviation from the segment.
                    float maximumD = 0;
                    int maximumI = -1;
                    int ci;
                    int cinc;
                    int endi;

                    // Traverse the segment in lexilogical order so that the maximum deviation
                    // is calculated similarly when travesing opposite segments.
                    if (pointB.X > pointA.X || (pointB.X == pointA.X && pointB.Z > pointA.Z))
                    {
                        cinc = 1;
                        ci = (pointA.I + cinc) % edgeContourCount;
                        endi = pointB.I;
                    }
                    else
                    {
                        cinc = edgeContourCount - 1;
                        ci = (pointB.I + cinc) % edgeContourCount;
                        endi = pointA.I;
                    }

                    // Tesselate only outer edges or edges between areas.

                    if (edgeContour[ci].Region == RegionsField.NULL_REGION)
                    {
                        while (ci != endi)
                        {
                            ContourPoint pointCI = edgeContour[ci];
                            float d = DistancePointSegment(pointCI.X, pointCI.Z, pointA.X, pointA.Z, pointB.X, pointB.Z);
                            if (d > maximumD)
                            {
                                maximumD = d;
                                maximumI = ci;
                            }
                            ci = (ci + cinc) % edgeContourCount;
                        }
                    }

                    // If the maximum deviation is larger than accepted error, add new point, else continue to next segment.
                    if (maximumI != -1 && maximumD > (maximumEdgeDeviation * maximumEdgeDeviation))
                    {
                        ContourPoint pointMaximumI = edgeContour[maximumI];
                        simplified.Insert(i, new ContourPoint(pointMaximumI.X, pointMaximumI.Y, pointMaximumI.Z, maximumI));
                    }
                    else
                        i++;
                }

                // Split too long edge.
                if (maximumEdgeLength > 0)
                {
                    for (int i = 0; i < simplified.Count;)
                    {
                        int ii = (i + 1) % simplified.Count;

                        ContourPoint pointA = simplified[i];
                        ContourPoint pointB = simplified[ii];

                        // Find maximum deviation from the segment.
                        int maximumI = -1;
                        int ci = (pointA.I + 1) % edgeContourCount;

                        // Tessellate only outer edges.
                        if (edgeContour[ci].Region == RegionsField.NULL_REGION)
                        {
                            int dx = pointB.X - pointA.X;
                            int dz = pointB.Z - pointA.Z;
                            if (((dx * dx) + (dz * dz)) > maximumEdgeLength * maximumEdgeLength)
                            {
                                // Round based on the segments in lexilogical order so that the maximum tesselation
                                // is consistent regardles in which direction segments are traversed.
                                int n = pointB.I < pointA.I ? (pointB.I + edgeContourCount - pointA.I) : (pointB.I - pointA.I);
                                if (n > 1)
                                {
                                    if (pointB.X > pointA.X || (pointB.X == pointA.X && pointB.Z > pointA.Z))
                                        maximumI = (pointA.I + (n / 2)) % edgeContourCount;
                                    else
                                        maximumI = (pointA.I + ((n + 1) / 2)) % edgeContourCount;
                                }
                            }
                        }

                        // If the maximum deviation is larget than accepted error, add new point, else continue to next segment.
                        if (maximumI != -1)
                        {
                            ContourPoint pointMaximumI = edgeContour[maximumI];
                            simplified.Insert(i, new ContourPoint(pointMaximumI.X, pointMaximumI.Y, pointMaximumI.Z, maximumI));
                        }
                        else
                            i++;
                    }
                }

                for (int i = 0; i < simplified.Count; i++)
                {
                    // The neighbour region is take from the next raw point.
                    ContourPoint point = simplified[i];
                    int ai = (simplified[i].I + 1) % edgeContourCount;
                    int bi = simplified[i].I;
                    simplified[i] = new ContourPoint(point.X, point.Y, point.Z, edgeContour[ai].Region, edgeContour[bi].IsBorder);
                }

                return simplified;
            }
            catch
            {
                simplified.Dispose();
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DistancePointSegment(int x, int z, int px, int pz, int qx, int qz)
        {
            float pqx = qx - px;
            float pqz = qz - pz;
            float dx = x - px;
            float dz = z - pz;
            float d = (pqx * pqx) + (pqz * pqz);
            float t = (pqx * dx) + (pqz * dz);

            if (d > 0)
                t /= d;
            if (t < 0)
                t = 0;
            else if (t > 1)
                t = 1;

            dx = px + (t * pqx) - x;
            dz = pz + (t * pqz) - z;

            return (dx * dx) + (dz * dz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetIndexOfSide(ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, ref int spanIndex, ref int x, ref int z, out int y, int direction)
        {
            /* This function parameters to point to the specified spans.
             * We must also return the Y value of the corner.
             * However each corner can be composed up to 4 spans (neighbours). E.g:
             * s s
             *  .
             * s s
             * That means we have from 1 to 4 different Y values, which one we must choose?
             * We choose the highest value of them.
             * This ensures that the final vertex is above the surface of the source mesh.
             * And provides a common selection mechanism so that all contours that use the vertex will use the same height.
             */
            ref readonly CompactOpenHeightField.HeightSpan span = ref spans[spanIndex];
            y = span.Floor;

            // Don't use a switch statement because the Jitter doesn't inline them.
            if (direction == CompactOpenHeightField.HeightSpan.LEFT_DIRECTION)
            {
                spanIndex = span.Left;
                x--;
                y = GetIndexOfSideCheckNeighbours<Side.Left>(spans, spanIndex, y, in span);
            }
            else if (direction == CompactOpenHeightField.HeightSpan.FORWARD_DIRECTION)
            {
                spanIndex = span.Forward;
                z++;
                y = GetIndexOfSideCheckNeighbours<Side.Forward>(spans, spanIndex, y, in span);
            }
            else if (direction == CompactOpenHeightField.HeightSpan.RIGHT_DIRECTION)
            {
                spanIndex = span.Right;
                x++;
                y = GetIndexOfSideCheckNeighbours<Side.Right>(spans, spanIndex, y, in span);
            }
            else
            {
                Debug.Assert(direction == CompactOpenHeightField.HeightSpan.BACKWARD_DIRECTION);
                spanIndex = span.Backward;
                z--;
                y = GetIndexOfSideCheckNeighbours<Side.Backward>(spans, spanIndex, y, in span);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetIndexOfSideCheckNeighbours<T>(ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, int spanIndex, int y, in CompactOpenHeightField.HeightSpan span)
        {
            Side.DebugAssert<T>();

            if (spanIndex != CompactOpenHeightField.HeightSpan.NULL_SIDE)
            {
                ref readonly CompactOpenHeightField.HeightSpan neighbour = ref spans[spanIndex];
                y = Mathf.Max(y, neighbour.Floor);

                int index1, index2, index3 = -1;

                if (typeof(T) == typeof(Side.Left))
                {
                    index1 = neighbour.Forward;
                    index2 = span.Forward;
                }
                else if (typeof(T) == typeof(Side.Forward))
                {
                    index1 = neighbour.Right;
                    index2 = span.Right;
                }
                else if (typeof(T) == typeof(Side.Right))
                {
                    index1 = neighbour.Backward;
                    index2 = span.Backward;
                }
                else if (typeof(T) == typeof(Side.Backward))
                {
                    index1 = neighbour.Left;
                    index2 = span.Left;
                }
                else
                {
                    index1 = index2 = default;
                    Debug.Assert(false, "Impossible state.");
                }

                if (index1 != CompactOpenHeightField.HeightSpan.NULL_SIDE)
                    y = Mathf.Max(y, spans[index1].Floor);
                if (index2 != CompactOpenHeightField.HeightSpan.NULL_SIDE)
                {
                    ref readonly CompactOpenHeightField.HeightSpan neighbour2 = ref spans[index2];
                    if (typeof(T) == typeof(Side.Left))
                        index3 = neighbour2.Left;
                    else if (typeof(T) == typeof(Side.Forward))
                        index3 = neighbour2.Forward;
                    else if (typeof(T) == typeof(Side.Right))
                        index3 = neighbour2.Right;
                    else if (typeof(T) == typeof(Side.Backward))
                        index3 = neighbour2.Backward;
                }
                if (index3 != CompactOpenHeightField.HeightSpan.NULL_SIDE)
                    y = Mathf.Max(y, spans[index2].Floor);
            }

            return y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetPoints(int x, int z, int direction, out int px, out int pz)
        {
            px = x;
            pz = z;

            // Don't use a switch statement because the Jitter doesn't inline them.
            if (direction == CompactOpenHeightField.HeightSpan.LEFT_DIRECTION)
                pz++;
            else if (direction == CompactOpenHeightField.HeightSpan.FORWARD_DIRECTION)
            {
                px++;
                pz++;
            }
            else if (direction == CompactOpenHeightField.HeightSpan.RIGHT_DIRECTION)
                px++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsRegion(byte value, byte flag) => (value & flag) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ToFlag(int direction)
        {
            Debug.Assert(direction <= 3);
            return (byte)(1 << (direction + 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void NeighbourMarkFlag(ReadOnlySpan<ushort> regions, ref byte edgeFlag, int i, int neighbour, byte isRegional)
        {
            if (neighbour != CompactOpenHeightField.HeightSpan.NULL_SIDE)
            {
                ushort regionNeighbour = regions[neighbour];
                if (regionNeighbour == RegionsField.NULL_REGION)
                    edgeFlag |= isRegional;
                else if (regionNeighbour != regions[i])
                    edgeFlag |= isRegional;
            }
            else
                edgeFlag |= isRegional;
        }

        public void DrawGizmos(in Resolution resolution, in CompactOpenHeightField openHeightField, in RegionsField regionsField)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(resolution.Center, new Vector3(resolution.CellSize.x * resolution.Width, resolution.CellSize.y * resolution.Height, resolution.CellSize.z * resolution.Depth));
            Vector3 offset = new Vector3(resolution.Width * (-resolution.CellSize.x), resolution.Height * (-resolution.CellSize.y), resolution.Depth * (-resolution.CellSize).z) * .5f;// + (resolution.CellSize * .5f);
            offset.y -= resolution.CellSize.y / 2;
            offset += resolution.Center;

            RawPooledList<RawPooledList<ContourPoint>> contours = this.contours;
            for (int i = 0; i < contours.Count; i++)
            {
               // https://gamedev.stackexchange.com/a/46469/99234 from https://gamedev.stackexchange.com/questions/46463/how-can-i-find-an-optimum-set-of-colors-for-10-players
                const float goldenRatio = 1.61803398874989484820458683436f; // (1 + Math.Sqrt(5)) / 2
                const float div = 1 / goldenRatio;
                Gizmos.color = Color.HSVToRGB(i * div % 1f, .5f, Mathf.Sqrt(1 - (i * div % .5f)));

                RawPooledList<ContourPoint> contour = contours[i];

                ContourPoint point = contour[0];
                Vector2 position_ = new Vector2(point.X * resolution.CellSize.x, point.Z * resolution.CellSize.z);
                Vector3 position = new Vector3(position_.x, resolution.CellSize.y * point.Y, position_.y);
                Vector3 center_ = offset + position;
                Vector3 center_1 = center_;
                Vector3 center_2 = center_;
                for (int j = 1; j < contour.Count; j++)
                {
                    point = contour[j];
                    position_ = new Vector2(point.X * resolution.CellSize.x, point.Z * resolution.CellSize.z);
                    position = new Vector3(position_.x, resolution.CellSize.y * point.Y, position_.y);
                    center_2 = offset + position;
                    Gizmos.DrawLine(center_, center_2);
                    center_ = center_2;
                }
                Gizmos.DrawLine(center_2, center_1);
            }
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose() => contours.Dispose();

        private readonly struct ContourPoint
        {
            public readonly int X;
            public readonly int Y;
            public readonly int Z;
            private readonly uint payload;

            public ushort Region {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (ushort)(payload & ushort.MaxValue);
            }

            public bool IsBorder {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (payload & 1 << 16) != 0;
            }

            public int I {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (int)payload;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ContourPoint(int x, int y, int z, ushort region, bool isBorderVertex)
            {
                X = x;
                Y = y;
                Z = z;
                payload = region | ((uint)(isBorderVertex ? 1 : 0) << 16);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ContourPoint(int x, int y, int z, int i)
            {
                X = x;
                Y = y;
                Z = z;
                payload = (uint)i;
            }
        }
    }
}