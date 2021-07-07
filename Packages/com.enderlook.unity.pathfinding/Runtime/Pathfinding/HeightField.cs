using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Unity.Jobs;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

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

        /// <summary>
        /// Creates a new height field.
        /// </summary>
        /// <param name="voxels">Voxel information of the height field.</param>
        /// <param name="resolution">Resolution of the voxelization.</param>
        public HeightField(Memory<bool> voxels, in Resolution resolution)
        {
            resolution.DebugAssert(nameof(resolution));

            int xzLength = resolution.Width * resolution.Depth;
            Debug.Assert(voxels.Length >= xzLength * resolution.Height, $"{nameof(voxels)}.{nameof(voxels.Length)} can't be lower than {nameof(resolution)}.{nameof(resolution.Width)} * {nameof(resolution)}.{nameof(resolution.Height)} * {nameof(resolution)}.{nameof(resolution.Depth)}");

            columnsCount = xzLength;
            columns = ArrayPool<HeightColumn>.Shared.Rent(xzLength);
            try
            {
                RawPooledList<HeightSpan> spans = RawPooledList<HeightSpan>.Create();
                try
                {
                    if (Application.platform == RuntimePlatform.WebGLPlayer || SystemInfo.processorCount == 1)
                    {
                        Span<bool> voxels_ = voxels.Span;
                        int index = 0;
                        for (int x = 0; x < resolution.Width; x++)
                        {
                            for (int z = 0; z < resolution.Depth; z++)
                            {
                                int start = spans.Count;
                                bool added = false;
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
                                }

                                Debug.Assert(index == resolution.GetIndex(x, z));
                                columns[index++] = new HeightColumn(start, spans.Count - start);
                            }
                        }
                        this.spans = spans.UnderlyingArray;
                    }
                    else
                    {
                        HeightColumn[] columns = this.columns;
                        Task task;
                        using (AutoResetEvent @event = new AutoResetEvent(false))
                        {
                            task = Task.Run(() =>
                            {
                                int total = 0;
                                for (int i = 0; i < xzLength; i++)
                                {
                                    @event.WaitOne();
                                    Debug.Log($"M {i}");
                                    ref HeightColumn column = ref columns[i];
                                    int start = total;
                                    int count = column.Store(ref spans, total);
                                    column = new HeightColumn(start, count);
                                    total += count;
                                }
                            });

                            Resolution resolution_ = resolution;
                            int groups = SystemInfo.processorCount; // Can only be called from main thread.
                            Parallel.For(0, xzLength, i =>
                            {
                                // Redistribute index to improve locality due Parallel.For redistribution.
                                int groupLength = Mathf.CeilToInt(xzLength / (float)groups);
                                int group = i / groupLength + 1;
                                int groupStart = (i / groupLength) * groupLength;
                                int index = group - 1 + (i - groupStart) * groups;

                                int x = index / resolution_.Width;
                                int z = index % resolution_.Width;

                                Span<bool> voxels_ = voxels.Span;
                                bool added = false;
                                int count = 0;

                                HeightSpan[] spans_ = ArrayPool<HeightSpan>.Shared.Rent(resolution_.Height);
                                try
                                {
                                    for (int y = 0; y < resolution_.Height; y++)
                                    {
                                        bool isSolid = voxels_[resolution_.GetIndex(x, y, z)];
                                        if (!added)
                                        {
                                            spans_[count++] = new HeightSpan(isSolid);
                                            added = true;
                                        }
                                        else
                                        {
                                            ref HeightSpan span = ref spans_[count - 1];
                                            if (span.IsSolid == isSolid)
                                                Unsafe.AsRef(span.Height)++;
                                            else
                                                spans_[count++] = new HeightSpan(isSolid);
                                        }
                                    }
                                    Debug.Assert(index == resolution_.GetIndex(x, z));
                                    columns[index] = new HeightColumn(spans_, count);
                                    Debug.Log($"S {index}");
                                    @event.Set();
                                }
                                catch
                                {
                                    ArrayPool<HeightSpan>.Shared.Return(spans_);
                                }
                            });
                            while (!task.IsCompleted)
                                @event.Set();
                        }
                        Task.WaitAll(task);
                        this.spans = spans.UnderlyingArray;
                    }
                }
                catch
                {
                    spans.Dispose();
                    throw;
                }
            }
            catch
            {
                ArrayPool<HeightColumn>.Shared.Return(columns);
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
            private readonly HeightSpan[] spans;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeightColumn(int start, int count)
            {
                this.start = start;
                this.count = count;
                spans = default;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeightColumn(HeightSpan[] spans, int count)
            {
                this.spans = spans;
                this.count = count;
                start = default;
            }

            /// <summary>
            /// Spans of this column.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ReadOnlySpan<HeightSpan> Spans(in HeightField heightField) => heightField.spans.AsSpan(start, count);

            public int Store(ref RawPooledList<HeightSpan> spans, int count)
            {
                HeightSpan[] current;
                do
                {
                    current = this.spans;
                } while (current is null);
                spans.AddRange(current.AsSpan(0, this.count));
                ArrayPool<HeightSpan>.Shared.Return(current);
                return this.count;
            }
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