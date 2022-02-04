using Enderlook.Mathematics;

using System;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Generation
{
    /// <summary>
    /// Represent the resolution of a voxelization.
    /// </summary>
    internal readonly struct VoxelizationParameters
    {
        /// <summary>
        /// X-axis width in voxels.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Y-axis width in voxels.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Z-axis width in voxels.
        /// </summary>
        public int Depth { get; }

        /// <summary>
        /// Minimum point of the voxelization.
        /// </summary>
        public Vector3 Min { get; }

        /// <summary>
        /// Maximum point of the voxelization.
        /// </summary>
        public Vector3 Max { get; }

        /// <summary>
        /// Size of the voxelization volume.
        /// </summary>
        public Vector3 VolumeSize {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Max - Min;
        }

        /// <summary>
        /// Center of the voxelization volume.
        /// </summary>
        public Vector3 VolumeCenter {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Max + Min) * .5f;
        }

        /// <summary>
        /// Size of each voxel.
        /// </summary>
        public float VoxelSize { get; }

        /// <summary>
        /// Amounts of voxels the resolution has (<c><see cref="Width"/> * <see cref="Height"/> * <see cref="Depth"/></c>).
        /// </summary>
        public int VoxelsCount {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Width * Height * Depth;
        }

        /// <summary>
        /// Amounts of columns the resolution has (<c><see cref="Width"/> * <see cref="Depth"/></c>).
        /// </summary>
        public int ColumnsCount {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Width * Depth;
        }

        /// <summary>
        /// Bounds of the voxelization.
        /// </summary>
        public Bounds Bounds {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                Bounds value = default;
                value.SetMinMax(Min, Max);
                return value;
            }
        }

        /// <summary>
        /// Offset of the voxel at position (0, 0, 0).
        /// </summary>
        public Vector3 OffsetAtFloor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Vector3 offset = (new Vector3(Width * (-VoxelSize), Height * (-VoxelSize), Depth * (-VoxelSize)) * .5f) + (VoxelSize * .5f * Vector3.one);
                offset.y -= VoxelSize * .5f;
                offset += VolumeCenter;
                return offset;
            }
        }

        private VoxelizationParameters(Vector3 min, Vector3 max, float voxelSize)
        {
            Min = min;
            Max = max;
            VoxelSize = voxelSize;
            Vector3 volumeSize = max - min;

            Vector3 parts = volumeSize / voxelSize;
            Width = Mathf.CeilToInt(parts.x);
            Height = Mathf.CeilToInt(parts.y);
            Depth = Mathf.CeilToInt(parts.z);
        }

        /// <summary>
        /// Creates voxelization parameters.
        /// </summary>
        /// <param name="minHint">Approximate minimum boundary point.</param>
        /// <param name="maxHint">Approximate maximum boundary point.</param>
        /// <param name="voxelSizeHint">Approximate size of each voxel.</param>
        /// <returns>Voxelization parameters.</returns>
        public static VoxelizationParameters WithVoxelSize(Vector3 minHint, Vector3 maxHint, float voxelSizeHint)
        {
            Debug.Assert(voxelSizeHint > 0);
            Debug.Assert(maxHint.x > minHint.x && maxHint.y > minHint.y && maxHint.z > minHint.z);

            // TODO: Math could be improved.

            Vector3 minAnchor = minHint / voxelSizeHint;
            minAnchor = new Vector3(Mathf.Floor(minAnchor.x), Mathf.Floor(minAnchor.y), Mathf.Floor(minAnchor.z)) * voxelSizeHint;
            Vector3 maxAnchor = maxHint / voxelSizeHint;
            maxAnchor = new Vector3(Mathf.Ceil(maxAnchor.x), Mathf.Ceil(maxAnchor.y), Mathf.Ceil(maxAnchor.z)) * voxelSizeHint;

            Vector3 volumeSize = maxAnchor - minAnchor;
            float maxLength = Mathematic.Max(volumeSize.x, volumeSize.y, volumeSize.z);
            int resolution = (int)Mathf.Round(maxLength / voxelSizeHint);

            float voxelSize = maxLength / resolution;

            Vector3 unit = voxelSize * Vector3.one;
            minAnchor -= unit;
            maxAnchor += unit;

            return new VoxelizationParameters(minAnchor, maxAnchor, voxelSize);
        }

        /// <summary>
        /// Get the indexes coordinates of the index in 3D space.
        /// </summary>
        /// <param name="index">Index whose coordinates will be calculated.</param>
        /// <returns>Coordinates of the specified index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3Int From3D(int index)
        {
            int v = Math.DivRem(index, Depth, out int z);
            int x = Math.DivRem(v, Height, out int y);
            Debug.Assert(index < Width * Height * Depth && index == GetIndex(z, y, x));
            return new Vector3Int(x, y, z);
        }

        /// <summary>
        /// Get the indexes coordinates of the index in 2D space using as axies width and depth.
        /// </summary>
        /// <param name="index">Index whose coordinates will be calculated.</param>
        /// <returns>Coordinates of the specified index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2Int From2D(int index)
        {
            Debug.Assert(index < Width * Depth);
            int x = Math.DivRem(index, Depth, out int z);
            return new Vector2Int(x, z);
        }

        /// <summary>
        /// Get the index of an element at the specified coordinates.
        /// </summary>
        /// <param name="x">Value in width.</param>
        /// <param name="y">Value in height.</param>
        /// <param name="z">Value in depth.</param>
        /// <returns>Index at the specified coordinates.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetIndex(int x, int y, int z)
        {
            int index = (Depth * ((Height * x) + y)) + z;
            Debug.Assert(x >= 0 && x < Width && z >= 0 && z < Depth && y >= 0 && index < Width * Height * Depth);
            return index;
        }

        /// <summary>
        /// Get the index of an element at the specified coordinates by forming a plane using the <see cref="Width"/> and <see cref="Depth"/> axis.
        /// </summary>
        /// <param name="x">Value in width.</param>
        /// <param name="z">Value in depth.</param>
        /// <returns>Index at the specified coordinates.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetIndex(int x, int z)
        {
            int index = (Depth * x) + z;
            Debug.Assert(x >= 0 && x < Width && z >= 0 && z < Depth && index < Width * Depth);
            return index;
        }
    }
}
