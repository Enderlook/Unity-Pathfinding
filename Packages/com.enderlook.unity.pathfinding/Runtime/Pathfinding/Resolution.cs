using System;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    /// <summary>
    /// Represent the resolution of a voxelization.
    /// </summary>
    public readonly struct Resolution
    {
        /// <summary>
        /// X-axis width.
        /// </summary>
        public readonly int Width;

        /// <summary>
        /// Y-axis width.
        /// </summary>
        public readonly int Height;

        /// <summary>
        /// Z-axis width.
        /// </summary>
        public readonly int Depth;

        /// <summary>
        /// Center point.
        /// </summary>
        public readonly Vector3 Center;

        /// <summary>
        /// Total size (not half-extents).
        /// </summary>
        public readonly Vector3 Size;

        /// <summary>
        /// Size of each voxel.
        /// </summary>
        public Vector3 CellSize {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new Vector3(Size.x / Width, Size.y / Height, Size.z / Depth);
        }

        /// <summary>
        /// Amounts of cells the resolution has (<c><see cref="Width"/> * <see cref="Height"/> * <see cref="Depth"/></c>).
        /// </summary>
        internal int Cells {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Width * Height * Depth;
        }

        /// <summary>
        /// Amounts of 2d cells the resolution has (<c><see cref="Width"/> * <see cref="Depth"/></c>).
        /// </summary>
        internal int Cells2D {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Width * Depth;
        }

        /// <summary>
        /// Creates a new resolution.
        /// </summary>
        /// <param name="width">Width (X-axis) of the resolution in voxels.</param>
        /// <param name="height">Height (Y-axis) of the resolution in voxels.</param>
        /// <param name="depth">Depth (Z-axis) of the resolution in voxels.</param>
        /// <param name="center">Center point of the resulution in world space.</param>
        /// <param name="size">Size of the resolution in world space.</param>
        public Resolution(int width, int height, int depth, Vector3 center, Vector3 size)
        {
            if (width < 1 || height < 1 || depth < 1 || size.x <= 0 || size.y <= 0)
                Throw();

            Width = width;
            Height = height;
            Depth = depth;
            Center = center;
            Size = size;

            void Throw()
            {
                if (width < 1)
                    throw new ArgumentOutOfRangeException(nameof(width), "Can't be lower than 1");
                if (height < 1)
                    throw new ArgumentOutOfRangeException(nameof(height), "Can't be lower than 1");
                if (depth < 1)
                    throw new ArgumentOutOfRangeException(nameof(depth), "Can't be lower than 1");
                if (size.x <= 0)
                    throw new ArgumentOutOfRangeException($"{nameof(size)}.{nameof(size.x)}", "Must be positive.");
                throw new ArgumentOutOfRangeException($"{nameof(size)}.{nameof(size.y)}", "Must be positive.");
            }
        }

        /// <summary>
        /// Creates a new resolution.
        /// </summary>
        /// <param name="width">Width (X-axis) of the resolution in voxels.</param>
        /// <param name="height">Height (Y-axis) of the resolution in voxels.</param>
        /// <param name="depth">Depth (Z-axis) of the resolution in voxels.</param>
        /// <param name="bounds">World space bounds of the resolution.</param>
        public Resolution(int width, int height, int depth, Bounds bounds)
        {
            if (width < 1 || height < 1 || depth < 1 || bounds.size.x <= 0 || bounds.size.y <= 0)
                Throw();

            Width = width;
            Height = height;
            Depth = depth;
            Center = bounds.center;
            Size = bounds.size;

            void Throw()
            {
                if (width < 1)
                    throw new ArgumentOutOfRangeException(nameof(width), "Can't be lower than 1");
                if (height < 1)
                    throw new ArgumentOutOfRangeException(nameof(height), "Can't be lower than 1");
                if (depth < 1)
                    throw new ArgumentOutOfRangeException(nameof(depth), "Can't be lower than 1");
                if (bounds.size.x <= 0)
                    throw new ArgumentOutOfRangeException($"{nameof(bounds)}.{nameof(bounds.size)}.{nameof(bounds.size.x)}", "Must be positive.");
                throw new ArgumentOutOfRangeException($"{nameof(bounds)}.{nameof(bounds.size)}.{nameof(bounds.size.y)}", "Must be positive.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ThrowIfDefault()
        {
            if (Width == 0)
                Throw();
            void Throw() => throw new ArgumentException("Is default.", "resolution");
        }

        /// <summary>
        /// Debug assert that this instance is valid.
        /// </summary>
        /// <param name="parameterName">Name of the instance.</param>
        [System.Diagnostics.Conditional("Debug")]
        internal void DebugAssert(string parameterName)
        {
            Debug.Assert(Width >= 0, $"{parameterName}.{nameof(Width)} can't be lower than 1");
            Debug.Assert(Height >= 0, $"{parameterName}.{nameof(Height)} can't be lower than 1");
            Debug.Assert(Depth >= 0, $"{parameterName}.{nameof(Depth)} can't be lower than 1");
            Debug.Assert(Size.x >= 0, $"{parameterName}.{nameof(Size)}.{nameof(Size.x)} must be positive");
            Debug.Assert(Size.y >= 0, $"{parameterName}.{nameof(Size)}.{nameof(Size.y)} must be positive");
        }

        /// <summary>
        /// Get the index of an element at the specified coordinates.
        /// </summary>
        /// <param name="x">Value in width.</param>
        /// <param name="y">Value in height.</param>
        /// <param name="z">Value in depth.</param>
        /// <returns>Index at the specified coordinates.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int GetIndex(int x, int y, int z)
        {
            Debug.Assert(x >= 0);
            Debug.Assert(x < Width);
            Debug.Assert(z >= 0);
            Debug.Assert(z < Depth);
            Debug.Assert(y >= 0);
            int index = (Depth * ((Height * x) + y)) + z;
            Debug.Assert(index < Width * Height * Depth);
            return index;
        }

        /// <summary>
        /// Get the index of an element at the specified coordinates by forming a plane using the <see cref="Width"/> and <see cref="Depth"/> axis.
        /// </summary>
        /// <param name="x">Value in width.</param>
        /// <param name="z">Value in depth.</param>
        /// <returns>Index at the specified coordinates.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int GetIndex(int x, int z)
        {
            Debug.Assert(x >= 0);
            Debug.Assert(x < Width);
            Debug.Assert(z >= 0);
            Debug.Assert(z < Depth);
            int index = (Depth * x) + z;
            Debug.Assert(index < Width * Depth);
            return index;
        }
    }
}
