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
        private const byte LEFT_IS_REGIONAL = 1 << (CompactOpenHeightField.HeightSpan.LEFT_INDEX + 1);
        private const byte FORWARD_IS_REGIONAL = 1 << (CompactOpenHeightField.HeightSpan.FORWARD_INDEX + 1);
        private const byte RIGHT_IS_REGIONAL = 1 << (CompactOpenHeightField.HeightSpan.RIGHT_INDEX + 1);
        private const byte BACKWARD_IS_REGIONAL = 1 << (CompactOpenHeightField.HeightSpan.BACKWARD_INDEX + 1);
        private const byte IS_USED = 1 << 7;
        private readonly RawPooledList<RawPooledList<(int x, int z, int y)>> contours;

        /// <summary>
        /// Calculates the contours of the specified regions.
        /// </summary>
        /// <param name="regionsField">Regions whose contours is being calculated.</param>
        /// <param name="openHeightField">Open height field owner of the <paramref name="regionsField"/>.</param>
        /// <param name="resolution">Resolution of the <paramref name="regionsField"/>.</param>
        /// <param name="maxIterations">Maximum amount of iterations used to walk along the contours.</param>
        public Contours(in RegionsField regionsField, in CompactOpenHeightField openHeightField, in Resolution resolution, int maxIterations = 40000)
        {
            ReadOnlySpan<ushort> regions = regionsField.Regions;
            ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans = openHeightField.Spans;
            ReadOnlySpan<CompactOpenHeightField.HeightColumn> columns = openHeightField.Columns;
            Debug.Assert(regions.Length == spans.Length);

            byte[] edgeFlags = CreateFlags(regions, spans);
            try
            {
                contours = RawPooledList<RawPooledList<(int x, int z, int y)>>.Create();
                try
                {
                    RawPooledList<(int x, int z, int y)> edgeContour = RawPooledList<(int x, int z, int y)>.Create();
                    try
                    {
                        FindContours(resolution, spans, columns, edgeFlags, ref edgeContour, ref contours, maxIterations);
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

        private void FindContours(in Resolution resolution, ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, ReadOnlySpan<CompactOpenHeightField.HeightColumn> columns, byte[] edgeFlags, ref RawPooledList<(int x, int z, int y)> edgeContour, ref RawPooledList<RawPooledList<(int x, int z, int y)>> contours, int maxIterations)
        {
            int spanIndex = 0;
            int columnIndex = 0;
            int w = 0;
            for (int x = 0; x < resolution.Width; x++)
            {
                for (int z = 0; z < resolution.Depth; z++)
                {
                    Debug.Assert(columnIndex == GetIndex(resolution, x, z));
                    CompactOpenHeightField.HeightColumn column = columns[columnIndex++];

                    for (int i = column.First; i < column.Last; i++)
                    {
                        byte flags = edgeFlags[spanIndex];
                        if (flags == 0 || (flags & IS_USED) != 0)
                        {
                            spanIndex++;
                            continue;
                        }

                        w++;
                        WalkContour(spans, edgeFlags, ref edgeContour, x, z, spanIndex, ref flags, maxIterations);
                        spanIndex++;

                        RawPooledList<(int x, int z, int y)> copy = RawPooledList<(int x, int z, int y)>.Create(edgeContour.AsSpan());
                        try
                        {
                            contours.Add(copy);
                        }
                        catch
                        {
                            copy.Dispose();
                            throw;
                        }
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

                    NeighbourMarkFlag(regions, edgeFlags, i, span.Left, LEFT_IS_REGIONAL);
                    NeighbourMarkFlag(regions, edgeFlags, i, span.Forward, FORWARD_IS_REGIONAL);
                    NeighbourMarkFlag(regions, edgeFlags, i, span.Right, RIGHT_IS_REGIONAL);
                    NeighbourMarkFlag(regions, edgeFlags, i, span.Backward, BACKWARD_IS_REGIONAL);
                }
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(edgeFlags);
                throw;
            }
            return edgeFlags;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetIndex(in Resolution resolution, int x, int z)
        {
            Debug.Assert(x >= 0);
            Debug.Assert(x < resolution.Width);
            Debug.Assert(z >= 0);
            Debug.Assert(z < resolution.Depth);
            int index_ = (resolution.Depth * x) + z;
            Debug.Assert(index_ < resolution.Width * resolution.Depth);
            return index_;
        }


        private void FromEdgeToVertices(ref RawPooledList<(int span, int neighbour)> edgeContour)
        {

        }

        private void WalkContour(ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, byte[] edgeFlags, ref RawPooledList<(int x, int z, int y)> edgeContour, int x, int z, int spanIndex, ref byte initialFlags, int maxIterations)
        {
            ref readonly CompactOpenHeightField.HeightSpan heightSpan = ref spans[spanIndex];
            int py = heightSpan.Floor;
            // Choose first edge.
            int direction;
            if (IsRegion(initialFlags, LEFT_IS_REGIONAL))
            {
                direction = CompactOpenHeightField.HeightSpan.LEFT_INDEX;
                py = GetIndexOfSideCheckNeighbours<Left>(spans, spanIndex, py, heightSpan);
            }
            else if (IsRegion(initialFlags, FORWARD_IS_REGIONAL))
            {
                direction = CompactOpenHeightField.HeightSpan.FORWARD_INDEX;
                py = GetIndexOfSideCheckNeighbours<Forward>(spans, spanIndex, py, heightSpan);
            }
            else if (IsRegion(initialFlags, RIGHT_IS_REGIONAL))
            {
                direction = CompactOpenHeightField.HeightSpan.RIGHT_INDEX;
                py = GetIndexOfSideCheckNeighbours<Right>(spans, spanIndex, py, heightSpan);
            }
            else if (IsRegion(initialFlags, BACKWARD_IS_REGIONAL))
            {
                direction = CompactOpenHeightField.HeightSpan.BACKWARD_INDEX;
                py = GetIndexOfSideCheckNeighbours<Backward>(spans, spanIndex, py, heightSpan);
            }
            else
            {
                Debug.Assert(false, "Impossible state.");
                direction = 0;
            }

            edgeContour.Clear();
            GetPoints(x, z, direction, out int px, out int pz);
            edgeContour.Add((px, pz, py));
            initialFlags |= IS_USED;

            int startSpan = spanIndex;
            int startDirection = direction;

            Loop(spans, ref edgeContour);

            void Loop(ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans_, ref RawPooledList<(int x, int z, int y)> edgeContour_)
            {
                int iterations = 0;
                while (iterations++ < maxIterations)
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
            }
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
             * This ensures that te final vertex is above the surface of the source mesh.
             * And provides a common selection mechanism so that all contours that use the vertex will use the same height.
             */
            ref readonly CompactOpenHeightField.HeightSpan span = ref spans[spanIndex];
            y = span.Floor;
            switch (direction)
            {
                case CompactOpenHeightField.HeightSpan.LEFT_INDEX:
                    spanIndex = span.Left;
                    x--;
                    y = GetIndexOfSideCheckNeighbours<Left>(spans, spanIndex, y, in span);
                    break;
                case CompactOpenHeightField.HeightSpan.FORWARD_INDEX:
                    spanIndex = span.Forward;
                    z++;
                    y = GetIndexOfSideCheckNeighbours<Forward>(spans, spanIndex, y, in span);
                    break;
                case CompactOpenHeightField.HeightSpan.RIGHT_INDEX:
                    spanIndex = span.Right;
                    x++;
                    y = GetIndexOfSideCheckNeighbours<Right>(spans, spanIndex, y, in span);
                    break;
                case CompactOpenHeightField.HeightSpan.BACKWARD_INDEX:
                    spanIndex = span.Backward;
                    z--;
                    y = GetIndexOfSideCheckNeighbours<Backward>(spans, spanIndex, y, in span);
                    break;
                default:
                    Debug.Assert(false, "Impossible state");
                    goto case CompactOpenHeightField.HeightSpan.LEFT_INDEX;
            }
        }

        private struct Left { }
        private struct Right { }
        private struct Forward { }
        private struct Backward { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetIndexOfSideCheckNeighbours<T>(ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, int spanIndex, int y, in CompactOpenHeightField.HeightSpan span)
        {
            if (spanIndex != CompactOpenHeightField.HeightSpan.NULL_SIDE)
            {
                ref readonly CompactOpenHeightField.HeightSpan neighbour = ref spans[spanIndex];
                y = Mathf.Max(y, neighbour.Floor);

                int index1, index2, index3 = -1;

                if (typeof(T) == typeof(Left))
                {
                    index1 = neighbour.Forward;
                    index2 = span.Forward;
                }
                else if (typeof(T) == typeof(Forward))
                {
                    index1 = neighbour.Right;
                    index2 = span.Right;
                }
                else if (typeof(T) == typeof(Right))
                {
                    index1 = neighbour.Backward;
                    index2 = span.Backward;
                }
                else if (typeof(T) == typeof(Backward))
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
                    if (typeof(T) == typeof(Left))
                        index3 = neighbour2.Left;
                    else if (typeof(T) == typeof(Forward))
                        index3 = neighbour2.Forward;
                    else if (typeof(T) == typeof(Right))
                        index3 = neighbour2.Right;
                    else if (typeof(T) == typeof(Backward))
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
            switch (direction)
            {
                case CompactOpenHeightField.HeightSpan.LEFT_INDEX:
                    pz++;
                    break;
                case CompactOpenHeightField.HeightSpan.FORWARD_INDEX:
                    px++;
                    pz++;
                    break;
                case CompactOpenHeightField.HeightSpan.RIGHT_INDEX:
                    px++;
                    break;
            }
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
        private static void NeighbourMarkFlag(ReadOnlySpan<ushort> regions, byte[] edgeFlags, int i, int neighbour, byte isRegional)
        {
            if (neighbour != CompactOpenHeightField.HeightSpan.NULL_SIDE)
            {
                ushort regionNeighbour = regions[neighbour];
                if (regionNeighbour == RegionsField.NULL_REGION)
                    edgeFlags[i] |= isRegional;
                else if (regionNeighbour != regions[i])
                {
                    ref byte edgeFlag = ref edgeFlags[i];
                    edgeFlag |= isRegional;
                }
            }
            else
                edgeFlags[i] |= isRegional;
        }

        public void DrawGizmos(in Resolution resolution, in CompactOpenHeightField openHeightField, in RegionsField regionsField)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(resolution.Center, new Vector3(resolution.CellSize.x * resolution.Width, resolution.CellSize.y * resolution.Height, resolution.CellSize.z * resolution.Depth));
            Vector3 offset = new Vector3(resolution.Width * (-resolution.CellSize.x), resolution.Height * (-resolution.CellSize.y), resolution.Depth * (-resolution.CellSize).z) * .5f;// + (resolution.CellSize * .5f);
            offset.y -= resolution.CellSize.y / 2;
            offset += resolution.Center;

            RawPooledList<RawPooledList<(int x, int z, int y)>> contours = this.contours;
            for (int i = 0; i < contours.Count; i++)
            {
               // https://gamedev.stackexchange.com/a/46469/99234 from https://gamedev.stackexchange.com/questions/46463/how-can-i-find-an-optimum-set-of-colors-for-10-players
                const float goldenRatio = 1.61803398874989484820458683436f; // (1 + Math.Sqrt(5)) / 2
                const float div = 1 / goldenRatio;
                Gizmos.color = Color.HSVToRGB(i * div % 1f, .5f, Mathf.Sqrt(1 - (i * div % .5f)));

                RawPooledList<(int x, int z, int y)> contour = contours[i];

                (int x, int z, int y) = contour[0];
                Vector2 position_ = new Vector2(x * resolution.CellSize.x, z * resolution.CellSize.z);
                Vector3 position = new Vector3(position_.x, resolution.CellSize.y * y, position_.y);
                Vector3 center_ = offset + position;
                Vector3 center_1 = center_;
                Vector3 center_2 = center_;
                for (int j = 1; j < contour.Count; j++)
                {
                    (x, z, y) = contour[j];
                    position_ = new Vector2(x * resolution.CellSize.x, z * resolution.CellSize.z);
                    position = new Vector3(position_.x, resolution.CellSize.y * y, position_.y);
                    center_2 = offset + position;
                    Gizmos.DrawLine(center_, center_2);
                    center_ = center_2;
                }
                Gizmos.DrawLine(center_2, center_1);
            }
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose() => contours.Dispose();
    }
}