using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    /// <summary>
    /// Represent the resolution of a voxelization.
    /// </summary>
    internal readonly struct Resolution
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
        public Vector3 CellSize => new Vector3(Size.x / Width, Size.y / Height, Size.z / Depth);

        public Resolution(int width, int height, int depth, Vector3 center, Vector3 size)
        {
            Width = width;
            Height = height;
            Depth = depth;
            Center = center;
            Size = size;
        }

        public Resolution(int width, int height, int depth, Bounds bounds)
        {
            Width = width;
            Height = height;
            Depth = depth;
            Center = bounds.center;
            Size = bounds.size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetIndex(int x, int y, int z)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetIndex(int x, int z)
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
