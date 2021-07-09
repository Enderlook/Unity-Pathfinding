using Enderlook.Collections.Pooled;
using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Enumerables;
using Enderlook.Unity.Jobs;
using Enderlook.Voxelization;

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Unity.Jobs;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    /// <summary>
    /// A voxelizer of multiple meshes.
    /// </summary>
    internal struct MeshVoxelizer : IDisposable
    {
        private readonly bool[] voxels;
        private readonly MeshGenerationOptions options;
        private List<Vector3> vertices;

        /// <summary>
        /// Voxelization result.
        /// </summary>
        public Memory<bool> Voxels => voxels.AsMemory(0, options.Resolution.Cells);

        private RawPooledList<(Vector3[] vertices, int verticesCount, int[] triangles)> stack;

        /// <summary>
        /// Creates a new voxelizer of meshes.
        /// </summary>
        /// <param name="options">Stores configuration information.</param>
        public MeshVoxelizer(MeshGenerationOptions options)
        {
            options.Validate();
            this.options = options;
            int length = options.Resolution.Cells;
            voxels = ArrayPool<bool>.Shared.Rent(length);
            Array.Clear(voxels, 0, length);
            vertices = new List<Vector3>();
            stack = RawPooledList<(Vector3[] vertices, int verticesCount, int[] triangles)>.Create();
        }

        /// <summary>
        /// Enqueues a mesh to be voxelized when <see cref="Process"/> is executed.
        /// </summary>
        /// <param name="meshFilter">Mesh to voxelize.</param>
        public void Enqueue(MeshFilter meshFilter)
        {
            // TODO: On Unity 2020 use Mesh.AcquireReadOnlyMeshData for zero-allocation.

            Transform transform = meshFilter.transform;
            Mesh mesh = meshFilter.sharedMesh;

            vertices.Clear();
            mesh.GetVertices(vertices);

            Vector3[] vertices_ = ArrayPool<Vector3>.Shared.Rent(vertices.Count);

            int count = vertices.Count;
            for (int i = 0; i < count; i++)
                vertices_[i] = transform.TransformPoint(vertices[i]);

            int[] triangles = mesh.triangles;

            stack.Add((vertices_, count, triangles));
        }

        /// <summary>
        /// Voxelizes all enqueued meshes.
        /// </summary>
        public async ValueTask Process()
        {
            RawPooledList<(Vector3[] vertices, int verticesCount, int[] triangles)> stack_ = stack;
            stack = RawPooledList<(Vector3[] vertices, int verticesCount, int[] triangles)>.Create();

            if (stack_.Count == 0)
                return;

            if (options.UseMultithreading && stack_.Count > 1)
                await ProcessMultiThread(stack_, voxels, options);
            else
                await ProcessSingleThread(stack_);
        }

        private async ValueTask ProcessSingleThread(RawPooledList<(Vector3[] vertices, int verticesCount, int[] triangles)> stack)
        {
            try
            {
                int count = stack.Count;
                options.PushTask(count, "Voxelizing Mesh");
                {
                    Resolution resolution = options.Resolution;
                    int voxelsLength = resolution.Cells;
                    Vector3 center = resolution.Center;
                    Vector3 size = resolution.Size;

                    bool[] voxels_ = ArrayPool<bool>.Shared.Rent(voxelsLength);

                    for (int i = 0; i < count; i++)
                    {
                        (Vector3[] vertices, int verticesCount, int[] triangles) content = stack[i];

                        if (unchecked((uint)content.verticesCount > (uint)content.vertices.Length))
                        {
                            Debug.Assert(false, "Index out of range.");
                            return;
                        }

                        for (int j = 0; j < content.verticesCount; j++)
                            content.vertices[j] -= center;

                        Voxelizer.Voxelize(
                            MemoryMarshal.Cast<Vector3, System.Numerics.Vector3>(content.vertices.AsSpan(0, content.verticesCount)),
                            content.triangles,
                            voxels_,
                            Unsafe.As<Vector3, System.Numerics.Vector3>(ref size),
                            (resolution.Width, resolution.Height, resolution.Depth)
                        );

                        // TODO: This can be optimized to only copy relevant voxels instead of the whole array.
                        OrSingleThread(voxels, voxels_, voxelsLength);
                        voxels_.AsSpan(0, voxelsLength).Clear();

                        if (options.StepTaskAndCheckIfMustYield())
                            await options.Yield();
                    }
                }
                options.PopTask();
            }
            finally
            {
                stack.Dispose();
            }
        }

        private static void OrSingleThread(bool[] a, bool[] b, int count)
        {
            Debug.Assert(count > 0);
            Debug.Assert(a.Length >= count);
            Debug.Assert(b.Length >= count);

            // This check allow the jitter to remove bound checks in the loop.
            if (a.Length < count || b.Length < count || count == 0)
            {
                Debug.Assert(false, "Impossible state.");
                return;
            }

            Span<System.Numerics.Vector<byte>> a_ = MemoryMarshal.Cast<bool, System.Numerics.Vector<byte>>(a.AsSpan(0, count));
            Span<System.Numerics.Vector<byte>> b_ = MemoryMarshal.Cast<bool, System.Numerics.Vector<byte>>(b.AsSpan(0, count));

            // This check allow the jitter to remove bound checks in the loop.
            if (a_.Length != b_.Length)
            {
                Debug.Assert(false, "Impossible state.");
                return;
            }

            for (int i = 0; i < a_.Length; i++)
                a_[i] |= b_[i];

            for (int i = count / System.Numerics.Vector<byte>.Count; i < count; i++)
                a[i] |= b[i];
        }

        private static async ValueTask ProcessMultiThread(RawPooledList<(Vector3[] vertices, int verticesCount, int[] triangles)> stack, bool[] voxels, MeshGenerationOptions options)
        {
            try
            {
                options.PushTask(stack.Count + 1, "Voxelizing Mesh");
                {
                    Resolution resolution = options.Resolution;
                    using (BlockingCollection<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)> orJobs = new BlockingCollection<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)>(stack.Count))
                    {
                        Task task = Task.Run(() =>
                        {
                        // TODO: This part could also be parallelized in case too many task are enqueued.
                        while (!orJobs.IsAddingCompleted)
                            {
                                (bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple) tuple = orJobs.Take();
                                try
                                {
                                    OrMultithread(resolution, voxels, tuple);
                                }
                                finally
                                {
                                    ArrayPool<bool>.Shared.Return(tuple.voxels);
                                }
                            }
                        });
                        Parallel.For(0, stack.Count, i =>
                        {
                            VoxelizeMultithreadSlave(resolution, orJobs, stack, i);
                            options.StepTask();
                        });
                        orJobs.CompleteAdding();
                        await task;
                        if (orJobs.Count > 0)
                        {
                            options.PushTask(stack.Count, stack.Count - orJobs.Count, "Merging Voxelized Meshes");
                            // Continue merging on current thread.
                            while (!orJobs.IsCompleted)
                            {
                                (bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple) tuple = orJobs.Take();
                                try
                                {
                                    OrMultithread(resolution, voxels, tuple);
                                }
                                finally
                                {
                                    ArrayPool<bool>.Shared.Return(tuple.voxels);
                                }
                                options.StepTask();
                            }
                        }
                    }
                    options.StepTask();
                }
                options.PopTask();
            }
            finally
            {
                stack.Dispose();
            }
        }

        private static void VoxelizeMultithreadSlave(
            in Resolution resolution,
            BlockingCollection<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)> orJobs,
            RawPooledList<(Vector3[] vertices, int verticesCount, int[] triangles)> stack, int i)
        {
            (Vector3[] vertices, int verticesCount, int[] triangles) content = stack[i];

            if (unchecked((uint)content.verticesCount > (uint)content.vertices.Length))
            {
                Debug.Assert(false, "Index out of range.");
                return;
            }

            Vector3 center = resolution.Center;
            for (int j = 0; j < content.verticesCount; j++)
                content.vertices[j] -= center;

            // Calculate bounds of the mesh.
            // TODO: we may be calculating this twice.
            Vector3 min = content.vertices[0];
            Vector3 max = content.vertices[0];
            for (int j = 0; j < content.vertices.Length; j++)
            {
                Vector3 vertice = content.vertices[j];
                min = Vector3.Min(min, vertice);
                max = Vector3.Max(max, vertice);
            }

            // Fit bounds to global resolution.
            Vector3 cellSize = resolution.CellSize;

            int xMinMultiple = Mathf.FloorToInt(min.x / cellSize.x);
            int yMinMultiple = Mathf.FloorToInt(min.y / cellSize.y);
            int zMinMultiple = Mathf.FloorToInt(min.z / cellSize.z);

            int xMaxMultiple = Mathf.CeilToInt(max.x / cellSize.x);
            int yMaxMultiple = Mathf.CeilToInt(max.y / cellSize.y);
            int zMaxMultiple = Mathf.CeilToInt(max.z / cellSize.z);

            // Fix offset
            xMinMultiple += resolution.Width / 2;
            yMinMultiple += resolution.Height / 2;
            zMinMultiple += resolution.Depth / 2;
            xMaxMultiple += resolution.Width / 2;
            yMaxMultiple += resolution.Height / 2;
            zMaxMultiple += resolution.Depth / 2;

            // Clamp values because a part of the mesh may be outside the voxelization area.
            xMinMultiple = Mathf.Max(xMinMultiple, 0);
            yMinMultiple = Mathf.Max(yMinMultiple, 0);
            zMinMultiple = Mathf.Max(zMinMultiple, 0);
            xMaxMultiple = Mathf.Min(xMaxMultiple, resolution.Width);
            yMaxMultiple = Mathf.Min(yMaxMultiple, resolution.Height);
            zMaxMultiple = Mathf.Min(zMaxMultiple, resolution.Depth);

            bool[] voxels = ArrayPool<bool>.Shared.Rent(resolution.Cells);
            Span<Voxelizer.VoxelInfo> voxelsInfo = MemoryMarshal.Cast<bool, Voxelizer.VoxelInfo>(voxels);

            Vector3 size = resolution.Size;
            Voxelizer.Voxelize(
                MemoryMarshal.Cast<Vector3, System.Numerics.Vector3>(content.vertices.AsSpan(0, content.verticesCount)),
                content.triangles,
                voxelsInfo,
                Unsafe.As<Vector3, System.Numerics.Vector3>(ref size),
                (resolution.Width, resolution.Height, resolution.Depth)
            );

            int index = resolution.Depth * (resolution.Height * xMinMultiple);
            for (int x = xMinMultiple; x < xMaxMultiple; x++)
            {
                index += resolution.Depth * yMinMultiple;
                for (int y = yMinMultiple; y < yMaxMultiple; y++)
                {
                    index += zMinMultiple;
                    for (int z = zMinMultiple; z < zMaxMultiple; z++)
                    {
                        Debug.Assert(index == resolution.GetIndex(x, y, z));
                        voxels[index] = voxelsInfo[index].Fill;
                        index++;
                    }
                    index += resolution.Depth - zMaxMultiple;
                }
                index += resolution.Depth * (resolution.Height - yMaxMultiple);
            }

            orJobs.Add((voxels, xMinMultiple, yMinMultiple, zMinMultiple, xMaxMultiple, yMaxMultiple, zMaxMultiple));
        }

        private static void OrMultithread(in Resolution resolution, bool[] voxels, (bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple) tuple)
        {
            int index = resolution.Depth * (resolution.Height * tuple.xMinMultiple);
            for (int x = tuple.xMinMultiple; x < tuple.xMaxMultiple; x++)
            {
                index += resolution.Depth * tuple.yMinMultiple;
                for (int y = tuple.yMinMultiple; y < tuple.yMaxMultiple; y++)
                {
                    index += tuple.zMinMultiple;
                    for (int z = tuple.zMinMultiple; z < tuple.zMaxMultiple; z++)
                    {
                        Debug.Assert(index == resolution.GetIndex(x, y, z));
                        voxels[index] |= tuple.voxels[index];
                        index++;
                    }
                    index += resolution.Depth - tuple.zMaxMultiple;
                }
                index += resolution.Depth * (resolution.Height - tuple.yMaxMultiple);
            }
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            stack.Dispose();
            ArrayPool<bool>.Shared.Return(voxels);
        }
    }
}
