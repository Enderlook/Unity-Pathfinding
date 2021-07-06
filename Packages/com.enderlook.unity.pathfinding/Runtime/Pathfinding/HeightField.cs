using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
        public HeightField(Span<bool> voxels, in Resolution resolution)
        {
            resolution.DebugAssert(nameof(resolution));

            int xzLength = resolution.Width * resolution.Depth;
            Debug.Assert(voxels.Length >= xzLength * resolution.Height, $"{nameof(voxels)}.{nameof(voxels.Length)} can't be lower than {nameof(resolution)}.{nameof(resolution.Width)} * {nameof(resolution)}.{nameof(resolution.Height)} * {nameof(resolution)}.{nameof(resolution.Depth)}");

            columnsCount = xzLength;
            columns = ArrayPool<HeightColumn>.Shared.Rent(xzLength);
            try
            {
                Span<HeightColumnBuilder.HeightSpanBuilder> span;
                HeightColumnBuilder.HeightSpanBuilder[] spanOwner;
                if (resolution.Height * Unsafe.SizeOf<HeightColumnBuilder.HeightSpanBuilder>() < sizeof(byte) * 512)
                {
                    spanOwner = null;
                    unsafe
                    {
                        HeightColumnBuilder.HeightSpanBuilder* ptr = stackalloc HeightColumnBuilder.HeightSpanBuilder[resolution.Height];
                        span = new Span<HeightColumnBuilder.HeightSpanBuilder>(ptr, resolution.Height);
                    }
                }
                else
                {
                    spanOwner = ArrayPool<HeightColumnBuilder.HeightSpanBuilder>.Shared.Rent(resolution.Height);
                    span = spanOwner;
                }

                try
                {
                    int index = 0;
                    for (int x = 0; x < resolution.Width; x++)
                    {
                        for (int z = 0; z < resolution.Depth; z++)
                        {
                            HeightColumnBuilder column = new HeightColumnBuilder(span);
                            for (int y = 0; y < resolution.Height; y++)
                                column.Grow(voxels[resolution.GetIndex(x, y, z)]);

                            Debug.Assert(index == resolution.GetIndex(x, z));
                            columns[index++] = column.ToBuilt();
                        }
                    }
                }
                finally
                {
                    if (!(spanOwner is null))
                        ArrayPool<HeightColumnBuilder.HeightSpanBuilder>.Shared.Return(spanOwner);
                }
            }
            catch
            {
                for (int i = 0; i < xzLength; i++)
                    columns[i].Dispose();

                ArrayPool<HeightColumn>.Shared.Return(columns);
                throw;
            }
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            for (int i = 0; i < columnsCount; i++)
                columns[i].Dispose();
            ArrayPool<HeightColumn>.Shared.Return(columns);
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
                Debug.Assert(columnsCount == resolution.Cells, $"{parameterName}.{nameof(Columns)}.{nameof(Span<HeightColumn>.Length)} must be equal to {resolutionParameterName}.{nameof(resolution.Cells)}.");
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
                    ReadOnlySpan<HeightSpan> heightSpans = columns[i++].Spans;

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

        private ref struct HeightColumnBuilder
        {
            private Span<HeightSpanBuilder> spans;
            private int count;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeightColumnBuilder(Span<HeightSpanBuilder> spans)
            {
                this.spans = spans;
                count = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Grow(bool isSolid)
            {
                unsafe
                {
                    if (count == 0)
                        spans[count++] = new HeightSpanBuilder(isSolid);
                    else
                    {
                        int index = count - 1;
                        if (spans[index].IsSolid == isSolid)
                            spans[index].Height++;
                        else
                            spans[count++] = new HeightSpanBuilder(isSolid);
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeightColumn ToBuilt() => new HeightColumn(MemoryMarshal.Cast<HeightSpanBuilder, HeightSpan>(spans.Slice(0, count)));
            public struct HeightSpanBuilder
            {
                public int Height;
                public bool IsSolid;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public HeightSpanBuilder(bool isSolid)
                {
                    Height = 1;
                    IsSolid = isSolid;
                }
            }
        }

        /// <summary>
        /// Represent the column of a <see cref="HeightField"/>.
        /// </summary>
        public readonly struct HeightColumn : IDisposable
        {
            private readonly HeightSpan[] spans;
            private readonly int count;

            /// <summary>
            /// Spans of this column.
            /// </summary>
            public ReadOnlySpan<HeightSpan> Spans {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => spans.AsSpan(0, count);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeightColumn(Span<HeightSpan> span)
            {
                spans = ArrayPool<HeightSpan>.Shared.Rent(span.Length);
                span.CopyTo(spans.AsSpan());
                count = span.Length;
            }

            /// <inheritdoc cref="IDisposable.Dispose"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose() => ArrayPool<HeightSpan>.Shared.Return(spans);
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