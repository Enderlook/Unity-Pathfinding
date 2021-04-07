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

            for (int i = 0; i < spans.Length; i++)
            {
                byte flags = edgeFlags[i];
                if ((flags & (LEFT_IS_REGIONAL | FORWARD_IS_REGIONAL | RIGHT_IS_REGIONAL | BACKWARD_IS_REGIONAL)) == 0)
                    continue;
                RawPooledList<(int span, int neighbour)> contour = WalkContour(resolution, regions, spans, edgeFlags, i, flags);
            }
        }

        private RawPooledList<(int span, int neighbour)> WalkContour(in Resolution resolution, ReadOnlySpan<ushort> regions, ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, byte[] edgeFlags, int i, byte flags)
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

            RawPooledList<(int span, int neighbour)> contour = RawPooledList<(int span, int neighbour)>.Create();
            contour.Add((i, direction));

            int startSpan = i;
            int startDirection = direction;

            while (true)
            {
                flags = edgeFlags[i];
                direction = RotateClockwise(direction);
                if (CheckFlag(flags, ToFlag(direction)))
                {
                    contour.Add((i, direction));

                    direction = RotateClockwise(direction);
                    if (CheckFlag(flags, ToFlag(direction)))
                    {
                        contour.Add((i, direction));

                        direction = RotateClockwise(direction);
                        if (CheckFlag(flags, ToFlag(direction)))
                        {
                            contour.Add((i, direction));

                            direction = RotateClockwise(direction);
                            if (CheckFlag(flags, ToFlag(direction)))
                            {
                                contour.Add((i, direction));
                            }
                        }
                    }
                }

                i = spans[i].GetIndexOfSide(i, direction, resolution.Depth);
                direction = RotateCounterClockwise(direction);

                if (i == startSpan && direction == startDirection)
                    break;
            }

            return contour;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CheckFlag(byte value, byte flag) => (value & flag) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte ToFlag(int direction)
        {
            Debug.Assert(direction < 3);
            return (byte)(1 << (direction + 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int RotateClockwise(int direction) => (direction + 1) & 0x3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int RotateCounterClockwise(int direction) => (direction + 3) & 0x3;

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