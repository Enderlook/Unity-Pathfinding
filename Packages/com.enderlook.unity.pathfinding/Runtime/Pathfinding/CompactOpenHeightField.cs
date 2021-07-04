using Enderlook.Collections.Pooled.LowLevel;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    /// <summary>
    /// Represent an open height field.
    /// </summary>
    internal readonly struct CompactOpenHeightField : IDisposable
    {
        private readonly HeightColumn[] columns;
        private readonly int columnsCount;
        private readonly HeightSpan[] spans;
        private readonly int spansCount;

        public ReadOnlySpan<HeightColumn> Columns => columns.AsSpan(0, columnsCount);
        public ReadOnlySpan<HeightSpan> Spans => spans.AsSpan(0, spansCount);

        /// <summary>
        /// Creates a the open height field of a height field.
        /// </summary>
        /// <param name="heightField">Height field used to create open height field.</param>
        /// <param name="resolution">Resolution of <paramref name="heightField"/></param>
        /// <param name="maxTraversableStep">Maximum amount of cells between two floors to be considered neighbours.</param>
        /// <param name="minTraversableHeight">Minimum height between a floor and a ceil to be considered traversable.</param>
        /// <returns>The open height field of the heigh field.</returns>
        public CompactOpenHeightField(in HeightField heightField, in Resolution resolution, int maxTraversableStep, int minTraversableHeight)
        {
            spans = null;
            spansCount = 0;

            columnsCount = resolution.Width * resolution.Depth;
            columns = ArrayPool<HeightColumn>.Shared.Rent(resolution.Width * resolution.Depth);
            try
            {
                RawPooledList<HeightSpan> spanBuilder = RawPooledList<HeightSpan>.Create();
                try
                {
                    Initialize(heightField, resolution, ref spanBuilder);

                    spans = spanBuilder.UnderlyingArray;
                    spansCount = spanBuilder.Count;

                    CalculateNeighbours(resolution, maxTraversableStep, minTraversableHeight);
                }
                catch
                {
                    spanBuilder.Dispose();
                    throw;
                }
            }
            catch
            {
                ArrayPool<HeightColumn>.Shared.Return(columns);
                throw;
            }
        }

        private void Initialize(in HeightField heightField, in Resolution resolution, ref RawPooledList<HeightSpan> spanBuilder)
        {
            ReadOnlySpan<HeightField.HeightColumn> columns = heightField.AsSpan();
            int index = 0;
            for (int x = 0; x < resolution.Width; x++)
            {
                for (int z = 0; z < resolution.Depth; z++)
                {
                    Debug.Assert(index == resolution.GetIndex(x, z));
                    int startIndex = spanBuilder.Count;

                    HeightField.HeightColumn column = columns[index];
                    ReadOnlySpan<HeightField.HeightSpan> spans = column.AsSpan();
                    Debug.Assert(spans.Length > 0);

                    int i = 0;
                    int y = 0;

                    if (spans.Length > 1)
                    {
                        HeightField.HeightSpan span = spans[i++];

#if DEBUG
                        bool wasSolid = span.IsSolid;
#endif

                        if (!span.IsSolid)
                        {
                            /* Do we actually need to add this span?
                             * If we remove it, everything works... the output is just a bit different,
                             * maybe it doesn't mater */
                            const int floor = -1;
                            int ceil = y + span.Height;
                            spanBuilder.Add(new HeightSpan(floor, ceil));

                            // Regardless we remove above span, this line must stay!
                            y += span.Height;
                        }
                        else
                        {
                            int floor = y + span.Height;
                            if (spans.Length > 2)
                            {
                                span = spans[i++];

#if DEBUG
                                Debug.Assert(wasSolid != span.IsSolid);
                                wasSolid = span.IsSolid;
#endif

                                y += span.Height;
                                int ceil = y;
                                spanBuilder.Add(new HeightSpan(floor, ceil));
                            }
                            else
                            {
                                Debug.Assert(i == spans.Length - 1);
#if DEBUG
                                span = spans[i];
                                Debug.Assert(wasSolid != span.IsSolid);
#endif
                                const int ceil = -1;
                                spanBuilder.Add(new HeightSpan(floor, ceil));

                                goto end;
                            }
                        }

                        for (; i < spans.Length - 1; i++)
                        {
                            span = spans[i];
#if DEBUG
                            Debug.Assert(wasSolid != span.IsSolid);
                            wasSolid = span.IsSolid;
#endif
                            if (!span.IsSolid)
                            {
                                int floor = y;
                                y += span.Height;
                                int ceil = y;
                                spanBuilder.Add(new HeightSpan(floor, ceil));
                            }
                            else
                                y += span.Height;
                        }

                        if (spans.Length > 2)
                        {
                            Debug.Assert(i == spans.Length - 1);
                            span = spans[i];
#if DEBUG
                            Debug.Assert(wasSolid != span.IsSolid);
#endif
                            if (!span.IsSolid)
                            {
                                int floor = y;
                                const int ceil = -1;
                                spanBuilder.Add(new HeightSpan(floor, ceil));
                            }
                        }

                        end:;
                    }

                    this.columns[index++] = new HeightColumn(startIndex, spanBuilder.Count);
                }
            }
        }

        private void CalculateNeighbours(in Resolution resolution, int maxTraversableStep, int minTraversableHeight)
        {
            int xM = resolution.Width - 1;
            int zM = resolution.Depth - 1;

            int index = 0;

            int x = 0;
            {
                int z = 0;
                CalculateNeighboursBody<RightForward>(resolution, maxTraversableStep, minTraversableHeight, ref index, x, z);
                for (z++; z < zM; z++)
                    CalculateNeighboursBody<RightForwardBackward>(resolution, maxTraversableStep, minTraversableHeight, ref index, x, z);
                Debug.Assert(z == zM);
                CalculateNeighboursBody<RightBackwardIncrement>(resolution, maxTraversableStep, minTraversableHeight, ref index, x, zM);
            }

            for (x++; x < xM; x++)
            {
                int z = 0;
                CalculateNeighboursBody<LeftRightForward>(resolution, maxTraversableStep, minTraversableHeight, ref index, x, z);
                for (z = 1; z < zM; z++)
                {
                    /* This is the true body of this function.
                     * All methods that starts with CalculateNeighboursBody() are actually specializations of this body to avoid branching inside the loop.
                     * TODO: Does this actually improves perfomance? */
                    CalculateNeighboursBody<LeftRightForwardBackward>(resolution, maxTraversableStep, minTraversableHeight, ref index, x, z);
                }

                Debug.Assert(z == zM);
                CalculateNeighboursBody<LeftRightBackwardIncrement>(resolution, maxTraversableStep, minTraversableHeight, ref index, x, zM);
            }

            Debug.Assert(x == xM);
            {
                int z = 0;
                CalculateNeighboursBody<LeftForward>(resolution, maxTraversableStep, minTraversableHeight, ref index, x, z);
                for (z++; z < zM; z++)
                    CalculateNeighboursBody<LeftForwardBackward>(resolution, maxTraversableStep, minTraversableHeight, ref index, x, z);
                Debug.Assert(z == zM);
                CalculateNeighboursBody<LeftBackward>(resolution, maxTraversableStep, minTraversableHeight, ref index, x, z);
            }
        }

        private struct LeftRightForwardBackward { }
        private struct LeftRightForward { }
        private struct LeftRightBackwardIncrement { }
        private struct LeftForwardBackward { }
        private struct LeftForward { }
        private struct LeftBackward { }
        private struct RightForward { }
        private struct RightForwardBackward { }
        private struct RightBackwardIncrement { }

        private void CalculateNeighboursBody<T>(in Resolution resolution, int maxTraversableStep, int minTraversableHeight, ref int index, int x, int z)
        {
            Debug.Assert(
                typeof(T) == typeof(LeftRightForwardBackward) ||
                typeof(T) == typeof(LeftRightForward) ||
                typeof(T) == typeof(LeftRightBackwardIncrement) ||
                typeof(T) == typeof(LeftForwardBackward) ||
                typeof(T) == typeof(LeftForward) ||
                typeof(T) == typeof(LeftBackward) ||
                typeof(T) == typeof(RightForward) ||
                typeof(T) == typeof(RightForwardBackward) ||
                typeof(T) == typeof(RightBackwardIncrement)
            );
            Debug.Assert(index == resolution.GetIndex(x, z));

            HeightColumn column = columns[index];

            HeightColumn left, right, backward, forward;

            if (typeof(T) == typeof(LeftRightForwardBackward) ||
                typeof(T) == typeof(LeftRightForward) ||
                typeof(T) == typeof(LeftRightBackwardIncrement) ||
                typeof(T) == typeof(LeftForwardBackward) ||
                typeof(T) == typeof(LeftForward) ||
                typeof(T) == typeof(LeftBackward))
            {
                Debug.Assert(index - resolution.Depth == resolution.GetIndex(x - 1, z));
                left = columns[index - resolution.Depth];
            }
            else
                left = default;

            if (typeof(T) == typeof(LeftRightForwardBackward) ||
                typeof(T) == typeof(LeftRightForward) ||
                typeof(T) == typeof(LeftRightBackwardIncrement) ||
                typeof(T) == typeof(RightForwardBackward) ||
                typeof(T) == typeof(RightForward) ||
                typeof(T) == typeof(RightBackwardIncrement))
            {
                Debug.Assert(index + resolution.Depth == resolution.GetIndex(x + 1, z));
                right = columns[index + resolution.Depth];
            }
            else
                right = default;

            if (typeof(T) == typeof(LeftRightForwardBackward) ||
                typeof(T) == typeof(LeftRightBackwardIncrement) ||
                typeof(T) == typeof(RightForwardBackward) ||
                typeof(T) == typeof(LeftForwardBackward) ||
                typeof(T) == typeof(RightBackwardIncrement) ||
                typeof(T) == typeof(LeftBackward))
            {
                Debug.Assert(index - 1 == resolution.GetIndex(x, z - 1));
                backward = columns[index - 1];
            }
            else
                backward = default;

            if (typeof(T) == typeof(LeftRightForwardBackward) ||
                typeof(T) == typeof(LeftRightForward) ||
                typeof(T) == typeof(RightForwardBackward) ||
                typeof(T) == typeof(LeftForwardBackward) ||
                typeof(T) == typeof(RightForward) ||
                typeof(T) == typeof(LeftForward))
            {
                Debug.Assert(index + 1 == resolution.GetIndex(x, z + 1));
                forward = columns[++index];
            }
            else
                forward = default;

            if (typeof(T) == typeof(RightBackwardIncrement) ||
                typeof(T) == typeof(LeftRightBackwardIncrement))
                index++;

            for (int i = column.First; i < column.Last; i++)
            {
                // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                // Hack: HeightSpan is immutable for the outside, however this function must initialize (mutate) the struct.
                ref HeightSpanBuilder span = ref Unsafe.As<HeightSpan, HeightSpanBuilder>(ref spans[i]);

                if (typeof(T) == typeof(LeftRightForwardBackward) ||
                    typeof(T) == typeof(LeftRightForward) ||
                    typeof(T) == typeof(LeftRightBackwardIncrement) ||
                    typeof(T) == typeof(LeftForwardBackward) ||
                    typeof(T) == typeof(LeftForward) ||
                    typeof(T) == typeof(LeftBackward))
                    CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, left, ref span, ref span.Left);

                if (typeof(T) == typeof(LeftRightForwardBackward) ||
                    typeof(T) == typeof(LeftRightForward) ||
                    typeof(T) == typeof(LeftRightBackwardIncrement) ||
                    typeof(T) == typeof(RightForwardBackward) ||
                    typeof(T) == typeof(RightForward) ||
                    typeof(T) == typeof(RightBackwardIncrement))
                    CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, right, ref span, ref span.Right);

                if (typeof(T) == typeof(LeftRightForwardBackward) ||
                    typeof(T) == typeof(LeftRightForward) ||
                    typeof(T) == typeof(RightForwardBackward) ||
                    typeof(T) == typeof(LeftForwardBackward) ||
                    typeof(T) == typeof(RightForward) ||
                    typeof(T) == typeof(LeftForward))
                    CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, forward, ref span, ref span.Forward);

                if (typeof(T) == typeof(LeftRightForwardBackward) ||
                    typeof(T) == typeof(LeftRightBackwardIncrement) ||
                    typeof(T) == typeof(RightForwardBackward) ||
                    typeof(T) == typeof(LeftForwardBackward) ||
                    typeof(T) == typeof(RightBackwardIncrement) ||
                    typeof(T) == typeof(LeftBackward))
                    CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, backward, ref span, ref span.Backward);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CalculateNeighboursLoop(int maxTraversableStep, int minTraversableHeight, HeightColumn column, ref HeightSpanBuilder span, ref int side)
        {
            for (int j = column.First, end = column.Last; j < end; j++)
                if (span.PresentNeighbour(ref side, j, spans[j], maxTraversableStep, minTraversableHeight))
                    break;
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            ArrayPool<HeightColumn>.Shared.Return(columns);
            ArrayPool<HeightSpan>.Shared.Return(spans);
        }

        public void DrawGizmos(in Resolution resolution, bool surfaces, bool neightbours)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(resolution.Center, new Vector3(resolution.CellSize.x * resolution.Width, resolution.CellSize.y * resolution.Height, resolution.CellSize.z * resolution.Depth));
            Vector3 offset = (new Vector3(resolution.Width * (-resolution.CellSize.x), resolution.Height * (-resolution.CellSize.y), resolution.Depth * (-resolution.CellSize).z) * .5f) + (resolution.CellSize * .5f);
            offset.y -= resolution.CellSize.y / 2;
            offset += resolution.Center;

            int i = 0;
            for (int x = 0; x < resolution.Width; x++)
            {
                for (int z = 0; z < resolution.Depth; z++)
                {
                    Vector2 position_ = new Vector2(x * resolution.CellSize.x, z * resolution.CellSize.z);
                    int i_ = i;

                    ref HeightColumn column = ref columns[i++];

                    if (!column.IsEmpty)
                    {
                        int j = column.First;

                        HeightSpan heightSpan = spans[j++];
                        if (heightSpan.Floor != HeightSpan.NULL_SIDE)
                            Draw(resolution, heightSpan.Floor - .1f, Color.green);
                        Draw(resolution, heightSpan.Ceil + .1f, Color.red);
                        Draw2(resolution, spans, heightSpan, i_);

                        for (; j < column.Last - 1; j++)
                        {
                            heightSpan = spans[j];
                            Draw(resolution, heightSpan.Floor - .1f, Color.green);
                            Draw(resolution, heightSpan.Ceil + .1f, Color.red);
                            Draw2(resolution, spans, heightSpan, i_);
                        }

                        if (column.Count > 1) // Shouldn't this be 2?
                        {
                            Debug.Assert(j == column.Last - 1);
                            heightSpan = spans[j];
                            Draw(resolution, heightSpan.Floor, Color.green);
                            if (heightSpan.Ceil != HeightSpan.NULL_SIDE)
                                Draw(resolution, heightSpan.Ceil + .1f, Color.red);
                            Draw2(resolution, spans, heightSpan, i_);
                        }
                    }

                    void Draw(in Resolution resolution_, float y, Color color)
                    {
                        if (!surfaces)
                            return;
                        Gizmos.color = color;
                        Vector3 position = new Vector3(position_.x, resolution_.CellSize.y * y, position_.y);
                        Vector3 center_ = offset + position;
                        Vector3 size = new Vector3(resolution_.CellSize.x, resolution_.CellSize.y * .1f, resolution_.CellSize.z);
                        Gizmos.DrawCube(center_, size);
                    }

                    void Draw2(in Resolution resolution_, HeightSpan[] spans, HeightSpan span, int index)
                    {
                        if (!neightbours)
                            return;
                        Gizmos.color = Color.yellow;
                        unsafe
                        {
                            if (span.Left != HeightSpan.NULL_SIDE)
                            {
                                Debug.Assert(index - resolution_.Depth == resolution_.GetIndex(x - 1, z));
                                HeightSpan span_ = spans[span.Left];
                                Draw3(resolution_, span_.Floor, span.Floor, HeightSpan.NULL_SIDE, 0);
                            }

                            if (span.Right != HeightSpan.NULL_SIDE)
                            {
                                Debug.Assert(index + resolution_.Depth == resolution_.GetIndex(x + 1, z));
                                HeightSpan span_ = spans[span.Right];
                                Draw3(resolution_, span_.Floor, span.Floor, 1, 0);
                            }

                            if (span.Backward != HeightSpan.NULL_SIDE)
                            {
                                Debug.Assert(index - 1 == resolution_.GetIndex(x, z - 1));
                                HeightSpan span_ = spans[span.Backward];
                                Draw3(resolution_, span_.Floor, span.Floor, 0, HeightSpan.NULL_SIDE);
                            }

                            if (span.Forward != HeightSpan.NULL_SIDE)
                            {
                                Debug.Assert(index + 1 == resolution_.GetIndex(x, z + 1));
                                HeightSpan span_ = spans[span.Forward];
                                Draw3(resolution_, span_.Floor, span.Floor, 0, 1);
                            }
                        }

                        void Draw3(in Resolution resolution__, int yTo, float yFrom, int x_, int z_)
                        {
                            Vector3 positionFrom = new Vector3(position_.x, resolution__.CellSize.y * yFrom, position_.y);
                            Vector3 centerFrom = offset + positionFrom;

                            Vector3 position__ = new Vector2((x + x_) * resolution__.CellSize.x, (z + z_) * resolution__.CellSize.z);
                            Vector3 positionTo = new Vector3(position__.x, resolution__.CellSize.y * yTo, position__.y);
                            Vector3 centerTo = offset + positionTo;

                            Gizmos.DrawLine(centerFrom, centerTo);
                        }
                    }
                }
            }
        }

        internal readonly struct HeightColumn
        {
            public readonly int First;
            public readonly int Last;
            public bool IsEmpty => First == Last;

            public int Count => Last - First;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeightColumn(int first, int last)
            {
                First = first;
                Last = last;
            }
        }

        internal readonly struct HeightSpan
        {
            // Value of Floor, Ceil, Left, Forward, Right, Backward when is null.
            public const int NULL_SIDE = -1;
            public const int LEFT_INDEX = 0;
            public const int FORWARD_INDEX = 1;
            public const int RIGHT_INDEX = 2;
            public const int BACKWARD_INDEX = 3;

            public readonly int Floor;
            public readonly int Ceil;

            public readonly int Left;
            public readonly int Forward;
            public readonly int Right;
            public readonly int Backward;

            public struct LeftSide { }
            public struct ForwardSide { }
            public struct RightSide { }
            public struct BackwardSide { }

            public bool IsBorder {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get {
                    // A border is any span with less than 4 neighbours.
                    Debug.Assert(NULL_SIDE == -1, "If this fail, you must change the next line to perform 4 comparisons instead, since we can no longer rely in our current trick.");
                    bool isBorder = (Left | Forward | Right | Backward) == NULL_SIDE;
                    Debug.Assert(isBorder == (Left == NULL_SIDE || Forward == NULL_SIDE || Right == NULL_SIDE || Backward == NULL_SIDE));
                    return isBorder;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetSide(int indexIndex)
            {
                Debug.Assert(indexIndex >= 0 && indexIndex < 4);
                return Unsafe.Add(ref Unsafe.AsRef(Left), indexIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetSide<T>()
            {
                if (typeof(T) == typeof(LeftSide))
                    return Left;
                else if (typeof(T) == typeof(RightSide))
                    return Right;
                else if (typeof(T) == typeof(ForwardSide))
                    return Forward;
                else if (typeof(T) == typeof(BackwardSide))
                    return Backward;
                else
                {
                    Debug.Assert(false);
                    return 0;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeightSpan(int floor, int ceil)
            {
                Floor = floor;
                Ceil = ceil;
                Left = NULL_SIDE;
                Forward = NULL_SIDE;
                Right = NULL_SIDE;
                Backward = NULL_SIDE;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int RotateClockwise(int side) => (side + 1) & 0x3;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int RotateCounterClockwise(int side) => (side + 3) & 0x3;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetSideRotatedClockwise<T>()
            {
                if (typeof(T) == typeof(LeftSide))
                    return Right;
                else if (typeof(T) == typeof(RightSide))
                    return Forward;
                else if (typeof(T) == typeof(ForwardSide))
                    return Backward;
                else if (typeof(T) == typeof(BackwardSide))
                    return Left;
                else
                {
                    Debug.Assert(false);
                    return 0;
                }
            }
        }

        private struct HeightSpanBuilder
        {
            // Must have same layout as HeightSpan.

            public int Floor;
            public int Ceil;

            public int Left;
            public int Forward;
            public int Right;
            public int Backward;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool PresentNeighbour(ref int side, int neighbourIndex, HeightSpan neighbourSpan, int maxTraversableStep, int minTraversableHeight)
            {
                if (Floor == HeightSpan.NULL_SIDE || neighbourSpan.Floor == HeightSpan.NULL_SIDE)
                    return false;

                if (Math.Abs(Floor - neighbourSpan.Floor) <= maxTraversableStep)
                {
                    if (Ceil == HeightSpan.NULL_SIDE || neighbourSpan.Ceil == HeightSpan.NULL_SIDE || Math.Min(Ceil, neighbourSpan.Ceil) - Math.Max(Floor, neighbourSpan.Floor) >= minTraversableHeight)
                    {
                        Debug.Assert(side == HeightSpan.NULL_SIDE);
                        side = neighbourIndex;
                        return true;
                    }
                }
                return false;
            }
        }
    }
}