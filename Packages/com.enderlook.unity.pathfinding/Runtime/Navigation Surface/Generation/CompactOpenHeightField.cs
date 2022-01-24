using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Unity.Pathfinding.Utils;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Generation
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

        /// <summary>
        /// Columns of the compacted open height field.
        /// </summary>
        public ReadOnlyArraySlice<HeightColumn> Columns => new ReadOnlyArraySlice<HeightColumn>(columns, columnsCount);

        /// <summary>
        /// Spans of the compacted open height field.
        /// </summary>
        public ReadOnlyArraySlice<HeightSpan> Spans => new ReadOnlyArraySlice<HeightSpan>(spans, spansCount);

        /// <summary>
        /// Amount of spans.
        /// </summary>
        public int SpansCount => spansCount;

        /// <summary>
        /// Amount of columns.
        /// </summary>
        public int ColumnsCount => columnsCount;

        private CompactOpenHeightField(HeightColumn[] columns, int columnsCount, HeightSpan[] spans, int spansCount)
        {
            this.columns = columns;
            this.columnsCount = columnsCount;
            this.spans = spans;
            this.spansCount = spansCount;
        }

        /// <summary>
        /// Get the column at the specified index.
        /// </summary>
        /// <param name="index">Index of the column.</param>
        /// <returns>Column at the specified index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly HeightColumn Column(int index)
        {
            Debug.Assert(index < columnsCount);
            return ref columns[index];
        }

        /// <summary>
        /// Get the span at the specified index.
        /// </summary>
        /// <param name="index">Index of the span.</param>
        /// <returns>Span at the specified index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly HeightSpan Span(int index)
        {
            Debug.Assert(index < spansCount);
            return ref spans[index];
        }

        /// <summary>
        /// Creates a the open height field of a height field.
        /// </summary>
        /// <param name="heightField">Height field used to create open height field.</param>
        /// <param name="options">Stores configuration information.</param>
        /// <returns>The open height field of the height field.</returns>
        public static async ValueTask<CompactOpenHeightField> Create(HeightField heightField, NavigationGenerationOptions options)
        {
            VoxelizationParameters parameters = options.VoxelizationParameters;
            heightField.DebugAssert(nameof(heightField), parameters, $"{nameof(options)}.{nameof(options.VoxelizationParameters)}");

            HeightColumn[] columns = ArrayPool<HeightColumn>.Shared.Rent(parameters.Width * parameters.Depth);
            RawPooledList<HeightSpan> spans = RawPooledList<HeightSpan>.Create();
            options.PushTask(2, "Compact Open Height Field");
            {
                if (options.ShouldUseTimeSlice)
                    spans = await Initialize<Toggle.Yes>(heightField, columns, spans, options);
                else
                    spans = await Initialize<Toggle.No>(heightField, columns, spans, options);
                options.StepTask();

                options.PushTask(parameters.ColumnsCount, "Calculate Neighbours");
                {
                    if (options.UseMultithreading)
                        CalculateNeighboursMultiThread(options, columns, spans.UnderlyingArray);
                    else if (options.ShouldUseTimeSlice)
                        await CalculateNeighboursSingleThread<Toggle.Yes>(options, columns, spans.UnderlyingArray);
                    else
                        await CalculateNeighboursSingleThread<Toggle.No>(options, columns, spans.UnderlyingArray);
                }
                options.PopTask();
                options.StepTask();
            }
            options.PopTask();
            return new CompactOpenHeightField(columns, parameters.Width * parameters.Depth, spans.UnderlyingArray, spans.Count);
        }

        /// <summary>
        /// Debug assert that this instance is valid.
        /// </summary>
        /// <param name="parameterName">Name of the instance.</param>
        [System.Diagnostics.Conditional("Debug")]
        public void DebugAssert(string parameterName, in VoxelizationParameters parameters, string resolutionParameterName)
        {
            Debug.Assert(!(columns is null), $"{parameterName} is default");

            if (!(columns is null))
            {
                Debug.Assert(columnsCount == parameters.ColumnsCount, $"{parameterName} is not valid for the passed resolution {resolutionParameterName}.");

                if (unchecked((uint)columnsCount >= (uint)columns.Length))
                {
                    Debug.Assert(false, "Index out of range.");
                    return;
                }

                for (int i = 0; i < columnsCount; i++)
                {
                    if (columns[i].Last > parameters.Height)
                    {
                        Debug.Assert(false, $"{parameterName} is not valid for the passed resolution {resolutionParameterName}.");
                        return;
                    }
                }
            }
        }

        private static async ValueTask<RawPooledList<HeightSpan>> Initialize<TYield>(HeightField heightField, HeightColumn[] columns, RawPooledList<HeightSpan> spanBuilder, NavigationGenerationOptions options)
        {
            VoxelizationParameters parameters = options.VoxelizationParameters;
            options.PushTask(parameters.ColumnsCount, "Initialize");
            {
                int index = 0;
                for (int x = 0; x < parameters.Width; x++)
                {
                    for (int z = 0; z < parameters.Depth; z++)
                    {
                        // TODO: We can convert this to multithreading by having one array per column and copying all the output to a final array.
                        spanBuilder = InitializeWork(heightField, columns, spanBuilder, parameters, ref index, x, z);
                        await options.StepTaskAndYield<TYield>();
                    }
                }
            }
            options.PopTask();
            return spanBuilder;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static RawPooledList<HeightSpan> InitializeWork(in HeightField heightField, HeightColumn[] columns, RawPooledList<HeightSpan> spanBuilder, in VoxelizationParameters parameters, ref int index, int x, int z)
        {
            Debug.Assert(index == parameters.GetIndex(x, z));
            int startIndex = spanBuilder.Count;

            HeightField.HeightColumn column = heightField.Columns[index];
            ReadOnlySpan<HeightField.HeightSpan> spans = column.Spans(heightField);
            Debug.Assert(spans.Length > 0);

            int i = 0;
            int y = 0;

            if (spans.Length > 1)
            {
                HeightField.HeightSpan span = spans[i++];

#if UNITY_ASSERTIONS
                bool wasSolid = span.IsSolid;
#endif

                if (!span.IsSolid)
                {
                    /* Do we actually need to add this span?
                     * If we remove it, everything works... the output is just a bit different,
                     * maybe it doesn't mater. */
                    const int floor = -1;
                    int ceil = y + span.Height;
                    spanBuilder.Add(new HeightSpan(floor, ceil));

                    // Regardless we remove above span, this line must stay.
                    y += span.Height;
                }
                else
                {
                    int floor = y + span.Height;
                    if (spans.Length > 2)
                    {
                        span = spans[i++];

#if UNITY_ASSERTIONS
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
#if UNITY_ASSERTIONS
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
#if UNITY_ASSERTIONS
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
#if UNITY_ASSERTIONS
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

            columns[index++] = new HeightColumn(startIndex, spanBuilder.Count);
            return spanBuilder;
        }

        private static async ValueTask CalculateNeighboursSingleThread<TYield>(NavigationGenerationOptions options, HeightColumn[] columns, HeightSpan[] spans)
        {
            VoxelizationParameters parameters = options.VoxelizationParameters;
            int maxTraversableStep = options.MaximumTraversableStep;
            int minTraversableHeight = options.MaximumTraversableStep;

            int xM = parameters.Width - 1;
            int zM = parameters.Depth - 1;

            int index = 0;

            int x = 0;
            {
                int z = 0;
                CalculateNeighboursBody<RightForward>(parameters, columns, spans, maxTraversableStep, minTraversableHeight, ref index, x, z);
                await options.StepTaskAndYield<TYield>();
                for (z++; z < zM; z++)
                {
                    CalculateNeighboursBody<RightForwardBackward>(parameters, columns, spans, maxTraversableStep, minTraversableHeight, ref index, x, z);
                    await options.StepTaskAndYield<TYield>();
                }
                Debug.Assert(z == zM);
                CalculateNeighboursBody<RightBackwardIncrement>(parameters, columns, spans, maxTraversableStep, minTraversableHeight, ref index, x, z);
                await options.StepTaskAndYield<TYield>();
            }

            for (x++; x < xM; x++)
            {
                int z = 0;
                CalculateNeighboursBody<LeftRightForward>(parameters, columns, spans, maxTraversableStep, minTraversableHeight, ref index, x, z);
                await options.StepTaskAndYield<TYield>();
                for (z = 1; z < zM; z++)
                {
                    /* This is the true body of this function.
                        * All methods that starts with CalculateNeighboursBody() are actually specializations of this body to avoid branching inside the loop.
                        * TODO: Does this actually improves perfomance? */
                    CalculateNeighboursBody<LeftRightForwardBackward>(parameters, columns, spans, maxTraversableStep, minTraversableHeight, ref index, x, z);
                    await options.StepTaskAndYield<TYield>();
                }
                Debug.Assert(z == zM);
                CalculateNeighboursBody<LeftRightBackwardIncrement>(parameters, columns, spans, maxTraversableStep, minTraversableHeight, ref index, x, z);
                await options.StepTaskAndYield<TYield>();
            }

            Debug.Assert(x == xM);
            {
                int z = 0;
                CalculateNeighboursBody<LeftForward>(parameters, columns, spans, maxTraversableStep, minTraversableHeight, ref index, x, z);
                await options.StepTaskAndYield<TYield>();
                for (z++; z < zM; z++)
                {
                    CalculateNeighboursBody<LeftForwardBackward>(parameters, columns, spans, maxTraversableStep, minTraversableHeight, ref index, x, z);
                    await options.StepTaskAndYield<TYield>();
                }
                Debug.Assert(z == zM);
                CalculateNeighboursBody<LeftBackward>(parameters, columns, spans, maxTraversableStep, minTraversableHeight, ref index, x, z);
                await options.StepTaskAndYield<TYield>();
            }
        }

        private static void CalculateNeighboursMultiThread(NavigationGenerationOptions options, HeightColumn[] columns, HeightSpan[] spans)
        {
            VoxelizationParameters parameters = options.VoxelizationParameters;
            int maxTraversableStep = options.MaximumTraversableStep;
            int minTraversableHeight = options.MaximumTraversableStep;

            int xM = parameters.Width - 1;
            int zM = parameters.Depth - 1;

            Parallel.For(0, 9, t =>
            {
                switch (t)
                {
                    case 0:
                    {
                        int x = 0;
                        int z = 0;
                        int index = 0;
                        CalculateNeighboursBody<RightForward>(parameters, columns, spans, maxTraversableStep, minTraversableHeight, ref index, x, z);
                        options.StepTask();
                        break;
                    }
                    case 1:
                    {
                        int x = 0;
                        Parallel.For(1, zM, z =>
                        {
                            int index = parameters.GetIndex(x, z);
                            CalculateNeighboursBody<RightForwardBackward>(parameters, columns, spans, maxTraversableStep, minTraversableHeight, ref index, x, z);
                            options.StepTask();
                        });
                        break;
                    }
                    case 2:
                    {
                        int x = 0;
                        int z = zM;
                        int index = parameters.GetIndex(x, z);
                        CalculateNeighboursBody<RightBackwardIncrement>(parameters, columns, spans, maxTraversableStep, minTraversableHeight, ref index, x, z);
                        options.StepTask();
                        break;
                    }
                    case 3:
                    {
                        Parallel.For(1, xM, x =>
                        {
                            int z = 0;
                            int index = parameters.GetIndex(x, z);
                            CalculateNeighboursBody<LeftRightForward>(parameters, columns, spans, maxTraversableStep, minTraversableHeight, ref index, x, z);
                            options.StepTask();
                        });
                        break;
                    }
                    case 4:
                    {
                        int xW = xM - 1;
                        int zW = zM - 1;
                        Parallel.For(0, xW * zW, i =>
                        {
                            int x = (i / zW) + 1;
                            int z = (i % zW) + 1;
                            int index = parameters.GetIndex(x, z);
                            CalculateNeighboursBody<LeftRightForwardBackward>(parameters, columns, spans, maxTraversableStep, minTraversableHeight, ref index, x, z);
                            options.StepTask();
                        });
                        break;
                    }
                    case 5:
                    {
                        Parallel.For(1, xM, x =>
                        {
                            int z = zM;
                            int index = parameters.GetIndex(x, z);
                            CalculateNeighboursBody<LeftRightBackwardIncrement>(parameters, columns, spans, maxTraversableStep, minTraversableHeight, ref index, x, z);
                            options.StepTask();
                        });
                        break;
                    }
                    case 6:
                    {
                        int x = xM;
                        int z = 0;
                        int index = parameters.GetIndex(x, z);
                        CalculateNeighboursBody<LeftForward>(parameters, columns, spans, maxTraversableStep, minTraversableHeight, ref index, x, z);
                        options.StepTask();
                        break;
                    }
                    case 7:
                    {
                        Parallel.For(1, zM, z =>
                        {
                            int x = xM;
                            int index = parameters.GetIndex(x, z);
                            CalculateNeighboursBody<LeftForwardBackward>(parameters, columns, spans, maxTraversableStep, minTraversableHeight, ref index, x, z);
                            options.StepTask();
                        });
                        break;
                    }
                    case 8:
                    {
                        int x = xM;
                        int z = zM;
                        int index = parameters.GetIndex(x, z);
                        CalculateNeighboursBody<LeftBackward>(parameters, columns, spans, maxTraversableStep, minTraversableHeight, ref index, x, z);
                        options.StepTask();
                        break;
                    }
                }
            });
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

        private static void CalculateNeighboursBody<T>(in VoxelizationParameters parameters, HeightColumn[] columns, HeightSpan[] spans, int maxTraversableStep, int minTraversableHeight, ref int index, int x, int z)
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
            Debug.Assert(index == parameters.GetIndex(x, z));

            HeightColumn column = columns[index];

            HeightColumn left, right, backward, forward;

            if (typeof(T) == typeof(LeftRightForwardBackward) ||
                typeof(T) == typeof(LeftRightForward) ||
                typeof(T) == typeof(LeftRightBackwardIncrement) ||
                typeof(T) == typeof(LeftForwardBackward) ||
                typeof(T) == typeof(LeftForward) ||
                typeof(T) == typeof(LeftBackward))
            {
                Debug.Assert(index - parameters.Depth == parameters.GetIndex(x - 1, z));
                left = columns[index - parameters.Depth];
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
                Debug.Assert(index + parameters.Depth == parameters.GetIndex(x + 1, z));
                right = columns[index + parameters.Depth];
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
                Debug.Assert(index - 1 == parameters.GetIndex(x, z - 1));
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
                Debug.Assert(index + 1 == parameters.GetIndex(x, z + 1));
                forward = columns[++index];
            }
            else
                forward = default;

            if (typeof(T) == typeof(RightBackwardIncrement) ||
                typeof(T) == typeof(LeftRightBackwardIncrement))
                index++;

            for (int i = column.First; i < column.Last; i++)
            {
                // TODO: Should allow yielding here?
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
                    CalculateNeighboursLoop(spans, maxTraversableStep, minTraversableHeight, left, ref span, ref span.Left);

                if (typeof(T) == typeof(LeftRightForwardBackward) ||
                    typeof(T) == typeof(LeftRightForward) ||
                    typeof(T) == typeof(LeftRightBackwardIncrement) ||
                    typeof(T) == typeof(RightForwardBackward) ||
                    typeof(T) == typeof(RightForward) ||
                    typeof(T) == typeof(RightBackwardIncrement))
                    CalculateNeighboursLoop(spans, maxTraversableStep, minTraversableHeight, right, ref span, ref span.Right);

                if (typeof(T) == typeof(LeftRightForwardBackward) ||
                    typeof(T) == typeof(LeftRightForward) ||
                    typeof(T) == typeof(RightForwardBackward) ||
                    typeof(T) == typeof(LeftForwardBackward) ||
                    typeof(T) == typeof(RightForward) ||
                    typeof(T) == typeof(LeftForward))
                    CalculateNeighboursLoop(spans, maxTraversableStep, minTraversableHeight, forward, ref span, ref span.Forward);

                if (typeof(T) == typeof(LeftRightForwardBackward) ||
                    typeof(T) == typeof(LeftRightBackwardIncrement) ||
                    typeof(T) == typeof(RightForwardBackward) ||
                    typeof(T) == typeof(LeftForwardBackward) ||
                    typeof(T) == typeof(RightBackwardIncrement) ||
                    typeof(T) == typeof(LeftBackward))
                    CalculateNeighboursLoop(spans, maxTraversableStep, minTraversableHeight, backward, ref span, ref span.Backward);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CalculateNeighboursLoop(HeightSpan[] spans, int maxTraversableStep, int minTraversableHeight, HeightColumn column, ref HeightSpanBuilder span, ref int side)
        {
            for (int j = column.First, end = column.Last; j < end; j++)
                // TODO: Should allow yielding here?
                if (span.PresentNeighbour(ref side, j, spans[j], maxTraversableStep, minTraversableHeight))
                    break;
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            ArrayPool<HeightColumn>.Shared.Return(columns);
            ArrayPool<HeightSpan>.Shared.Return(spans);
        }

        public void DrawGizmos(in VoxelizationParameters parameters, bool surfaces, bool neightbours, bool volumes)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(parameters.VolumeCenter, parameters.VolumeSize);
            Vector3 offset = parameters.OffsetAtFloor;

            int i = 0;
            for (int x = 0; x < parameters.Width; x++)
            {
                for (int z = 0; z < parameters.Depth; z++)
                {
                    Vector2 position_ = new Vector2(x, z) * parameters.VoxelSize;
                    int i_ = i;

                    HeightColumn column = columns[i++];

                    if (!column.IsEmpty)
                    {
                        int j = column.First;

                        ref readonly HeightSpan heightSpan = ref spans[j++];
                        if (heightSpan.Floor != HeightSpan.NULL_SIDE)
                        {
                            Draw(parameters, heightSpan.Floor - .1f, Color.green);
                            Draw4(parameters, heightSpan.Floor, heightSpan.Ceil);
                        }
                        else
                            Draw4(parameters, -.1f, heightSpan.Ceil);
                        Draw(parameters, heightSpan.Ceil + .1f, Color.red);
                        Draw2(parameters, spans, heightSpan, i_);

                        for (; j < column.Last - 1; j++)
                        {
                            heightSpan = ref spans[j];
                            Draw(parameters, heightSpan.Floor - .1f, Color.green);
                            Draw(parameters, heightSpan.Ceil + .1f, Color.red);
                            Draw4(parameters, heightSpan.Floor, heightSpan.Ceil);
                            Draw2(parameters, spans, heightSpan, i_);
                        }

                        if (column.Count > 1) // Shouldn't this be 2?
                        {
                            Debug.Assert(j == column.Last - 1);
                            heightSpan = ref spans[j];
                            Draw(parameters, heightSpan.Floor, Color.green);
                            if (heightSpan.Ceil != HeightSpan.NULL_SIDE)
                            {
                                Draw(parameters, heightSpan.Ceil + .1f, Color.red);
                                Draw4(parameters, heightSpan.Floor, heightSpan.Ceil);
                            }
                            else
                                Draw4(parameters, heightSpan.Floor, parameters.Height + .1f);
                            Draw2(parameters, spans, heightSpan, i_);
                        }
                    }

                    void Draw(in VoxelizationParameters parameters_, float y, Color color)
                    {
                        if (!surfaces)
                            return;
                        Gizmos.color = color;
                        Vector3 position = new Vector3(position_.x, parameters_.VoxelSize * y, position_.y);
                        Vector3 center_ = offset + position;
                        Vector3 size = new Vector3(parameters_.VoxelSize, parameters_.VoxelSize * .1f, parameters_.VoxelSize);
                        Gizmos.DrawCube(center_, size);
                    }

                    void Draw4(in VoxelizationParameters parameters_, float yMin, float yMax)
                    {
                        if (!volumes)
                            return;
                        Gizmos.color = Color.cyan;
                        Vector3 position = new Vector3(position_.x, parameters_.VoxelSize * ((yMin + yMax) / 2), position_.y);
                        Vector3 center_ = offset + position;
                        Vector3 size = new Vector3(parameters_.VoxelSize, yMax - yMin, parameters_.VoxelSize);
                        Gizmos.DrawWireCube(center_, size);
                    }

                    void Draw2(in VoxelizationParameters parameters_, HeightSpan[] spans, HeightSpan span, int index)
                    {
                        if (!neightbours)
                            return;
                        Gizmos.color = Color.yellow;
                        unsafe
                        {
                            if (span.Left != HeightSpan.NULL_SIDE)
                            {
                                Debug.Assert(index - parameters_.Depth == parameters_.GetIndex(x - 1, z));
                                HeightSpan span_ = spans[span.Left];
                                Draw3(parameters_, span_.Floor, span.Floor, HeightSpan.NULL_SIDE, 0);
                            }

                            if (span.Right != HeightSpan.NULL_SIDE)
                            {
                                Debug.Assert(index + parameters_.Depth == parameters_.GetIndex(x + 1, z));
                                HeightSpan span_ = spans[span.Right];
                                Draw3(parameters_, span_.Floor, span.Floor, 1, 0);
                            }

                            if (span.Backward != HeightSpan.NULL_SIDE)
                            {
                                Debug.Assert(index - 1 == parameters_.GetIndex(x, z - 1));
                                HeightSpan span_ = spans[span.Backward];
                                Draw3(parameters_, span_.Floor, span.Floor, 0, HeightSpan.NULL_SIDE);
                            }

                            if (span.Forward != HeightSpan.NULL_SIDE)
                            {
                                Debug.Assert(index + 1 == parameters_.GetIndex(x, z + 1));
                                HeightSpan span_ = spans[span.Forward];
                                Draw3(parameters_, span_.Floor, span.Floor, 0, 1);
                            }
                        }

                        void Draw3(in VoxelizationParameters parameters__, int yTo, float yFrom, int x_, int z_)
                        {
                            const bool drawArrow = false;

                            Vector3 positionFrom = new Vector3(position_.x, parameters__.VoxelSize * yFrom, position_.y);
                            Vector3 centerFrom = offset + positionFrom;

                            Vector3 position__ = new Vector2((x + x_) * parameters__.VoxelSize, (z + z_) * parameters__.VoxelSize);
                            Vector3 positionTo = new Vector3(position__.x, parameters__.VoxelSize * yTo, position__.y);
                            Vector3 centerTo = offset + positionTo;

                            int v = centerFrom.x.CompareTo(centerTo.x);
                            if (v == 0)
                            {
                                v = centerFrom.y.CompareTo(centerTo.y);
                                if (v == 0)
                                    v = centerFrom.z.CompareTo(centerTo.z);
                            }

                            if (v <= 0)
                            {
                                // Optimizes perfomance by don't drawing twice the same line.
                                Gizmos.DrawLine(centerFrom, centerTo);
                            }

                            if (drawArrow)
                            {
                                // https://forum.unity.com/threads/debug-drawarrow.85980/
                                const float arrowHeadLength = .25f;
                                const float arrowHeadAngle = 20f;
                                Vector3 direction = centerTo - centerFrom;
                                Quaternion lookDirection = Quaternion.LookRotation(direction);
                                Vector3 right = lookDirection * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * Vector3.forward;
                                Vector3 left = lookDirection * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * Vector3.forward;
                                // Put the head of the arrow closer to `centerTo` but not exactly in the same point to give some space.
                                Vector3 destination = (centerFrom + (centerTo * 7)) / 8;
                                Gizmos.DrawRay(destination, right * arrowHeadLength);
                                Gizmos.DrawRay(destination, left * arrowHeadLength);
                            }
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ReadOnlySpan<T> Span<T>(ReadOnlySpan<T> span) => span.Slice(First, Last - First);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Span<T> Span<T>(Span<T> span) => span.Slice(First, Last - First);
        }

        internal readonly struct HeightSpan
        {
            // Value of Floor, Ceil, Left, Forward, Right, Backward when is null.
            public const int NULL_SIDE = -1;

            // Directions of each side.
            public const int LEFT_DIRECTION = 0;
            public const int FORWARD_DIRECTION = 1;
            public const int RIGHT_DIRECTION = 2;
            public const int BACKWARD_DIRECTION = 3;

            public readonly int Floor;
            public readonly int Ceil;

            public readonly int Left;
            public readonly int Forward;
            public readonly int Right;
            public readonly int Backward;

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
            public int GetSide(int direction)
            {
                Debug.Assert(direction >= 0 && direction < 4);
                return Unsafe.Add(ref Unsafe.AsRef(Left), direction);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetSide<T>()
            {
                Side.DebugAssert<T>();

                if (typeof(T) == typeof(Side.Left))
                    return Left;
                else if (typeof(T) == typeof(Side.Right))
                    return Right;
                else if (typeof(T) == typeof(Side.Forward))
                    return Forward;
                else if (typeof(T) == typeof(Side.Backward))
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
            public static int RotateClockwise(int direction) => (direction + 1) & 0x3;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int RotateCounterClockwise(int direction) => (direction + 3) & 0x3;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetSideRotatedClockwise<T>()
            {
                Side.DebugAssert<T>();

                if (typeof(T) == typeof(Side.Left))
                    return Forward;
                else if (typeof(T) == typeof(Side.Forward))
                    return Right;
                else if (typeof(T) == typeof(Side.Right))
                    return Backward;
                else if (typeof(T) == typeof(Side.Backward))
                    return Left;
                else
                {
                    Debug.Assert(false);
                    return 0;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetDirectionWithOffset(int direction, ref int x, ref int z)
            {
                Debug.Assert(direction >= 0 && direction < 4);
                switch (direction)
                {
                    case 0:
                        x += -1;
                        break;
                    case 1:
                        z += 1;
                        break;
                    case 2:
                        x += 1;
                        break;
                    case 3:
                        z += -1;
                        break;
                    default:
                        Debug.Assert(false, "Impossible state.");
                        goto case 0;
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