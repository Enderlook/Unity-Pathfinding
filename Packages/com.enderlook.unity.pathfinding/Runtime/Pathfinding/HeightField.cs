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
    internal struct HeightField : IDisposable
    {
        private readonly HeightColumn[] columns;
        public readonly (int x, int y, int z) Resolution;

        /// <summary>
        /// Creates a new height field.
        /// </summary>
        /// <param name="voxels">Voxel information of the height field.</param>
        /// <param name="resolution">Resolution of the voxelization.</param>
        public HeightField(Span<bool> voxels, (int x, int y, int z) resolution)
        {
            if (resolution.x < 1 || resolution.y < 1 || resolution.z < 1)
                throw new ArgumentOutOfRangeException(nameof(resolution), $"{nameof(resolution)} values can't be lower than 1.");

            int xzLength = resolution.x * resolution.z;
            if (voxels.Length < xzLength * resolution.y)
                throw new ArgumentOutOfRangeException(nameof(voxels), $"Length can't be lower than {nameof(resolution)}.{nameof(resolution.x)} * {nameof(resolution)}.{nameof(resolution.y)} * {nameof(resolution)}.{nameof(resolution.z)}");

            columns = ArrayPool<HeightColumn>.Shared.Rent(xzLength);
            try
            {
                Resolution = resolution;
                Span<HeightSpan> span;
                HeightSpan[] spanOwner;
                if (resolution.y * Unsafe.SizeOf<HeightSpan>() < sizeof(byte) * 512)
                {
                    spanOwner = null;
                    unsafe
                    {
                        HeightSpan* ptr = stackalloc HeightSpan[resolution.y];
                        span = new Span<HeightSpan>(ptr, resolution.y);
                    }
                }
                else
                {
                    spanOwner = ArrayPool<HeightSpan>.Shared.Rent(resolution.y);
                    span = spanOwner;
                }

                try
                {
                    int index = 0;
                    for (int x = 0; x < resolution.x; x++)
                    {
                        for (int z = 0; z < resolution.z; z++)
                        {
                            HeightColumnBuilder column = new HeightColumnBuilder(span);
                            for (int y = 0; y < resolution.y; y++)
                                column.Grow(voxels[GetIndex(x, y, z)]);

                            Debug.Assert(index == GetIndex(x, z));
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetIndex(int x, int y, int z)
        {
            int index = (Resolution.z * ((Resolution.y * x) + y)) + z;
            Debug.Assert(index < Resolution.x * Resolution.y * Resolution.z);
            return index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetIndex(int x, int z)
        {
            int index = (Resolution.z * x) + z;
            Debug.Assert(index < Resolution.x * Resolution.z);
            return index;
        }

        public ReadOnlySpan<HeightColumn> AsSpan() => columns.AsSpan(0, Resolution.x * Resolution.y);

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            int length = Resolution.x * Resolution.z;
            for (int i = 0; i < length; i++)
                columns[i].Dispose();
            ArrayPool<HeightColumn>.Shared.Return(columns);
        }

        public void DrawGizmos(Vector3 center, Vector3 cellSize, bool drawOpen)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(center, new Vector3(cellSize.x * Resolution.x, cellSize.y * Resolution.y, cellSize.z * Resolution.z));
            Vector3 offset = (new Vector3(Resolution.x * (-cellSize.x), Resolution.y * (-cellSize.y), Resolution.z * (-cellSize).z) * .5f) + (cellSize * .5f);
            offset.y -= cellSize.y / 2;

            int i = 0;
            for (int x = 0; x < Resolution.x; x++)
            {
                for (int z = 0; z < Resolution.z; z++)
                {
                    Vector2 position_ = new Vector2(x * cellSize.x, z * cellSize.z);
                    ReadOnlySpan<HeightSpan> heightSpans = columns[i++].AsSpan();

                    int y = 0;
                    for (int j = 0; j < heightSpans.Length; j++)
                    {
                        HeightSpan heightSpan = heightSpans[j];
                        Vector3 position = new Vector3(position_.x, cellSize.y * (y + (heightSpan.Height / 2f)), position_.y);
                        Vector3 center_ = offset + position + center;
                        Vector3 size = new Vector3(cellSize.x, cellSize.y * heightSpan.Height, cellSize.z);
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