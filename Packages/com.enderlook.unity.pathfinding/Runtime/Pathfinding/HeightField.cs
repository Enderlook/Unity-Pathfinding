using Enderlook.Collections.Pooled.LowLevel;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using UnityEditor;

using UnityEngine;

using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Enderlook.Unity.Pathfinding2
{
    /// <summary>
    /// Represent the height field of a voxelization.
    /// </summary>
    internal readonly struct HeightField : IDisposable
    {
        private readonly HeightColumn[] columns;
        private readonly int columnsCount;
        private readonly HeightSpan[] spans;

        /// <summary>
        /// Columns of this height field.
        /// </summary>
        public ReadOnlySpan<HeightColumn> Columns {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => columns.AsSpan(0, columnsCount);
        }

        public HeightField(HeightColumn[] columns, int columnsCount, HeightSpan[] spans)
        {
            this.columns = columns;
            this.columnsCount = columnsCount;
            this.spans = spans;
        }

        /// <summary>
        /// Creates a new height field.
        /// </summary>
        /// <param name="voxels">Voxel information of the height field.</param>
        /// <param name="options">Stores configuration information.</param>
        /// <returns>Generated height field.</returns>
        public static async ValueTask<HeightField> Create(Memory<bool> voxels, MeshGenerationOptions options)
        {
            options.Validate();
            Resolution resolution = options.Resolution;
            int xzLength = resolution.Cells2D;
            Debug.Assert(voxels.Length >= resolution.Cells, $"{nameof(voxels)}.{nameof(voxels.Length)} can't be lower than {nameof(resolution)}.{nameof(resolution.Cells)}.");

            HeightColumn[] columns = ArrayPool<HeightColumn>.Shared.Rent(xzLength);
            try
            {
                options.PushTask(resolution.Cells, "Generating Height Field");
                HeightSpan[] spans;
                {
                    if (options.UseMultithreading)
                        spans = MultiThread(columns, voxels, options);
                    else
                        spans = await SingleThread(columns, voxels, options);
                }
                options.PopTask();
                return new HeightField(columns, xzLength, spans);
            }
            catch
            {
                ArrayPool<HeightColumn>.Shared.Return(columns);
                throw;
            }
        }

        private static async ValueTask<HeightSpan[]> SingleThread(HeightColumn[] columns, Memory<bool> voxels, MeshGenerationOptions options)
        {
            Resolution resolution = options.Resolution;
            // TODO: spans could be replaced from type RawPooledList<HeightSpan> to HeightSpan[resolution.Cells] instead.
            RawPooledList<HeightSpan> spans = RawPooledList<HeightSpan>.Create();
            try
            {
                int index = 0;
                for (int x = 0; x < resolution.Width; x++)
                {
                    for (int z = 0; z < resolution.Depth; z++)
                    {
                        int start = spans.Count;
                        SingleThreadWork(resolution, voxels, options, ref spans, x, z);
                        if (options.StepTaskAndCheckIfMustYield())
                           await options.Yield();;

                        Debug.Assert(index == resolution.GetIndex(x, z));
                        columns[index++] = new HeightColumn(start, spans.Count - start);
                    }
                }
                return spans.UnderlyingArray;
            }
            catch
            {
                spans.Dispose();
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SingleThreadWork(in Resolution resolution, Memory<bool> voxels, MeshGenerationOptions options, ref RawPooledList<HeightSpan> spans, int x, int z)
        {
            bool added = false;
            Span<bool> voxels_ = voxels.Span;
            for (int y = 0; y < resolution.Height; y++)
            {
                bool isSolid = voxels_[resolution.GetIndex(x, y, z)];
                if (!added)
                {
                    spans.Add(new HeightSpan(isSolid));
                    added = true;
                }
                else
                {
                    ref HeightSpan span = ref spans[spans.Count - 1];
                    if (span.IsSolid == isSolid)
                        Unsafe.AsRef(span.Height)++;
                    else
                        spans.Add(new HeightSpan(isSolid));
                }
                options.StepTask();
            }
        }

        private static HeightSpan[] MultiThread(HeightColumn[] columns, Memory<bool> voxels, MeshGenerationOptions options)
        {
            Resolution resolution = options.Resolution;
            HeightSpan[] spans = ArrayPool<HeightSpan>.Shared.Rent(resolution.Cells);
            try
            {
                Parallel.For(0, resolution.Cells2D, index =>
                {
                    int x = index / resolution.Width;
                    int z = index % resolution.Width;

                    Span<bool> voxels_ = voxels.Span;
                    bool added = false;
                    int start = index * resolution.Height;
                    int count = start;

                    for (int y = 0; y < resolution.Height; y++)
                    {
                        options.StepTask();

                        bool isSolid = voxels_[resolution.GetIndex(x, y, z)];
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
                    }
                    Debug.Assert(index == resolution.GetIndex(x, z));
                    columns[index] = new HeightColumn(start, count - start);
                });
                return spans;
            }
            catch
            {
                ArrayPool<HeightSpan>.Shared.Return(spans);
                throw;
            }
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            ArrayPool<HeightColumn>.Shared.Return(columns);
            ArrayPool<HeightSpan>.Shared.Return(spans);
        }

        /// <summary>
        /// Debug assert that this instance is valid.
        /// </summary>
        /// <param name="parameterName">Name of the instance.</param>
        [System.Diagnostics.Conditional("Debug")]
        public void DebugAssert(string parameterName, in Resolution resolution, string resolutionParameterName)
        {
            Debug.Assert(!(columns is null), $"{parameterName} is default");
            if (!(columns is null))
                Debug.Assert(columnsCount == resolution.Cells, $"{parameterName} iss not valid for passed resolution {resolutionParameterName}.");
        }

        public void DrawGizmos(in Resolution resolution, bool drawOpen)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(resolution.Center, new Vector3(resolution.CellSize.x * resolution.Width, resolution.CellSize.y * resolution.Height, resolution.CellSize.z * resolution.Depth));
            Vector3 offset = (new Vector3(resolution.Width * (-resolution.CellSize.x), resolution.Height * (-resolution.CellSize.y), resolution.Depth * (-resolution.CellSize).z) * .5f) + (resolution.CellSize * .5f);
            offset.y -= resolution.CellSize.y / 2;

            HeightColumn[] columns = this.columns;

            int i = 0;
            for (int x = 0; x < resolution.Width; x++)
            {
                for (int z = 0; z < resolution.Depth; z++)
                {
                    Vector2 position_ = new Vector2(x * resolution.CellSize.x, z * resolution.CellSize.z);
                    ReadOnlySpan<HeightSpan> heightSpans = columns[i++].Spans(this);

                    int y = 0;
                    for (int j = 0; j < heightSpans.Length; j++)
                    {
                        HeightSpan heightSpan = heightSpans[j];
                        Vector3 position = new Vector3(position_.x, resolution.CellSize.y * (y + (heightSpan.Height / 2f)), position_.y);
                        Vector3 center_ = offset + position + resolution.Center;
                        Vector3 size = new Vector3(resolution.CellSize.x, resolution.CellSize.y * heightSpan.Height, resolution.CellSize.z);
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