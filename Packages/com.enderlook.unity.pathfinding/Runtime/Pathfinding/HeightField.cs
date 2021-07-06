using System;
using System.Buffers;
using System.Runtime.CompilerServices;

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
                Span<HeightSpan> span;
                HeightSpan[] spanOwner;
                if (resolution.Height * Unsafe.SizeOf<HeightSpan>() < sizeof(byte) * 512)
                {
                    spanOwner = null;
                    unsafe
                    {
                        HeightSpan* ptr = stackalloc HeightSpan[resolution.Height];
                        span = new Span<HeightSpan>(ptr, resolution.Height);
                    }
                }
                else
                {
                    spanOwner = ArrayPool<HeightSpan>.Shared.Rent(resolution.Height);
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
                        ArrayPool<HeightSpan>.Shared.Return(spanOwner);
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

        public ReadOnlySpan<HeightColumn> AsSpan() => columns.AsSpan(0, columnsCount);

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
                Debug.Assert(columnsCount == resolution.Cells, $"{parameterName}.{nameof(AsSpan)}.{nameof(Span<HeightColumn>.Length)} must be equal to {resolutionParameterName}.{resolution.Cells}.");
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
                    ReadOnlySpan<HeightSpan> heightSpans = columns[i++].AsSpan();

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
            private Span<HeightSpan> spans;
            private int count;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeightColumnBuilder(Span<HeightSpan> spans)
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
                        spans[count++] = new HeightSpan(isSolid);
                    else
                    {
                        int index = count - 1;
                        if (spans[index].IsSolid == isSolid)
                            spans[index].Height++;
                        else
                            spans[count++] = new HeightSpan(isSolid);
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeightColumn ToBuilt() => new HeightColumn(spans.Slice(0, count));
        }

        public struct HeightColumn : IDisposable
        {
            private HeightSpan[] spans;
            private int count;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeightColumn(Span<HeightSpan> span)
            {
                spans = ArrayPool<HeightSpan>.Shared.Rent(span.Length);
                span.CopyTo(spans.AsSpan());
                count = span.Length;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ReadOnlySpan<HeightSpan> AsSpan() => spans.AsSpan(0, count);

            /// <inheritdoc cref="IDisposable.Dispose"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                HeightSpan[] array = spans;
                spans = null;
                if (!(array is null))
                    ArrayPool<HeightSpan>.Shared.Return(array);
            }
        }

        public struct HeightSpan
        {
            public int Height;
            public bool IsSolid;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeightSpan(bool isSolid)
            {
                Height = 1;
                IsSolid = isSolid;
            }
        }
    }
}