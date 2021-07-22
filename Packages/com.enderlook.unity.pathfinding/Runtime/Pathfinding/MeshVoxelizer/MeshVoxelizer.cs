using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Unity.Jobs;
using Enderlook.Voxelization;

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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
            {
                stack_.Dispose();
                return;
            }

            if (options.UseMultithreading && stack_.Count > 1)
                await ProcessMultiThread(stack_, voxels, options);
            else
                await ProcessSingleThread(stack_);
        }

        private async ValueTask ProcessSingleThread(RawPooledList<(Vector3[] vertices, int verticesCount, int[] triangles)> stack)
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

                    if (options.HasTimeSlice)
                        await ApplyOffset(options, content, center);
                    else
                    {
                        for (int j = 0; j < content.verticesCount; j++)
                            content.vertices[j] -= center;
                    }

                    Debug.Assert(sizeof(bool) == Unsafe.SizeOf<Voxelizer.VoxelInfo>());
                    Voxelizer.Voxelize(
                        MemoryMarshal.Cast<Vector3, System.Numerics.Vector3>(content.vertices.AsSpan(0, content.verticesCount)),
                        content.triangles,
                        MemoryMarshal.Cast<bool, Voxelizer.VoxelInfo>(voxels_),
                        Unsafe.As<Vector3, System.Numerics.Vector3>(ref size),
                        (resolution.Width, resolution.Height, resolution.Depth)
                    );
                    if (options.CheckIfMustYield())
                        await options.Yield();

                    (Vector3 min, Vector3 max) bounds;
                    if (options.HasTimeSlice)
                        bounds = await CalculateBounds(options, content);
                    else
                    {
                        // Calculate bounds of the mesh.
                        // TODO: we may be calculating this twice.
                        bounds.min = content.vertices[0];
                        bounds.max = content.vertices[0];
                        for (int j = 0; j < content.verticesCount; j++)
                        {
                            Vector3 vertice = content.vertices[j + 0];
                            bounds.min = Vector3.Min(bounds.min, vertice);
                            bounds.max = Vector3.Max(bounds.max, vertice);
                        }
                    }

                    // Fit bounds to global resolution.
                    Vector3 cellSize = resolution.CellSize;

                    int xMinMultiple = Mathf.FloorToInt(bounds.min.x / cellSize.x);
                    int yMinMultiple = Mathf.FloorToInt(bounds.min.y / cellSize.y);
                    int zMinMultiple = Mathf.FloorToInt(bounds.min.z / cellSize.z);

                    int xMaxMultiple = Mathf.CeilToInt(bounds.max.x / cellSize.x);
                    int yMaxMultiple = Mathf.CeilToInt(bounds.max.y / cellSize.y);
                    int zMaxMultiple = Mathf.CeilToInt(bounds.max.z / cellSize.z);

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

                    if (options.HasTimeSlice)
                        await Or<MeshGenerationOptions.WithYield>(options, resolution, voxels, voxels_, xMinMultiple, yMinMultiple, zMinMultiple, xMaxMultiple, yMaxMultiple, zMaxMultiple);
                    else
                        await Or<MeshGenerationOptions.WithoutYield>(options, resolution, voxels, voxels_, xMinMultiple, yMinMultiple, zMinMultiple, xMaxMultiple, yMaxMultiple, zMaxMultiple);

                    voxels_.AsSpan(0, voxelsLength).Clear();

                    if (options.StepTaskAndCheckIfMustYield())
                        await options.Yield();
                }
                ArrayPool<bool>.Shared.Return(voxels_);
            }
            options.PopTask();
            stack.Dispose();

            async ValueTask ApplyOffset(MeshGenerationOptions options, (Vector3[] vertices, int verticesCount, int[] triangles) content, Vector3 center)
            {
                const int unroll = 16;
                int j = 0;
                for (; j < content.verticesCount; j += unroll)
                {
                    // TODO: Is fine this loop unrolling? The idea is to rarely check the yield.

                    content.vertices[j + 0] -= center;
                    content.vertices[j + 1] -= center;
                    content.vertices[j + 2] -= center;
                    content.vertices[j + 3] -= center;
                    content.vertices[j + 4] -= center;
                    content.vertices[j + 5] -= center;
                    content.vertices[j + 6] -= center;
                    content.vertices[j + 7] -= center;
                    content.vertices[j + 8] -= center;
                    content.vertices[j + 9] -= center;
                    content.vertices[j + 10] -= center;
                    content.vertices[j + 11] -= center;
                    content.vertices[j + 12] -= center;
                    content.vertices[j + 13] -= center;
                    content.vertices[j + 14] -= center;
                    content.vertices[j + 15] -= center;

                    if (options.CheckIfMustYield())
                        await options.Yield();
                }

                for (; j < content.verticesCount; j++)
                    content.vertices[j] -= center;

                if (options.CheckIfMustYield())
                    await options.Yield();
            }

            async ValueTask<(Vector3 min, Vector3 max)> CalculateBounds(MeshGenerationOptions options, (Vector3[] vertices, int verticesCount, int[] triangles) content)
            {
                // Calculate bounds of the mesh.
                // TODO: we may be calculating this twice.
                Vector3 min = content.vertices[0];
                Vector3 max = content.vertices[0];
                const int unroll = 16;
                int j = 0;
                for (; j < content.verticesCount; j += unroll)
                {
                    // TODO: Is fine this loop unrolling? The idea is to rarely check the yield.

                    Vector3 vertice = content.vertices[j + 0];
                    min = Vector3.Min(min, vertice);
                    max = Vector3.Max(max, vertice);

                    vertice = content.vertices[j + 1];
                    min = Vector3.Min(min, vertice);
                    max = Vector3.Max(max, vertice);

                    vertice = content.vertices[j + 2];
                    min = Vector3.Min(min, vertice);
                    max = Vector3.Max(max, vertice);

                    vertice = content.vertices[j + 3];
                    min = Vector3.Min(min, vertice);
                    max = Vector3.Max(max, vertice);

                    vertice = content.vertices[j + 4];
                    min = Vector3.Min(min, vertice);
                    max = Vector3.Max(max, vertice);

                    vertice = content.vertices[j + 5];
                    min = Vector3.Min(min, vertice);
                    max = Vector3.Max(max, vertice);

                    vertice = content.vertices[j + 6];
                    min = Vector3.Min(min, vertice);
                    max = Vector3.Max(max, vertice);

                    vertice = content.vertices[j + 7];
                    min = Vector3.Min(min, vertice);
                    max = Vector3.Max(max, vertice);

                    vertice = content.vertices[j + 8];
                    min = Vector3.Min(min, vertice);
                    max = Vector3.Max(max, vertice);

                    vertice = content.vertices[j + 9];
                    min = Vector3.Min(min, vertice);
                    max = Vector3.Max(max, vertice);

                    vertice = content.vertices[j + 10];
                    min = Vector3.Min(min, vertice);
                    max = Vector3.Max(max, vertice);

                    vertice = content.vertices[j + 11];
                    min = Vector3.Min(min, vertice);
                    max = Vector3.Max(max, vertice);

                    vertice = content.vertices[j + 12];
                    min = Vector3.Min(min, vertice);
                    max = Vector3.Max(max, vertice);

                    vertice = content.vertices[j + 13];
                    min = Vector3.Min(min, vertice);
                    max = Vector3.Max(max, vertice);

                    vertice = content.vertices[j + 14];
                    min = Vector3.Min(min, vertice);
                    max = Vector3.Max(max, vertice);

                    vertice = content.vertices[j + 15];
                    min = Vector3.Min(min, vertice);
                    max = Vector3.Max(max, vertice);

                    if (options.CheckIfMustYield())
                        await options.Yield();
                }

                for (; j < content.verticesCount; j++)
                {
                    Vector3 vertice = content.vertices[j];
                    min = Vector3.Min(min, vertice);
                    max = Vector3.Max(max, vertice);
                }

                if (options.CheckIfMustYield())
                    await options.Yield();

                return (min, max);
            }

            async ValueTask Or<TYield>(MeshGenerationOptions options, Resolution resolution, bool[] voxels, bool[] voxels_, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)
            {
                Voxelizer.VoxelInfo[] voxelsInfo = Unsafe.As<Voxelizer.VoxelInfo[]>(voxels_);
                int index = resolution.Depth * (resolution.Height * xMinMultiple);
                for (int x = xMinMultiple; x < xMaxMultiple; x++)
                {
                    index += resolution.Depth * yMinMultiple;
                    for (int y = yMinMultiple; y < yMaxMultiple; y++)
                    {
                        index += zMinMultiple;
                        int z = zMinMultiple;

                        if (MeshGenerationOptions.UseYields<TYield>())
                        {
                            const int unroll = 16;
                            int iTotal = (zMaxMultiple - z) / unroll;
                            for (int i = 0; i < iTotal; i++)
                            {
                                Debug.Assert(index + 0 == resolution.GetIndex(x, y, z + 0));
                                voxels[index + 0] |= voxelsInfo[index + 0].Fill;

                                Debug.Assert(index + 1 == resolution.GetIndex(x, y, z + 1));
                                voxels[index + 1] |= voxelsInfo[index + 1].Fill;

                                Debug.Assert(index + 2 == resolution.GetIndex(x, y, z + 2));
                                voxels[index + 2] |= voxelsInfo[index + 2].Fill;

                                Debug.Assert(index + 2 == resolution.GetIndex(x, y, z + 2));
                                voxels[index + 2] |= voxelsInfo[index + 2].Fill;

                                Debug.Assert(index + 3 == resolution.GetIndex(x, y, z + 3));
                                voxels[index + 3] |= voxelsInfo[index + 3].Fill;

                                Debug.Assert(index + 5 == resolution.GetIndex(x, y, z + 5));
                                voxels[index + 5] |= voxelsInfo[index + 5].Fill;

                                Debug.Assert(index + 6 == resolution.GetIndex(x, y, z + 6));
                                voxels[index + 6] |= voxelsInfo[index + 6].Fill;

                                Debug.Assert(index + 7 == resolution.GetIndex(x, y, z + 7));
                                voxels[index + 7] |= voxelsInfo[index + 7].Fill;

                                Debug.Assert(index + 8 == resolution.GetIndex(x, y, z + 8));
                                voxels[index + 8] |= voxelsInfo[index + 8].Fill;

                                Debug.Assert(index + 9 == resolution.GetIndex(x, y, z + 9));
                                voxels[index + 9] |= voxelsInfo[index + 9].Fill;

                                Debug.Assert(index + 10 == resolution.GetIndex(x, y, z + 10));
                                voxels[index + 10] |= voxelsInfo[index + 10].Fill;

                                Debug.Assert(index + 11 == resolution.GetIndex(x, y, z + 11));
                                voxels[index + 11] |= voxelsInfo[index + 11].Fill;

                                Debug.Assert(index + 12 == resolution.GetIndex(x, y, z + 12));
                                voxels[index + 12] |= voxelsInfo[index + 12].Fill;

                                Debug.Assert(index + 13 == resolution.GetIndex(x, y, z + 13));
                                voxels[index + 13] |= voxelsInfo[index + 13].Fill;

                                Debug.Assert(index + 14 == resolution.GetIndex(x, y, z + 14));
                                voxels[index + 14] |= voxelsInfo[index + 14].Fill;

                                Debug.Assert(index + 15 == resolution.GetIndex(x, y, z + 15));
                                voxels[index + 15] |= voxelsInfo[index + 15].Fill;

                                z += unroll;
                                index += unroll;

                                if (options.CheckIfMustYield())
                                    await options.Yield();
                            }
                        }

                        for (; z < zMaxMultiple; z++, index++)
                        {
                            Debug.Assert(index == resolution.GetIndex(x, y, z));
                            voxels[index] |= voxelsInfo[index].Fill;
                        }

                        if (options.CheckIfMustYield<TYield>())
                            await options.Yield();

                        index += resolution.Depth - zMaxMultiple;
                    }
                    index += resolution.Depth * (resolution.Height - yMaxMultiple);
                }
            }
        }

        private static async ValueTask ProcessMultiThread(RawPooledList<(Vector3[] vertices, int verticesCount, int[] triangles)> stack, bool[] voxels, MeshGenerationOptions options)
        {
            options.PushTask(stack.Count + 1, "Voxelizing Mesh");
            {
                Resolution resolution = options.Resolution;
                using (BlockingCollection<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)> orJobs = new BlockingCollection<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)>(stack.Count))
                {
                    Task task = Task.Run(() =>
                    {
                        // TODO: This part could also be parallelized in case too many tasks are enqueued.
                        while (!orJobs.IsAddingCompleted)
                        {
                            (bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple) tuple = orJobs.Take();
                            OrMultithread(resolution, voxels, tuple);
                            ArrayPool<bool>.Shared.Return(tuple.voxels);
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
                            OrMultithread(resolution, voxels, tuple);
                            ArrayPool<bool>.Shared.Return(tuple.voxels);
                            options.StepTask();
                        }
                    }
                }
                stack.Dispose();
                options.StepTask();
            }
            options.PopTask();
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
