using Enderlook.Collections.Pooled.LowLevel;
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
        /// <param name="voxels">Voxel information of the height field.</param>
        /// <param name="options">Stores configuration information.</param>
        /// <returns>Generated height field.</returns>
        public static async ValueTask<HeightField> Create(ReadOnlyArraySlice<bool> voxels, NavigationGenerationOptions options)
        {
            VoxelizationParameters parameters = options.VoxelizationParameters;
            Debug.Assert(voxels.Length >= parameters.VoxelsCount, $"{nameof(voxels)}.{nameof(voxels.Length)} can't be lower than {nameof(parameters)}.{nameof(parameters.VoxelsCount)}.");

            int xzLength = parameters.ColumnsCount;
            ArraySlice<HeightColumn> columns = new ArraySlice<HeightColumn>(xzLength, false);
            HeightSpan[] spans;
            options.PushTask(parameters.VoxelsCount, "Generating Height Field");
            {
                if (options.UseMultithreading)
                    spans = MultiThread(columns, voxels, options);
                else if (options.ShouldUseTimeSlice)
                    spans = await SingleThread<Toggle.Yes>(columns, voxels, options);
                else
                    spans = await SingleThread<Toggle.No>(columns, voxels, options);
            }
            options.PopTask();
            return new HeightField(columns, spans);
        }

        private static async ValueTask<HeightSpan[]> SingleThread<TYield>(ArraySlice<HeightColumn> columns, ReadOnlyArraySlice<bool> voxels, NavigationGenerationOptions options)
        {
            TimeSlicer timeSlicer = options.TimeSlicer;
            VoxelizationParameters parameters = options.VoxelizationParameters;
            // TODO: Spans could be replaced from type RawPooledList<HeightSpan> to HeightSpan[resolution.Cells] instead.
            RawPooledList<HeightSpan> spans = RawPooledList<HeightSpan>.Create();
            int index = 0;
            for (int x = 0; x < parameters.Width; x++)
            {
                for (int z = 0; z < parameters.Depth; z++)
                {
                    int start = spans.Count;
                    bool added = false;
                    if (Toggle.IsToggled<TYield>())
                    {
                        int y = 0;
                        while (Local(ref spans, x, z, ref added, ref y))
                            await timeSlicer.Yield();
                    }
                    else
                    {
                        for (int y = 0; y < parameters.Height; y++)
                            SingleThread_ProcesssVoxel(options, voxels, parameters, ref spans, x, z, ref added, y);
                    }
                    Debug.Assert(index == parameters.GetIndex(x, z));
                    columns[index++] = new HeightColumn(start, spans.Count - start);
                }
            }
            return spans.UnderlyingArray;

            bool Local(ref RawPooledList<HeightSpan> spans_, int x, int z, ref bool added, ref int y)
            {
                while (true)
                {
                    if (y >= parameters.Height)
                        break;
                    SingleThread_ProcesssVoxel(options, voxels, parameters, ref spans_, x, z, ref added, y++);

                    if (y >= parameters.Height)
                        break;
                    SingleThread_ProcesssVoxel(options, voxels, parameters, ref spans_, x, z, ref added, y++);

                    if (y >= parameters.Height)
                        break;
                    SingleThread_ProcesssVoxel(options, voxels, parameters, ref spans_, x, z, ref added, y++);

                    if (y >= parameters.Height)
                        break;
                    SingleThread_ProcesssVoxel(options, voxels, parameters, ref spans_, x, z, ref added, y++);

                    if (timeSlicer.MustYield())
                        return true;
                }
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SingleThread_ProcesssVoxel(NavigationGenerationOptions options, ReadOnlyArraySlice<bool> voxels, VoxelizationParameters parameters, ref RawPooledList<HeightSpan> spans, int x, int z, ref bool added, int y)
        {
            bool isSolid = voxels[parameters.GetIndex(x, y, z)];
            if (!added)
            {
                spans.Add(new HeightSpan(isSolid));
                added = true;
            }
            else
            {
                ref HeightSpan span = ref spans[spans.Count - 1];
                if (span.IsSolid == isSolid)
                    // HACK: HeightSpan.Height is readonly, however we are still constructing it so we mutate it.
                    Unsafe.AsRef(span.Height)++;
                else
                    spans.Add(new HeightSpan(isSolid));
            }
            options.StepTask();
        }

        private static HeightSpan[] MultiThread(ArraySlice<HeightColumn> columns, ReadOnlyArraySlice<bool> voxels, NavigationGenerationOptions options)
        {
            VoxelizationParameters parameters = options.VoxelizationParameters;
            HeightSpan[] spans = ArrayPool<HeightSpan>.Shared.Rent(parameters.VoxelsCount);
            Parallel.For(0, parameters.ColumnsCount, index =>
            {
                int x = index / parameters.Width;
                int z = index % parameters.Width;

                bool added = false;
                int start = index * parameters.Height;
                int count = start;

                for (int y = 0; y < parameters.Height; y++)
                {
                    bool isSolid = voxels[parameters.GetIndex(x, y, z)];
                    if (!added)
                    {
                        spans[count++] = new HeightSpan(isSolid);
                        added = true;
                    }
                    else
                    {
                        ref HeightSpan span = ref spans[count - 1];
                        if (span.IsSolid == isSolid)
                            Unsafe.AsRef(span.Height)++;
                        else
                            spans[count++] = new HeightSpan(isSolid);
                    }
                    options.StepTask();
                }
                Debug.Assert(index == parameters.GetIndex(x, z));
                columns[index] = new HeightColumn(start, count - start);
            });
            return spans;
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