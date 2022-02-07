using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Pools;
using Enderlook.Unity.Pathfinding.Utils;

using System;
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
        private readonly ArraySlice<HeightColumn> columns;
        private readonly ArraySlice<HeightSpan> spans;

        /// <summary>
        /// Columns of the compacted open height field.
        /// </summary>
        public ReadOnlyArraySlice<HeightColumn> Columns => columns;

        /// <summary>
        /// Spans of the compacted open height field.
        /// </summary>
        public ReadOnlyArraySlice<HeightSpan> Spans => spans;

        public CompactOpenHeightField(ArraySlice<HeightColumn> columns, ArraySlice<HeightSpan> spans)
        {
            this.columns = columns;
            this.spans = spans;
        }

        /// <summary>
        /// Creates a the open height field of a height field.
        /// </summary>
        /// <param name="options">Stores configuration information.</param>
        /// <param name="heightField">Height field used to create open height field.</param>
        /// <returns>The open height field of the height field.</returns>
        public static async ValueTask<CompactOpenHeightField> Create(NavigationGenerationOptions options, HeightField heightField)
        {
            VoxelizationParameters parameters = options.VoxelizationParameters;
            heightField.DebugAssert(nameof(heightField), parameters, $"{nameof(options)}.{nameof(options.VoxelizationParameters)}");

            ArraySlice<HeightColumn> columns = new ArraySlice<HeightColumn>(parameters.Width * parameters.Depth, false);
            RawPooledList<HeightSpan> spans = RawPooledList<HeightSpan>.Create();
            options.PushTask(2, "Compact Open Height Field");
            {
                if (options.ShouldUseTimeSlice)
                    spans = await Initialize<Toggle.Yes>(options, heightField, columns, spans);
                else
                    spans = await Initialize<Toggle.No>(options, heightField, columns, spans);
                options.StepTask();

                options.PushTask(parameters.ColumnsCount, "Calculate Neighbours");
                {
                    if (options.UseMultithreading)
                        MultiThread.Calculate(options, columns, spans);
                    else if (options.ShouldUseTimeSlice)
                        await CalculateNeighboursSingleThread<Toggle.Yes>(options, columns, spans);
                    else
                        await CalculateNeighboursSingleThread<Toggle.No>(options, columns, spans);
                }
                options.PopTask();
                options.StepTask();
            }
            options.PopTask();
            return new CompactOpenHeightField(columns, spans);
        }

        /// <summary>
        /// Debug assert that this instance is valid.
        /// </summary>
        /// <param name="parameterName">Name of the instance.</param>
        [System.Diagnostics.Conditional("Debug")]
        public void DebugAssert(string parameterName, in VoxelizationParameters parameters, string resolutionParameterName)
        {
            Debug.Assert(!(columns.Array is null), $"{parameterName} is default");

            if (!(columns.Array is null))
            {
                Debug.Assert(columns.Length == parameters.ColumnsCount, $"{parameterName} is not valid for the passed resolution {resolutionParameterName}.");

                for (int i = 0; i < columns.Length; i++)
                {
                    if (columns[i].Last > parameters.Height)
                    {
                        Debug.Assert(false, $"{parameterName} is not valid for the passed resolution {resolutionParameterName}.");
                        return;
                    }
                }
            }
        }

        private static async ValueTask<RawPooledList<HeightSpan>> Initialize<TYield>(NavigationGenerationOptions options, HeightField heightField, ArraySlice<HeightColumn> columns, RawPooledList<HeightSpan> spanBuilder)
        {
            VoxelizationParameters parameters = options.VoxelizationParameters;
            options.PushTask(parameters.ColumnsCount, "Initialize");
            {
                TimeSlicer timeSlicer = options.TimeSlicer;
                int index = 0;
                for (int x = 0; x < parameters.Width; x++)
                {
                    for (int z = 0; z < parameters.Depth; z++)
                    {
                        // TODO: We can convert this to multithreading by having one array per column and copying all the output to a final array.

                        Debug.Assert(index == parameters.GetIndex(x, z));
                        int startIndex = spanBuilder.Count;

                        HeightField.HeightColumn column = heightField.Columns[index];
#if DEBUG
                        bool wasSolid = default;
#endif
                        int i = 0;
                        int y = 0;
                        if (InitializeWorkStart(heightField, ref spanBuilder, column, ref wasSolid, ref i, ref y))
                        {
                            while (InitializeWork<TYield>(timeSlicer, heightField, ref spanBuilder, column, ref i, ref y
#if DEBUG
                                , ref wasSolid
#endif
                            ))
                                await timeSlicer.Yield();
                        }
                        columns[index++] = new HeightColumn(startIndex, spanBuilder.Count);
                        options.StepTask();
                    }
                }
            }
            options.PopTask();
            return spanBuilder;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool InitializeWorkStart(in HeightField heightField, ref RawPooledList<HeightSpan> spanBuilder, in HeightField.HeightColumn column, ref bool wasSolid, ref int i, ref int y)
        {
            ReadOnlySpan<HeightField.HeightSpan> spans = column.Spans(heightField);
            Debug.Assert(spans.Length > 0);
            if (spans.Length > 1)
            {
                HeightField.HeightSpan span = spans[i++];

#if DEBUG
                wasSolid = span.IsSolid;
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
                        Debug.Assert(wasSolid != spans[i].IsSolid);
#endif
                        const int ceil = -1;
                        spanBuilder.Add(new HeightSpan(floor, ceil));

                        return false;
                    }
                }
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool InitializeWork<TYield>(TimeSlicer timeSlicer, in HeightField heightField, ref RawPooledList<HeightSpan> spanBuilder, in HeightField.HeightColumn column, ref int i, ref int y
#if DEBUG
            , ref bool wasSolid
#endif
            )
        {
            ReadOnlySpan<HeightField.HeightSpan> spans = column.Spans(heightField);
            Debug.Assert(spans.Length > 0);

            for (; i < spans.Length - 1;)
            {
                HeightField.HeightSpan span = spans[i];
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
                i++;

                if (Toggle.IsToggled<TYield>() && timeSlicer.MustYield())
                    return true;
            }

            if (spans.Length > 2)
            {
                Debug.Assert(i == spans.Length - 1);
                HeightField.HeightSpan span = spans[i];
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
            return false;
        }

        private static async ValueTask CalculateNeighboursSingleThread<TYield>(NavigationGenerationOptions options, ArraySlice<HeightColumn> columns, ArraySlice<HeightSpan>  spans)
        {
            TimeSlicer timeSlicer = options.TimeSlicer;
            VoxelizationParameters parameters = options.VoxelizationParameters;
            int maxTraversableStep = options.MaximumTraversableStep;
            int minTraversableHeight = options.MaximumTraversableStep;

            int xM = parameters.Width - 1;
            int zM = parameters.Depth - 1;

            int index = 0;

            int x = 0;
            {
                int z = 0;
                index = await CalculateNeighboursBody<RightForward, TYield>(timeSlicer, parameters, columns, spans, maxTraversableStep, minTraversableHeight, index, x, z);
                options.StepTask();
                for (z++; z < zM; z++)
                {
                    index = await CalculateNeighboursBody<RightForwardBackward, TYield>(timeSlicer, parameters, columns, spans, maxTraversableStep, minTraversableHeight, index, x, z);
                    options.StepTask();
                }
                Debug.Assert(z == zM);
                index = await CalculateNeighboursBody<RightBackwardIncrement, TYield>(timeSlicer, parameters, columns, spans, maxTraversableStep, minTraversableHeight, index, x, z);
                options.StepTask();
            }

            for (x++; x < xM; x++)
            {
                int z = 0;
                index = await CalculateNeighboursBody<LeftRightForward, TYield>(timeSlicer, parameters, columns, spans, maxTraversableStep, minTraversableHeight, index, x, z);
                options.StepTask();
                for (z = 1; z < zM; z++)
                {
                    /* This is the true body of this function.
                     * All methods that starts with CalculateNeighboursBody() are actually specializations of this body to avoid branching inside the loop.
                     * TODO: Does this actually improves perfomance? */
                    index = await CalculateNeighboursBody<LeftRightForwardBackward, TYield>(timeSlicer, parameters, columns, spans, maxTraversableStep, minTraversableHeight, index, x, z);
                    options.StepTask();
                }
                Debug.Assert(z == zM);
                index = await CalculateNeighboursBody<LeftRightBackwardIncrement, TYield>(timeSlicer, parameters, columns, spans, maxTraversableStep, minTraversableHeight, index, x, z);
                options.StepTask();
            }

            Debug.Assert(x == xM);
            {
                int z = 0;
                index = await CalculateNeighboursBody<LeftForward, TYield>(timeSlicer, parameters, columns, spans, maxTraversableStep, minTraversableHeight, index, x, z);
                options.StepTask();
                for (z++; z < zM; z++)
                {
                    index = await CalculateNeighboursBody<LeftForwardBackward, TYield>(timeSlicer, parameters, columns, spans, maxTraversableStep, minTraversableHeight, index, x, z);
                    options.StepTask();
                }
                Debug.Assert(z == zM);
                await CalculateNeighboursBody<LeftBackward, TYield>(timeSlicer, parameters, columns, spans, maxTraversableStep, minTraversableHeight, index, x, z);
                options.StepTask();
            }
        }

        private sealed class MultiThread
        {
            private readonly Action<int> action;
            private NavigationGenerationOptions options;
            private ArraySlice<HeightColumn> columns;
            private ArraySlice<HeightSpan> spans;

            public MultiThread() => action = Process;

            public static void Calculate(NavigationGenerationOptions options, ArraySlice<HeightColumn> columns, ArraySlice<HeightSpan> spans)
            {
                ObjectPool<MultiThread> pool = ObjectPool<MultiThread>.Shared;
                MultiThread instance = pool.Rent();
                {
                    instance.options = options;
                    instance.columns = columns;
                    instance.spans = spans;

                    Parallel.For(0, options.VoxelizationParameters.ColumnsCount, instance.action);

                    instance.options = default;
                    instance.columns = default;
                    instance.spans = default;
                }
                pool.Return(instance);
            }

            private void Process(int index)
            {
                TimeSlicer timeSlicer = options.TimeSlicer;
                VoxelizationParameters parameters = options.VoxelizationParameters;
                int xM = parameters.Width - 1;
                int zM = parameters.Depth - 1;

                Vector2Int v = parameters.From2D(index);
                int x = v.x;
                int z = v.y;

                ValueTask<int> task;
                if (x == 0)
                {
                    if (z == 0)
                        task = CalculateNeighboursBody<RightForward, Toggle.No>(timeSlicer, parameters, columns, spans, options.MaximumTraversableStep, options.MinimumTraversableHeight, index, x, z);
                    else if (z != zM)
                        task = CalculateNeighboursBody<RightForwardBackward, Toggle.No>(timeSlicer, parameters, columns, spans, options.MaximumTraversableStep, options.MinimumTraversableHeight, index, x, z);
                    else
                        task = CalculateNeighboursBody<RightBackwardIncrement, Toggle.No>(timeSlicer, parameters, columns, spans, options.MaximumTraversableStep, options.MinimumTraversableHeight, index, x, z);
                }
                else if (x != xM)
                {
                    if (z == 0)
                        task = CalculateNeighboursBody<LeftRightForward, Toggle.No>(timeSlicer, parameters, columns, spans, options.MaximumTraversableStep, options.MinimumTraversableHeight, index, x, z);
                    else if (z != zM)
                        task = CalculateNeighboursBody<LeftRightForwardBackward, Toggle.No>(timeSlicer, parameters, columns, spans, options.MaximumTraversableStep, options.MinimumTraversableHeight, index, x, z);
                    else
                        task = CalculateNeighboursBody<LeftRightBackwardIncrement, Toggle.No>(timeSlicer, parameters, columns, spans, options.MaximumTraversableStep, options.MinimumTraversableHeight, index, x, z);
                }
                else
                {
                    if (z == 0)
                        task = CalculateNeighboursBody<LeftForward, Toggle.No>(timeSlicer, parameters, columns, spans, options.MaximumTraversableStep, options.MinimumTraversableHeight, index, x, z);
                    else if (z != zM)
                        task = CalculateNeighboursBody<LeftForwardBackward, Toggle.No>(timeSlicer, parameters, columns, spans, options.MaximumTraversableStep, options.MinimumTraversableHeight, index, x, z);
                    else
                        task = CalculateNeighboursBody<LeftBackward, Toggle.No>(timeSlicer, parameters, columns, spans, options.MaximumTraversableStep, options.MinimumTraversableHeight, index, x, z);
                }

                Debug.Assert(task.IsCompleted);
                options.StepTask();
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

        private static async ValueTask<int> CalculateNeighboursBody<T, TYield>(TimeSlicer timeSlicer, VoxelizationParameters parameters, ArraySlice<HeightColumn> columns, ArraySlice<HeightSpan> spans, int maxTraversableStep, int minTraversableHeight, int index, int x, int z)
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
                // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                if (typeof(T) == typeof(LeftRightForwardBackward) ||
                    typeof(T) == typeof(LeftRightForward) ||
                    typeof(T) == typeof(LeftRightBackwardIncrement) ||
                    typeof(T) == typeof(LeftForwardBackward) ||
                    typeof(T) == typeof(LeftForward) ||
                    typeof(T) == typeof(LeftBackward))
                {
                    while (CalculateNeighboursLoop<Side.Left, TYield>(timeSlicer, spans, left, maxTraversableStep, minTraversableHeight, ref spans[i]))
                        await timeSlicer.Yield();
                }

                if (typeof(T) == typeof(LeftRightForwardBackward) ||
                    typeof(T) == typeof(LeftRightForward) ||
                    typeof(T) == typeof(LeftRightBackwardIncrement) ||
                    typeof(T) == typeof(RightForwardBackward) ||
                    typeof(T) == typeof(RightForward) ||
                    typeof(T) == typeof(RightBackwardIncrement))
                {
                    while (CalculateNeighboursLoop<Side.Right, TYield>(timeSlicer, spans, right, maxTraversableStep, minTraversableHeight, ref spans[i]))
                        await timeSlicer.Yield();
                }

                if (typeof(T) == typeof(LeftRightForwardBackward) ||
                    typeof(T) == typeof(LeftRightForward) ||
                    typeof(T) == typeof(RightForwardBackward) ||
                    typeof(T) == typeof(LeftForwardBackward) ||
                    typeof(T) == typeof(RightForward) ||
                    typeof(T) == typeof(LeftForward))
                {
                    while (CalculateNeighboursLoop<Side.Forward, TYield>(timeSlicer, spans, forward, maxTraversableStep, minTraversableHeight, ref spans[i]))
                        await timeSlicer.Yield();
                }

                if (typeof(T) == typeof(LeftRightForwardBackward) ||
                    typeof(T) == typeof(LeftRightBackwardIncrement) ||
                    typeof(T) == typeof(RightForwardBackward) ||
                    typeof(T) == typeof(LeftForwardBackward) ||
                    typeof(T) == typeof(RightBackwardIncrement) ||
                    typeof(T) == typeof(LeftBackward))
                {
                    while (CalculateNeighboursLoop<Side.Backward, TYield>(timeSlicer, spans, backward, maxTraversableStep, minTraversableHeight, ref spans[i]))
                        await timeSlicer.Yield();
                }
            }

            return index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CalculateNeighboursLoop<TSide, TYield>(TimeSlicer timeSlicer, ArraySlice<HeightSpan> spans, HeightColumn column, int maxTraversableStep, int minTraversableHeight, ref HeightSpan span)
        {
            // Hack: HeightSpan is immutable for the outside, however this function must initialize (mutate) the struct.
            ref HeightSpanBuilder span_ = ref Unsafe.As<HeightSpan, HeightSpanBuilder>(ref span);
            for (int j = column.First, end = column.Last; j < end; j++)
            {
                if (span_.PresentNeighbour<TSide>(j, spans[j], maxTraversableStep, minTraversableHeight))
                    break;

                if (Toggle.IsToggled<TYield>() && timeSlicer.MustYield())
                    return true;
            }
            return false;
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            columns.Dispose();
            spans.Dispose();
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

                    void Draw2(in VoxelizationParameters parameters_, ArraySlice<HeightSpan> spans, HeightSpan span, int index)
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
            public bool PresentNeighbour<TSide>(int neighbourIndex, HeightSpan neighbourSpan, int maxTraversableStep, int minTraversableHeight)
            {
                Side.DebugAssert<TSide>();
                if (Floor == HeightSpan.NULL_SIDE || neighbourSpan.Floor == HeightSpan.NULL_SIDE)
                    return false;

                if (Math.Abs(Floor - neighbourSpan.Floor) <= maxTraversableStep)
                {
                    if (Ceil == HeightSpan.NULL_SIDE || neighbourSpan.Ceil == HeightSpan.NULL_SIDE || Math.Min(Ceil, neighbourSpan.Ceil) - Math.Max(Floor, neighbourSpan.Floor) >= minTraversableHeight)
                    {
                        int side;
                        if (typeof(TSide) == typeof(Side.Left))
                        {
                            side = Left;
                            Left = neighbourIndex;
                        }
                        else if (typeof(TSide) == typeof(Side.Right))
                        {
                            side = Right;
                            Right = neighbourIndex;
                        }
                        else if (typeof(TSide) == typeof(Side.Forward))
                        {
                            side = Forward;
                            Forward = neighbourIndex;
                        }
                        else if (typeof(TSide) == typeof(Side.Backward))
                        {
                            side = Backward;
                            Backward = neighbourIndex;
                        }
                        else
                            side = 0;
                        Debug.Assert(side == HeightSpan.NULL_SIDE);
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
