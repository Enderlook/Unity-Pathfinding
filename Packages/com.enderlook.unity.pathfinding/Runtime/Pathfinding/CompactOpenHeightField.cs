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
                    Debug.Assert(index == GetIndex(resolution, x, z));
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

                        end:
                        ;
                    }

                    this.columns[index++] = new HeightColumn(startIndex, spanBuilder.Count);
                }
            }
        }

        private void CalculateNeighbours(in Resolution resolution, int maxTraversableStep, int minTraversableHeight)
        {
            int xM = resolution.Width - 1;
            int zM = resolution.Depth - 1;

            int index = CalculateNeighboursWhenXIs0(resolution, maxTraversableStep, minTraversableHeight);
            int x;
            for (x = 1; x < xM; x++)
            {
                index = CalculateNeighboursWhenZIs0(resolution, maxTraversableStep, minTraversableHeight, index, x);

                int z;
                for (z = 1; z < zM; z++)
                {
                    /* This is the true body of this function.
                     * All methods that starts with When...() are actually specializations of this body to avoid branching inside the loop.
                     * TODO: Does that actually improves perfomance? */

                    Debug.Assert(index == GetIndex(resolution, x, z));

                    HeightColumn column = columns[index];

                    Debug.Assert(index - resolution.Depth == GetIndex(resolution, x - 1, z));
                    HeightColumn left = columns[index - resolution.Depth];
                    Debug.Assert(index + resolution.Depth == GetIndex(resolution, x + 1, z));
                    HeightColumn right = columns[index + resolution.Depth];
                    Debug.Assert(index - 1 == GetIndex(resolution, x, z - 1));
                    HeightColumn backward = columns[index - 1];
                    Debug.Assert(index + 1 == GetIndex(resolution, x, z + 1));
                    HeightColumn foward = columns[++index];

                    for (int i = column.First; i < column.Last; i++)
                    {
                        // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                        // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                        ref HeightSpan span = ref spans[i];

                        CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, left, ref span, ref span.Left);
                        CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, right, ref span, ref span.Right);
                        CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, foward, ref span, ref span.Foward);
                        CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, backward, ref span, ref span.Backward);
                    }
                }

                Debug.Assert(z == zM);
                Debug.Assert(z == resolution.Depth - 1);
                index = CalculateNeighboursWhenZIsZM(resolution, maxTraversableStep, minTraversableHeight, index, x, zM);
            }

            Debug.Assert(x == xM);
            Debug.Assert(x == resolution.Width - 1);
            CalculateNeighboursWhenXIsXM(resolution, maxTraversableStep, minTraversableHeight, index, x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CalculateNeighboursLoop(int maxTraversableStep, int minTraversableHeight, HeightColumn column, ref HeightSpan span, ref int side)
        {
            for (int j = column.First, end = column.Last; j < end; j++)
                if (span.PresentNeighbour(ref side, j, spans[j], maxTraversableStep, minTraversableHeight))
                    break;
        }

        private int CalculateNeighboursWhenXIs0(in Resolution resolution, int maxTraversableStep, int minTraversableHeight)
        {
            int index = 0;
            const int x = 0;
            int zM = resolution.Depth - 1;

            index = CalculateNeighboursWhenXIs0AndZIs0(resolution, maxTraversableStep, minTraversableHeight, index);

            int z;
            for (z = 1; z < zM; z++)
            {
                Debug.Assert(index == GetIndex(resolution, x, z));

                HeightColumn column = columns[index];

                Debug.Assert(index + resolution.Depth == GetIndex(resolution, x + 1, z));
                HeightColumn right = columns[index + resolution.Depth];
                Debug.Assert(index - 1 == GetIndex(resolution, x, z - 1));
                HeightColumn backward = columns[index - 1];
                Debug.Assert(index + 1 == GetIndex(resolution, x, z + 1));
                HeightColumn foward = columns[++index];

                for (int i = column.First, end = column.Last; i < end; i++)
                {
                    // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                    // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                    ref HeightSpan span = ref spans[i];

                    CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, right, ref span, ref span.Right);
                    CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, foward, ref span, ref span.Foward);
                    CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, backward, ref span, ref span.Backward);
                }
            }

            Debug.Assert(z == zM);
            Debug.Assert(z == resolution.Depth - 1);
            index = CalculateNeighboursWhenXIs0AndZIsZM(resolution, maxTraversableStep, minTraversableHeight, index);

            return index;
        }

        private int CalculateNeighboursWhenXIs0AndZIs0(in Resolution resolution, int maxTraversableStep, int minTraversableHeight, int index)
        {
            const int x = 0;
            const int z = 0;

            Debug.Assert(index == GetIndex(resolution, x, z));

            HeightColumn column = columns[index];

            Debug.Assert(index + resolution.Depth == GetIndex(resolution, x + 1, z));
            HeightColumn right = columns[index + resolution.Depth];
            Debug.Assert(index + 1 == GetIndex(resolution, x, z + 1));
            HeightColumn foward = columns[++index];

            for (int i = column.First, end = column.Last; i < end; i++)
            {
                // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                ref HeightSpan span = ref spans[i];

                CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, right, ref span, ref span.Right);
                CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, foward, ref span, ref span.Foward);
            }

            return index;
        }

        private int CalculateNeighboursWhenXIs0AndZIsZM(in Resolution resolution, int maxTraversableStep, int minTraversableHeight, int index)
        {
            const int x = 0;
            int z = resolution.Depth - 1;
            Debug.Assert(index == GetIndex(resolution, x, z));

            HeightColumn column = columns[index];

            Debug.Assert(index + resolution.Depth == GetIndex(resolution, x + 1, z));
            HeightColumn right = columns[index + resolution.Depth];
            Debug.Assert(index - 1 == GetIndex(resolution, x, z - 1));
            HeightColumn backward = columns[index - 1];
            index++;

            for (int i = column.First, end = column.Last; i < end; i++)
            {
                // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                ref HeightSpan span = ref spans[i];

                CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, right, ref span, ref span.Right);
                CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, backward, ref span, ref span.Backward);
            }

            return index;
        }

        private int CalculateNeighboursWhenZIs0(in Resolution resolution, int maxTraversableStep, int minTraversableHeight, int index, int x)
        {
            const int z = 0;

            Debug.Assert(index == GetIndex(resolution, x, z));

            HeightColumn column = columns[index];

            Debug.Assert(index - resolution.Depth == GetIndex(resolution, x - 1, z));
            HeightColumn left = columns[index - resolution.Depth];
            Debug.Assert(index + resolution.Depth == GetIndex(resolution, x + 1, z));
            HeightColumn right = columns[index + resolution.Depth];
            Debug.Assert(index + 1 == GetIndex(resolution, x, z + 1));
            HeightColumn foward = columns[++index];

            for (int i = column.First, end = column.Last; i < end; i++)
            {
                // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                ref HeightSpan span = ref spans[i];

                CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, left, ref span, ref span.Left);
                CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, right, ref span, ref span.Right);
                CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, foward, ref span, ref span.Foward);
            }

            return index;
        }

        private int CalculateNeighboursWhenZIsZM(in Resolution resolution, int maxTraversableStep, int minTraversableHeight, int index, int x, int z)
        {
            Debug.Assert(z == resolution.Depth - 1);
            Debug.Assert(index == GetIndex(resolution, x, z));

            HeightColumn column = columns[index];

            Debug.Assert(index - resolution.Depth == GetIndex(resolution, x - 1, z));
            HeightColumn left = columns[index - resolution.Depth];
            Debug.Assert(index + resolution.Depth == GetIndex(resolution, x + 1, z));
            HeightColumn right = columns[index + resolution.Depth];
            Debug.Assert(index - 1 == GetIndex(resolution, x, z - 1));
            HeightColumn backward = columns[index - 1];
            index++;

            for (int i = column.First, end = column.Last; i < end; i++)
            {
                // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                ref HeightSpan span = ref spans[i];

                CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, left, ref span, ref span.Left);
                CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, right, ref span, ref span.Right);
                CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, backward, ref span, ref span.Backward);
            }

            return index;
        }

        private void CalculateNeighboursWhenXIsXM(in Resolution resolution, int maxTraversableStep, int minTraversableHeight, int index, int x)
        {
            Debug.Assert(x == resolution.Width - 1);
            int zM = resolution.Depth - 1;

            index = CalculateNeighboursWhenXIsXMAndZIs0(resolution, maxTraversableStep, minTraversableHeight, index, x);

            int z;
            for (z = 1; z < zM; z++)
            {
                Debug.Assert(index == GetIndex(resolution, x, z));

                HeightColumn column = columns[index];

                Debug.Assert(index - resolution.Depth == GetIndex(resolution, x - 1, z));
                HeightColumn left = columns[index - resolution.Depth];
                Debug.Assert(index - 1 == GetIndex(resolution, x, z - 1));
                HeightColumn backward = columns[index - 1];
                Debug.Assert(index + 1 == GetIndex(resolution, x, z + 1));
                HeightColumn foward = columns[++index];

                for (int i = column.First, end = column.Last; i < end; i++)
                {
                    // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                    // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                    ref HeightSpan span = ref spans[i];

                    CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, left, ref span, ref span.Left);
                    CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, foward, ref span, ref span.Foward);
                    CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, backward, ref span, ref span.Backward);
                }
            }

            Debug.Assert(z == zM);
            Debug.Assert(z == resolution.Depth - 1);
            CalculateNeighboursWhenXIsXMAndZIsZM(resolution, maxTraversableStep, minTraversableHeight, index, x, z);
        }

        private int CalculateNeighboursWhenXIsXMAndZIs0(in Resolution resolution, int maxTraversableStep, int minTraversableHeight, int index, int x)
        {
            Debug.Assert(x == resolution.Width - 1);
            const int z = 0;

            Debug.Assert(index == GetIndex(resolution, x, z));

            HeightColumn column = columns[index];

            Debug.Assert(index - resolution.Depth == GetIndex(resolution, x - 1, z));
            HeightColumn left = columns[index - resolution.Depth];
            Debug.Assert(index + 1 == GetIndex(resolution, x, z + 1));
            HeightColumn foward = columns[++index];

            for (int i = column.First, end = column.Last; i < end; i++)
            {
                // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                ref HeightSpan span = ref spans[i];

                CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, left, ref span, ref span.Left);
                CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, foward, ref span, ref span.Foward);
            }

            return index;
        }

        private void CalculateNeighboursWhenXIsXMAndZIsZM(in Resolution resolution, int maxTraversableStep, int minTraversableHeight, int index, int x, int z)
        {
            Debug.Assert(x == resolution.Width - 1);
            Debug.Assert(z == resolution.Depth - 1);
            Debug.Assert(index == GetIndex(resolution, x, z));

            HeightColumn column = columns[index];

            Debug.Assert(index - resolution.Depth == GetIndex(resolution, x - 1, z));
            HeightColumn left = columns[index - resolution.Depth];
            Debug.Assert(index - 1 == GetIndex(resolution, x, z - 1));
            HeightColumn backward = columns[index - 1];

            for (int i = column.First, end = column.Last; i < end; i++)
            {
                // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                ref HeightSpan span = ref spans[i];

                CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, left, ref span, ref span.Left);
                CalculateNeighboursLoop(maxTraversableStep, minTraversableHeight, backward, ref span, ref span.Backward);
            }
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

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            ArrayPool<HeightColumn>.Shared.Return(columns);
            ArrayPool<HeightSpan>.Shared.Return(spans);
        }

        public void DrawGizmos(in Resolution resolution, bool neightbours)
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
                                Debug.Assert(index - resolution_.Depth == GetIndex(resolution_, x - 1, z));
                                HeightSpan span_ = spans[span.Left];
                                Draw3(resolution_, span_.Floor, span.Floor, HeightSpan.NULL_SIDE, 0);
                            }

                            if (span.Right != HeightSpan.NULL_SIDE)
                            {
                                Debug.Assert(index + resolution_.Depth == GetIndex(resolution_, x + 1, z));
                                HeightSpan span_ = spans[span.Right];
                                Draw3(resolution_, span_.Floor, span.Floor, 1, 0);
                            }

                            if (span.Backward != HeightSpan.NULL_SIDE)
                            {
                                Debug.Assert(index - 1 == GetIndex(resolution_, x, z - 1));
                                HeightSpan span_ = spans[span.Backward];
                                Draw3(resolution_, span_.Floor, span.Floor, 0, HeightSpan.NULL_SIDE);
                            }

                            if (span.Foward != HeightSpan.NULL_SIDE)
                            {
                                Debug.Assert(index + 1 == GetIndex(resolution_, x, z + 1));
                                HeightSpan span_ = spans[span.Foward];
                                Draw3(resolution_, span_.Floor, span.Floor, 0, 1);
                            }
                        }

                        int GetIndex(in Resolution resolution__, int x_, int z_)
                        {
                            int index_ = (resolution__.Depth * x_) + z_;
                            Debug.Assert(index_ < resolution__.Width * resolution__.Depth);
                            return index_;
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

        internal struct HeightSpan
        {
            // Value of Floor, Ceil, Left, Foward, Right, Backward when is null.
            public const int NULL_SIDE = -1;

            public int Floor;
            public int Ceil;

            public int Left;
            public int Foward;
            public int Right;
            public int Backward;

            public bool IsBorder {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get {
                    // A border is any span with less than 4 neighbours.
                    Debug.Assert(NULL_SIDE == -1, "If this fail, you must change the next line to perform 4 comparisons instead.");
                    bool isBorder = (Left | Foward | Right | Backward) == NULL_SIDE;
                    Debug.Assert(isBorder == (Left == NULL_SIDE || Foward == NULL_SIDE || Right == NULL_SIDE || Backward == NULL_SIDE));
                    return isBorder;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeightSpan(int floor, int ceil)
            {
                Floor = floor;
                Ceil = ceil;
                Left = NULL_SIDE;
                Foward = NULL_SIDE;
                Right = NULL_SIDE;
                Backward = NULL_SIDE;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool PresentNeighbour(ref int side, int neighbourIndex, HeightSpan neighbourSpan, int maxTraversableStep, int minTraversableHeight)
            {
                if (Floor == NULL_SIDE || neighbourSpan.Floor == NULL_SIDE)
                    return false;

                if (Math.Abs(Floor - neighbourSpan.Floor) <= maxTraversableStep)
                {
                    if (Ceil == NULL_SIDE || neighbourSpan.Ceil == NULL_SIDE || Math.Min(Ceil, neighbourSpan.Ceil) - Math.Max(Floor, neighbourSpan.Floor) >= minTraversableHeight)
                    {
                        Debug.Assert(side == NULL_SIDE);
                        side = neighbourIndex;
                        return true;
                    }
                }
                return false;
            }
        }

        internal enum SpanStatus : byte
        {
            Open = 0,
            InProgress = 1,
            Closed = 2,
        }
    }
}