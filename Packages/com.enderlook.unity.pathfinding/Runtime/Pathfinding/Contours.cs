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
            ReadOnlySpan<ushort> regions = regionsField.Regions;
            ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans = openHeightField.Spans;
            ReadOnlySpan<CompactOpenHeightField.HeightColumn> columns = openHeightField.Columns;
            Debug.Assert(regions.Length == spans.Length);

            byte[] edgeFlags = ArrayPool<byte>.Shared.Rent(spans.Length);
            for (int i = 0; i < spans.Length; i++)
            {
                ref readonly CompactOpenHeightField.HeightSpan span = ref spans[i];

                NeighbourMarkFlag(regions, edgeFlags, i, span.Left, LEFT_IS_REGIONAL);
                NeighbourMarkFlag(regions, edgeFlags, i, span.Forward, FORWARD_IS_REGIONAL);
                NeighbourMarkFlag(regions, edgeFlags, i, span.Right, RIGHT_IS_REGIONAL);
                NeighbourMarkFlag(regions, edgeFlags, i, span.Backward, BACKWARD_IS_REGIONAL);
            }

            RawPooledList<(int x, int z, int neighbour)> edgeContour = RawPooledList<(int x, int z, int neighbour)>.Create();
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
                            continue;

                        WalkContour(resolution, spans, edgeFlags, ref edgeContour, x, i, z, spanIndex, flags);

                        spanIndex++;
                    }
                }
            }
        }

        public void DrawGizmos(Resolution r, CompactOpenHeightField openHeighField)
        {

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

        private void WalkContour(in Resolution resolution, ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, byte[] edgeFlags, ref RawPooledList<(int x, int z, int neighbour)> edgeContour, int x, int i, int z, int spanIndex, byte flags)
        {
            // Choose first edge.
            int direction;
            if (CheckFlag(flags, LEFT_IS_REGIONAL))
                direction = CompactOpenHeightField.HeightSpan.LEFT_INDEX;
            else if (CheckFlag(flags, FORWARD_IS_REGIONAL))
                direction = CompactOpenHeightField.HeightSpan.FORWARD_INDEX;
            else if (CheckFlag(flags, RIGHT_IS_REGIONAL))
                direction = CompactOpenHeightField.HeightSpan.RIGHT_INDEX;
            else if (CheckFlag(flags, BACKWARD_IS_REGIONAL))
                direction = CompactOpenHeightField.HeightSpan.BACKWARD_INDEX;
            else
            {
                Debug.Assert(false, "Impossible state.");
                direction = 0;
            }

            edgeContour.Clear();
            GetPoints(x, z, direction, out int px, out int pz);
            edgeContour.Add((px, pz, direction));

            int startSpan = spanIndex;
            int startDirection = direction;

            do
            {
                flags = edgeFlags[spanIndex];
                for (int j = 0; j < 4; j++)
                {
                    direction = RotateClockwise(direction);
                    if (CheckFlag(flags, ToFlag(direction)))
                    {
                        GetPoints(x, z, direction, out px, out pz);
                        edgeContour.Add((px, pz, direction));
                    }
                    else
                        break;
                }

                spans[spanIndex].GetIndexOfSide(spans, ref spanIndex, ref x, ref z, out int y, direction);

                direction = RotateCounterClockwise(direction);
            }
            while (startSpan != spanIndex || startDirection != direction);
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
        private static bool CheckFlag(byte value, byte flag) => (value & flag) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ToFlag(int direction)
        {
            Debug.Assert(direction < 3);
            return (byte)(1 << (direction + 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int RotateClockwise(int direction) => (direction + 1) & 0x3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int RotateCounterClockwise(int direction) => (direction + 3) & 0x3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void NeighbourMarkFlag(ReadOnlySpan<ushort> regions, byte[] edgeFlags, int i, int neighbour, byte isRegional)
        {
            if (neighbour == CompactOpenHeightField.HeightSpan.NULL_SIDE)
                edgeFlags[neighbour] &= isRegional;
            else
            {
                ushort regionNeighbour = regions[neighbour];
                if (regionNeighbour == RegionsField.NULL_REGION)
                    edgeFlags[neighbour] &= isRegional;
                else if (regionNeighbour == regions[i])
                {
                    ref byte edgeFlag = ref edgeFlags[neighbour];
                    edgeFlag = (byte)(edgeFlag & (~isRegional));
                }
                else
                {
                    ref byte edgeFlag = ref edgeFlags[neighbour];
                    edgeFlag = (byte)(edgeFlag & isRegional);
                }
            }
        }
    }
}