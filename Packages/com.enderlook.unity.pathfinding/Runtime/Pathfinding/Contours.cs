using Enderlook.Collections.Pooled.LowLevel;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using Unity.Collections;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    /// <summary>
    /// Stores the contours of <see cref="RegionsField"/>.
    /// </summary>
    internal readonly struct Contours
    {
        private const byte LEFT_IS_REGIONAL = 1 << (CompactOpenHeightField.HeightSpan.LEFT_INDEX + 1);
        private const byte FORWARD_IS_REGIONAL = 1 << (CompactOpenHeightField.HeightSpan.FORWARD_INDEX + 1);
        private const byte RIGHT_IS_REGIONAL = 1 << (CompactOpenHeightField.HeightSpan.RIGHT_INDEX + 1);
        private const byte BACKWARD_IS_REGIONAL = 1 << (CompactOpenHeightField.HeightSpan.BACKWARD_INDEX + 1);

        /// <summary>
        /// Calculates the contours of the specified regions.
        /// </summary>
        /// <param name="regionsField">Regions whose contours is being calculated.</param>
        /// <param name="openHeightField">Open height field owner of the <paramref name="regionsField"/>.</param>
        /// <param name="resolution">Resolution of the <paramref name="regionsField"/>.</param>
        public Contours(in RegionsField regionsField, in CompactOpenHeightField openHeightField, in Resolution resolution)
        {
            this = default;
            ReadOnlySpan<ushort> regions = regionsField.Regions;
            ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans = openHeightField.Spans;
            ReadOnlySpan<CompactOpenHeightField.HeightColumn> columns = openHeightField.Columns;
            Debug.Assert(regions.Length == spans.Length);

            byte[] edgeFlags = CreateFlags(regions, spans);
            try
            {
                RawPooledList<(int x, int z, int y, int neighbour)> edgeContour = RawPooledList<(int x, int z, int y, int neighbour)>.Create();
                int spanIndex = 0;
                int columnIndex = 0;
                for (int x = 0; x < resolution.Width; x++)
                {
                    for (int z = 0; z < resolution.Depth; z++)
                    {
                        Debug.Assert(columnIndex == GetIndex(resolution, x, z));
                        CompactOpenHeightField.HeightColumn column = columns[columnIndex++];

                        for (int i = column.First; i < column.Last; i++)
                        {
                            byte flags = edgeFlags[spanIndex];
                            if ((flags & (LEFT_IS_REGIONAL | FORWARD_IS_REGIONAL | RIGHT_IS_REGIONAL | BACKWARD_IS_REGIONAL)) == 0)
                            {
                                spanIndex++;
                                continue;
                            }

                            WalkContour(spans, edgeFlags, ref edgeContour, x, z, spanIndex, flags);
                            spanIndex++;
                        }
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(edgeFlags);
            }
        }

        private static byte[] CreateFlags(ReadOnlySpan<ushort> regions, ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans)
        {
            byte[] edgeFlags = ArrayPool<byte>.Shared.Rent(spans.Length);
            try
            {
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

        /*[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetIndex(in Resolution resolution, int x, int y, int z)
        {
            Debug.Assert(x >= 0);
            Debug.Assert(x < resolution.Width);
            Debug.Assert(z >= 0);
            Debug.Assert(z < resolution.Depth);
            Debug.Assert(y >= 0);
            int index = (resolution.Depth * ((resolution.Height * x) + y)) + z;
            Debug.Assert(index < resolution.Width * resolution.Height * resolution.Depth);
            return index;
        }*/

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

        private void WalkContour(ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, byte[] edgeFlags, ref RawPooledList<(int x, int z, int y, int neighbour)> edgeContour, int x, int z, int spanIndex, byte flags)
        {
            // Choose first edge.
            int direction;
            if (IsRegion(flags, LEFT_IS_REGIONAL))
                direction = CompactOpenHeightField.HeightSpan.LEFT_INDEX;
            else if (IsRegion(flags, FORWARD_IS_REGIONAL))
                direction = CompactOpenHeightField.HeightSpan.FORWARD_INDEX;
            else if (IsRegion(flags, RIGHT_IS_REGIONAL))
                direction = CompactOpenHeightField.HeightSpan.RIGHT_INDEX;
            else if (IsRegion(flags, BACKWARD_IS_REGIONAL))
                direction = CompactOpenHeightField.HeightSpan.BACKWARD_INDEX;
            else
            {
                Debug.Assert(false, "Impossible state.");
                direction = 0;
            }

            edgeContour.Clear();
            int py = spans[spanIndex].Ceil;
            GetPoints(x, z, direction, out int px, out int pz);
            edgeContour.Add((px, pz, py, direction));

            int startSpan = spanIndex;
            int startDirection = direction;

            int o = 0;
            do
            {
                flags = edgeFlags[spanIndex];

                if (!IsRegion(flags, ToFlag(direction)))
                    goto end;
                GetPoints(x, z, direction, out px, out pz);
                edgeContour.Add((px, pz, py, direction));
                direction = RotateClockwise(direction);

                if (!IsRegion(flags, ToFlag(direction)))
                    goto end;
                GetPoints(x, z, direction, out px, out pz);
                edgeContour.Add((px, pz, py, direction));
                direction = RotateClockwise(direction);

                if (!IsRegion(flags, ToFlag(direction)))
                    goto end;
                GetPoints(x, z, direction, out px, out pz);
                edgeContour.Add((px, pz, py, direction));
                direction = RotateClockwise(direction);

                if (!IsRegion(flags, ToFlag(direction)))
                    goto end;
                GetPoints(x, z, direction, out px, out pz);
                edgeContour.Add((px, pz, py, direction));
                direction = RotateClockwise(direction);

                end:
                GetIndexOfSide(spans, ref spanIndex, ref x, ref z, out py, direction);

                direction = RotateCounterClockwise(direction);

                if (spanIndex == CompactOpenHeightField.HeightSpan.NULL_SIDE)
                    break;

                if (o++ > 10000)
                    break;
            }
            while (startSpan != spanIndex || startDirection != direction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetIndexOfSide(ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, ref int spanIndex, ref int x, ref int z, out int y, int direction)
        {
            ref readonly CompactOpenHeightField.HeightSpan span = ref spans[spanIndex];
            y = span.Floor;
            switch (direction)
            {
                case CompactOpenHeightField.HeightSpan.LEFT_INDEX:
                    spanIndex = span.Left;
                    x--;
                    break;
                case CompactOpenHeightField.HeightSpan.RIGHT_INDEX:
                    spanIndex = span.Right;
                    x++;
                    break;
                case CompactOpenHeightField.HeightSpan.FORWARD_INDEX:
                    spanIndex = span.Forward;
                    z++;
                    break;
                case CompactOpenHeightField.HeightSpan.BACKWARD_INDEX:
                    spanIndex = span.Backward;
                    z--;
                    break;
                default:
                    Debug.Assert(false, "Impossible state");
                    goto case CompactOpenHeightField.HeightSpan.LEFT_INDEX;
            }
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
        private static int RotateClockwise(int direction) => (direction + 1) & 0x3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int RotateCounterClockwise(int direction) => (direction + 3) & 0x3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void NeighbourMarkFlag(ReadOnlySpan<ushort> regions, byte[] edgeFlags, int i, int neighbour, byte isRegional)
        {
            if (neighbour != CompactOpenHeightField.HeightSpan.NULL_SIDE)
            {
                ushort regionNeighbour = regions[neighbour];
                if (regionNeighbour == RegionsField.NULL_REGION)
                    edgeFlags[i] |= isRegional;
                else if (regionNeighbour == regions[i])
                {
                    ref byte edgeFlag = ref edgeFlags[i];
                    edgeFlag &= (byte)~isRegional;
                    return;
                }
                else
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
            Vector3 offset = (new Vector3(resolution.Width * (-resolution.CellSize.x), resolution.Height * (-resolution.CellSize.y), resolution.Depth * (-resolution.CellSize).z) * .5f) + (resolution.CellSize * .5f);
            offset.y -= resolution.CellSize.y / 2;
            offset += resolution.Center;

            ReadOnlySpan<ushort> regions = regionsField.Regions;
            ReadOnlySpan<CompactOpenHeightField.HeightColumn> columns = openHeightField.Columns;
            ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans = openHeightField.Spans;


            /*for (int i = 0; i < a.Count; i++)
            {
               // https://gamedev.stackexchange.com/a/46469/99234 from https://gamedev.stackexchange.com/questions/46463/how-can-i-find-an-optimum-set-of-colors-for-10-players
                const float goldenRatio = 1.61803398874989484820458683436f; // (1 + Math.Sqrt(5)) / 2
                const float div = 1 / goldenRatio;
                Gizmos.color = Color.HSVToRGB(i * div % 1f, .5f, Mathf.Sqrt(1 - (i * div % .5f)));

                var c = a[i];

                foreach ((int x, int z, int y, int neighbour) in c)
                {
                    Vector2 position_ = new Vector2(x * resolution.CellSize.x, z * resolution.CellSize.z);
                    Draw(resolution, y);

                    void Draw(in Resolution resolution_, float y_)
                    {
                        Vector3 position = new Vector3(position_.x, resolution_.CellSize.y * y_, position_.y);
                        Vector3 center_ = offset + position;
                        Vector3 size = new Vector3(resolution_.CellSize.x, resolution_.CellSize.y * .1f, resolution_.CellSize.z);
                        Gizmos.DrawCube(center_, size);
                    }
                }
            }*/
        }
    }
}