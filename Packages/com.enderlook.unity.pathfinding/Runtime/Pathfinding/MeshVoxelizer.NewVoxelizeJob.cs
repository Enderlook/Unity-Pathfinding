using Enderlook.Collections.Pooled;
using Enderlook.Unity.Jobs;
using Enderlook.Voxelization;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    internal partial struct MeshVoxelizer
    {
        private struct FinalizeJob : IManagedJob
        {
            private PooledStack<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)> stack;

            public FinalizeJob(PooledStack<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)> stack)
                => this.stack = stack;

            public void Execute() => stack.Dispose();
        }

        private struct NewOrJob : IManagedJob
        {
            private bool[] destination;
            private (int x, int y, int z) resolution;
            private PooledStack<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)> stack;

            public NewOrJob(bool[] destination, (int x, int y, int z) resolution, PooledStack<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)> stack)
            {
                this.stack = stack;
                this.destination = destination;
                this.resolution = resolution;
            }

            public void Execute()
            {
                (bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple) tuple;

                lock (stack)
                    tuple = stack.Pop();

                try
                {
                    for (int x = tuple.xMinMultiple; x < tuple.xMaxMultiple; x++)
                    {
                        for (int y = tuple.yMinMultiple; y < tuple.yMaxMultiple; y++)
                        {
                            for (int z = tuple.zMinMultiple; z < tuple.zMaxMultiple; z++)
                            {
                                int i = GetIndex(ref resolution, x, y, z);
                                destination[i] |= tuple.voxels[i];
                            }
                        }
                    }
                }
                finally
                {
                    ArrayPool<bool>.Shared.Return(tuple.voxels);
                }
            }
        }

        private struct NewVoxelizeJob : IManagedJob
        {
            private PooledStack<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)> stack;
            private (int x, int y, int z) resolution;
            private int voxelsCount;
            private Memory<Vector3> vertices;
            private int[] triangles;
            private Vector3 center;
            private Vector3 size;

            public NewVoxelizeJob(PooledStack<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)> stack, (int x, int y, int z) resolution, int voxelsCount, Memory<Vector3> vertices, int[] triangles, Vector3 center, Vector3 size)
            {
                this.stack = stack;
                this.resolution = resolution;
                this.voxelsCount = voxelsCount;
                this.vertices = vertices;
                this.triangles = triangles;
                this.center = center;
                this.size = size;
            }

            public void A()
            {
                Span<Vector3> vertices_ = vertices.Span;
                for (int j = 0; j < vertices.Length; j++)
                    vertices_[j] -= center;

                // Calculate bounds of the mesh.
                // TODO: we may be calculating this twice.
                Vector3 min = vertices_[0];
                Vector3 max = vertices_[0];
                for (int i = 0; i < vertices_.Length; i++)
                {
                    Vector3 vertice = vertices_[i];
                    min = Vector3.Min(min, vertice);
                    max = Vector3.Max(max, vertice);
                }

                //min = Vector3.Max(min, center - size);
                //max = Vector3.Min(max, center + size);

                Gizmos.color = Color.red;
                Gizmos.DrawLine(min, max);

                // Fit bounds to global resolution
                Vector3 cellSize = new Vector3(size.x / resolution.x, size.y / resolution.y, size.z / resolution.z);

                int xMinMultiple = Mathf.FloorToInt(min.x / cellSize.x);
                int yMinMultiple = Mathf.FloorToInt(min.y / cellSize.y);
                int zMinMultiple = Mathf.FloorToInt(min.z / cellSize.z);

                int xMaxMultiple = Mathf.CeilToInt(max.x / cellSize.x);
                int yMaxMultiple = Mathf.CeilToInt(max.y / cellSize.y);
                int zMaxMultiple = Mathf.CeilToInt(max.z / cellSize.z);

                Gizmos.color = Color.yellow;
                Vector3 min_ = center + new Vector3(xMinMultiple * cellSize.x, yMinMultiple * cellSize.y, zMinMultiple * cellSize.z);
                Vector3 max_ = center + new Vector3(xMaxMultiple * cellSize.x, yMaxMultiple * cellSize.y, zMaxMultiple * cellSize.z);
                Vector3 size_ = max_ - min_;
                Gizmos.DrawWireCube(
                    min_ + (size_ / 2),
                    size_
                );

                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(min_, max_);

                // Fix offset
                xMinMultiple += resolution.x / 2;
                yMinMultiple += resolution.y / 2;
                zMinMultiple += resolution.z / 2;
                xMaxMultiple += resolution.x / 2;
                yMaxMultiple += resolution.y / 2;
                zMaxMultiple += resolution.z / 2;

                // Clamp values because a part of the mesh may be outside the voxelization area.
                xMinMultiple = Mathf.Max(xMinMultiple, 0);
                yMinMultiple = Mathf.Max(yMinMultiple, 0);
                zMinMultiple = Mathf.Max(zMinMultiple, 0);
                xMaxMultiple = Mathf.Min(xMaxMultiple, resolution.x);
                yMaxMultiple = Mathf.Min(yMaxMultiple, resolution.y);
                zMaxMultiple = Mathf.Min(zMaxMultiple, resolution.z);

                Debug.Log($"({xMinMultiple}, {yMinMultiple}, {zMinMultiple}) ({xMaxMultiple}, {yMaxMultiple}, {zMaxMultiple}) ({size.x / cellSize.x} {size.y / cellSize.y} {size.z / cellSize.z})");
            }

            public void Execute()
            {
                Span<Vector3> vertices_ = vertices.Span;
                for (int j = 0; j < vertices.Length; j++)
                    vertices_[j] -= center;

                // Calculate bounds of the mesh.
                // TODO: we may be calculating this twice.
                Vector3 min = vertices_[0];
                Vector3 max = vertices_[0];
                for (int i = 0; i < vertices_.Length; i++)
                {
                    Vector3 vertice = vertices_[i];
                    min = Vector3.Min(min, vertice);
                    max = Vector3.Max(max, vertice);
                }

                //min = Vector3.Max(min, center - size);
                //max = Vector3.Min(max, center + size);

                // Fit bounds to global resolution
                Vector3 cellSize = new Vector3(size.x / resolution.x, size.y / resolution.y, size.z / resolution.z);

                int xMinMultiple = Mathf.FloorToInt(min.x / cellSize.x);
                int yMinMultiple = Mathf.FloorToInt(min.y / cellSize.y);
                int zMinMultiple = Mathf.FloorToInt(min.z / cellSize.z);

                int xMaxMultiple = Mathf.CeilToInt(max.x / cellSize.x);
                int yMaxMultiple = Mathf.CeilToInt(max.y / cellSize.y);
                int zMaxMultiple = Mathf.CeilToInt(max.z / cellSize.z);

                // Fix offset
                xMinMultiple += resolution.x / 2;
                yMinMultiple += resolution.y / 2;
                zMinMultiple += resolution.z / 2;
                xMaxMultiple += resolution.x / 2;
                yMaxMultiple += resolution.y / 2;
                zMaxMultiple += resolution.z / 2;

                // Clamp values because a part of the mesh may be outside the voxelization area.
                xMinMultiple = Mathf.Max(xMinMultiple, 0);
                yMinMultiple = Mathf.Max(yMinMultiple, 0);
                zMinMultiple = Mathf.Max(zMinMultiple, 0);
                xMaxMultiple = Mathf.Min(xMaxMultiple, resolution.x);
                yMaxMultiple = Mathf.Min(yMaxMultiple, resolution.y);
                zMaxMultiple = Mathf.Min(zMaxMultiple, resolution.z);

                bool[] voxels = ArrayPool<bool>.Shared.Rent(voxelsCount);
                Span<Voxelizer.VoxelInfo> voxelsInfo = MemoryMarshal.Cast<bool, Voxelizer.VoxelInfo>(voxels);

                Voxelizer.Voxelize(
                    MemoryMarshal.Cast<Vector3, System.Numerics.Vector3>(vertices_),
                    triangles,
                    voxelsInfo,
                    Unsafe.As<Vector3, System.Numerics.Vector3>(ref size),
                    resolution
                );

                for (int x = xMinMultiple; x < xMaxMultiple; x++)
                {
                    for (int y = yMinMultiple; y < yMaxMultiple; y++)
                    {
                        for (int z = zMinMultiple; z < zMaxMultiple; z++)
                        {
                            int i = GetIndex(ref resolution, x, y, z);
                            voxels[i] = voxelsInfo[i].Fill;
                        }
                    }
                }

                lock (stack)
                    stack.Push((voxels, xMinMultiple, yMinMultiple, zMinMultiple, xMaxMultiple, yMaxMultiple, zMaxMultiple));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetIndex(ref (int x, int y, int z) resolution, int x, int y, int z)
        {
            Debug.Assert(x >= 0);
            Debug.Assert(x < resolution.x);
            Debug.Assert(z >= 0);
            Debug.Assert(z < resolution.z);
            Debug.Assert(y >= 0);
            int index = (resolution.z * ((resolution.y * x) + y)) + z;
            Debug.Assert(index < resolution.x * resolution.y * resolution.z);
            return index;
        }
    }
}
