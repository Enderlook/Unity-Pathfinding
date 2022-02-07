using Enderlook.Pools;
using Enderlook.Unity.Pathfinding.Utils;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using UnityEngine;

using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Enderlook.Unity.Pathfinding.Generation
{
    /// <summary>
    /// Represent the height field of a voxelization.
    /// </summary>
    internal readonly struct HeightField : IDisposable
    {
        private readonly ArraySlice<HeightColumn> columns;
        private readonly HeightSpan[] spans;

        /// <summary>
        /// Columns of this height field.
        /// </summary>
        public ReadOnlySpan<HeightColumn> Columns {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => columns;
        }

        public HeightField(ArraySlice<HeightColumn> columns, HeightSpan[] spans)
        {
            this.columns = columns;
            this.spans = spans;
        }

        /// <summary>
        /// Creates a new height field.
        /// </summary>
        /// <param name="options">Stores configuration information.</param>
        /// <param name="voxels">Voxel information of the height field.</param>
        /// <returns>Generated height field.</returns>
        public static async ValueTask<HeightField> Create(NavigationGenerationOptions options, ReadOnlyArraySlice<bool> voxels)
        {
            VoxelizationParameters parameters = options.VoxelizationParameters;
            Debug.Assert(voxels.Length >= parameters.VoxelsCount, $"{nameof(voxels)}.{nameof(voxels.Length)} can't be lower than {nameof(parameters)}.{nameof(parameters.VoxelsCount)}.");

            int xzLength = parameters.ColumnsCount;
            ArraySlice<HeightColumn> columns = new ArraySlice<HeightColumn>(xzLength, false);
            HeightSpan[] spans;
            options.PushTask(parameters.VoxelsCount, "Generating Height Field");
            {
                if (options.UseMultithreading)
                    spans = MultiThread.Calculate(options, voxels, columns).Array;
                else if (options.ShouldUseTimeSlice)
                    spans = (await SingleThread<Toggle.Yes>(options, voxels, columns)).Array;
                else
                    spans = (await SingleThread<Toggle.No>(options, voxels, columns)).Array;
            }
            options.PopTask();
            return new HeightField(columns, spans);
        }

        private static async ValueTask<ArraySlice<HeightSpan>> SingleThread<TYield>(NavigationGenerationOptions options, ReadOnlyArraySlice<bool> voxels, ArraySlice<HeightColumn> columns)
        {
            TimeSlicer timeSlicer = options.TimeSlicer;
            VoxelizationParameters parameters = options.VoxelizationParameters;
            // TODO: Spans could be replaced from type RawPooledList<HeightSpan> to HeightSpan[resolution.Cells] instead.
            ArraySlice<HeightSpan> spans = new ArraySlice<HeightSpan>(parameters.VoxelsCount, false);
            int spansCount = 0;
            int indexColumn = 0;
            for (int x = 0; x < parameters.Width; x++)
            {
                for (int z = 0; z < parameters.Depth; z++)
                {
                    // TODO: This may produce error if Height is 1 or 0.

                    int start = spansCount;
                    int index = parameters.GetIndex(x, 0, z);

                    bool isSolid = voxels[index];
                    spans[spansCount++] = new HeightSpan(isSolid);
                    options.StepTask();
                    int y = 1;
                    index += parameters.Depth;

                    if (Toggle.IsToggled<TYield>())
                        await timeSlicer.Yield();

                    int end = index + ((parameters.Height - 2) * parameters.Depth);
                    Debug.Assert(end == parameters.GetIndex(x, parameters.Height - 1, z));
                    while (ProcessColumn<TYield>(options, timeSlicer, parameters, voxels, spans, ref spansCount, x, z, ref y, ref index, end))
                        await timeSlicer.Yield();
                    Debug.Assert(indexColumn == parameters.GetIndex(x, z));
                    columns[indexColumn++] = new HeightColumn(start, spansCount - start);
                }
            }
            return spans;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ProcessColumn<TYield>(NavigationGenerationOptions options, TimeSlicer timeSlicer, in VoxelizationParameters parameters, in ReadOnlyArraySlice<bool> voxels, in ArraySlice<HeightSpan> spans, ref int spansCount, int x, int z, ref int y, ref int index, int end)
        {
            ref bool startVoxel = ref Unsafe.AsRef(voxels[index]);
            ref bool voxel = ref startVoxel;
            ref HeightSpan startSpan = ref spans[spansCount - 1];
            ref HeightSpan span = ref startSpan;
            ref bool end_ = ref Unsafe.Add(ref voxel, end - index + 1);
#if DEBUG
            int i = index;
            int y_ = y;
            int s = spansCount;
#endif

            // TODO: Apply loop unrolling.
            while (Unsafe.IsAddressLessThan(ref voxel, ref end_))
            {
                bool isSolid = voxel;
                if (span.IsSolid == isSolid)
                    // HACK: HeightSpan.Height is readonly, however we are still constructing it so we mutate it.
                    Unsafe.AsRef(span.Height)++;
                else
                {
#if DEBUG
                    s++;
                    Debug.Assert(spans.Length > s);
#endif
                    span = ref Unsafe.Add(ref span, 1);
                    span = new HeightSpan(isSolid);
                }
#if DEBUG
                Debug.Assert(y_ < parameters.Height && i == parameters.GetIndex(x, y_, z));
                y_++;
                i += parameters.Depth;
#endif
                voxel = ref Unsafe.Add(ref voxel, parameters.Depth);
                options.StepTask();

                if (Toggle.IsToggled<TYield>() && timeSlicer.MustYield())
                {
                    int offset = MathHelper.IndexesTo(ref startVoxel, ref voxel);
                    index += offset;
                    y += offset / parameters.Depth;
                    spansCount += MathHelper.IndexesTo(ref startSpan, ref span);
#if DEBUG
                    Debug.Assert(i == index && y_ == y && s == spansCount);
#endif
                    return true;
                }
            }

            {
                index += MathHelper.IndexesTo(ref startVoxel, ref voxel);
                spansCount += MathHelper.IndexesTo(ref startSpan, ref span);
#if DEBUG
                Debug.Assert(y_ == parameters.Height && i == index && s == spansCount);
#endif
            }

            return false;
        }

        private sealed class MultiThread
        {
            private readonly Action<int> action;
            private NavigationGenerationOptions options;
            private ReadOnlyArraySlice<bool> voxels;
            private ArraySlice<HeightColumn> columns;
            private ArraySlice<HeightSpan> spans;

            public MultiThread() => action = Process;

            public static ArraySlice<HeightSpan> Calculate(NavigationGenerationOptions options, ReadOnlyArraySlice<bool> voxels, ArraySlice<HeightColumn> columns)
            {
                VoxelizationParameters parameters = options.VoxelizationParameters;
                ArraySlice<HeightSpan> span = new ArraySlice<HeightSpan>(parameters.VoxelsCount, false);
                ObjectPool<MultiThread> pool = ObjectPool<MultiThread>.Shared;
                MultiThread instance = pool.Rent();
                {
                    instance.options = options;
                    instance.voxels = voxels;
                    instance.columns = columns;
                    instance.spans = span;

                    Parallel.For(0, parameters.ColumnsCount, instance.action);

                    instance.options = default;
                    instance.voxels = default;
                    instance.columns = default;
                    instance.spans = default;
                }
                pool.Return(instance);
                return span;
            }

            private void Process(int columnIndex)
            {
                VoxelizationParameters parameters = options.VoxelizationParameters;

                int x = columnIndex / parameters.Width;
                int z = columnIndex % parameters.Width;

                int start = columnIndex * parameters.Height;
                int spansCount = start;
                int index = parameters.GetIndex(x, 0, z);
                bool isSolid = voxels[index];
                spans[spansCount++] = new HeightSpan(isSolid);
                options.StepTask();
                int y = 1;
                index += parameters.Depth;

                int end = index + ((parameters.Height - 2) * parameters.Depth);
                Debug.Assert(end == parameters.GetIndex(x, parameters.Height - 1, z));
                bool value = ProcessColumn<Toggle.No>(options, options.TimeSlicer, parameters, voxels, spans, ref spansCount, x, z, ref y, ref index, end);
                Debug.Assert(!value && columnIndex == parameters.GetIndex(x, z));
                columns[columnIndex] = new HeightColumn(start, spansCount - start);
            }
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            columns.Dispose();
            ArrayPool<HeightSpan>.Shared.Return(spans);
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
                Debug.Assert(columns.Length == parameters.VoxelsCount, $"{parameterName} is not valid for passed resolution {resolutionParameterName}.");
        }

        public void DrawGizmos(in VoxelizationParameters parameters, bool drawOpen)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(parameters.VolumeCenter, parameters.VolumeSize);
            Vector3 offset = parameters.OffsetAtFloor;

            ArraySlice<HeightColumn> columns = this.columns;

            int i = 0;
            for (int x = 0; x < parameters.Width; x++)
            {
                for (int z = 0; z < parameters.Depth; z++)
                {
                    Vector2 position_ = new Vector2(x * parameters.VoxelSize, z * parameters.VoxelSize);
                    ReadOnlySpan<HeightSpan> heightSpans = columns[i++].Spans(this);

                    int y = 0;
                    for (int j = 0; j < heightSpans.Length; j++)
                    {
                        HeightSpan heightSpan = heightSpans[j];
                        Vector3 position = new Vector3(position_.x, parameters.VoxelSize * (y + (heightSpan.Height / 2f)), position_.y);
                        Vector3 center_ = offset + position;
                        Vector3 size = new Vector3(parameters.VoxelSize, parameters.VoxelSize * heightSpan.Height, parameters.VoxelSize);
                        y += heightSpan.Height;

                        Gizmos.color = heightSpan.IsSolid ? Color.red : Color.green;
                        if (heightSpan.IsSolid || drawOpen)
                            Gizmos.DrawWireCube(center_, size);
                    }
                }
            }
        }

        /// <summary>
        /// Represent the column of a <see cref="HeightField"/>.
        /// </summary>
        public readonly struct HeightColumn
        {
            private readonly int start;
            private readonly int count;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeightColumn(int start, int count)
            {
                this.start = start;
                this.count = count;
            }

            /// <summary>
            /// Spans of this column.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ReadOnlySpan<HeightSpan> Spans(in HeightField heightField) => heightField.spans.AsSpan(start, count);
        }

        /// <summary>
        /// Represent the span of a <see cref="HeightColumn"/>.
        /// </summary>
        public readonly struct HeightSpan
        {
            /// <summary>
            /// Height of this span.
            /// </summary>
            public readonly int Height;

            /// <summary>
            /// Whenever this span is solid.
            /// </summary>
            public readonly bool IsSolid;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeightSpan(bool isSolid)
            {
                Height = 1;
                IsSolid = isSolid;
            }
        }
    }
}