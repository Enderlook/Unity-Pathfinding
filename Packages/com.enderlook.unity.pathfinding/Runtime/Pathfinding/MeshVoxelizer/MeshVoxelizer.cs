using Enderlook.Collections.Pooled;
using Enderlook.Collections.Pooled.LowLevel;
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
    internal partial struct MeshVoxelizer : IDisposable
    {
        private readonly (int x, int y, int z) resolution;
        private readonly Bounds bounds;
        private readonly bool[] voxels;
        private readonly MeshGenerationOptions options;
        private List<Vector3> vertices;

        /// <summary>
        /// Voxelization result.
        /// </summary>
        public Memory<bool> Voxels => voxels.AsMemory(0, VoxelsLength);

        /// <summary>
        /// Lenght of <see cref="Voxels"/>;
        /// </summary>
        public int VoxelsLength => resolution.x * resolution.y * resolution.z;

        /// <summary>
        /// Size of each voxel.
        /// </summary>
        public Vector3 VoxelSize {
            get {
                Vector3 size = bounds.size;
                return new Vector3(size.x / resolution.x, size.y / resolution.y, size.z / resolution.z);
            }
        }

        private RawPooledList<(Vector3[] vertices, int verticesCount, int[] triangles)> stack;

        /// <summary>
        /// Creates a new voxelizer of meshes.
        /// </summary>
        /// <param name="resolution">Resolution of the voxelization.</param>
        /// <param name="bounds">Bounds where meshes are voxelized.</param>
        public MeshVoxelizer((int x, int y, int z) resolution, Bounds bounds, MeshGenerationOptions options)
        {
            this.resolution = resolution;
            this.bounds = bounds;
            this.options = options;
            int length = resolution.x * resolution.y * resolution.z;
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

            int voxelsLength = VoxelsLength;
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;

            if (options.UseMultithreading && stack_.Count > 1)
                await ProcessMultiThread(stack_, voxelsLength, center, size, resolution, voxels);
            else
                await ProcessSingleThread(stack_);
        }

        private async ValueTask ProcessSingleThread(RawPooledList<(Vector3[] vertices, int verticesCount, int[] triangles)> stack)
        {
            int count = stack.Count;
            options.PushTask(count);

            int voxelsLength = VoxelsLength;
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;

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
                    resolution
                );

                // TODO: This can be optimized to only copy relevant voxels instead of the whole array.
                OrSingleThread(voxels, voxels_, voxelsLength);
                voxels_.AsSpan(0, voxelsLength).Clear();

                options.StepTask();
                if (options.MustYield())
                    await Task.Yield();
            }

            stack.Dispose();
            options.PopTask();
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

        private static async ValueTask ProcessMultiThread(RawPooledList<(Vector3[] vertices, int verticesCount, int[] triangles)> stack, int voxelsLength, Vector3 center, Vector3 size, (int x, int y, int z) resolution, bool[] voxels)
        {
            using (BlockingCollection<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)> orJobs = new BlockingCollection<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)>(stack.Count))
            {
                Task task = Task.Run(() =>
                {
                    // TODO: This part could also be parallelized in case too many task are enqueued.
                    while (!orJobs.IsCompleted)
                    {
                        (bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple) tuple = orJobs.Take();
                        try
                        {
                            resolution = OrMultithread(resolution, voxels, tuple);
                        }
                        finally
                        {
                            ArrayPool<bool>.Shared.Return(tuple.voxels);
                        }
                    }
                });
                Parallel.For(0, stack.Count, i => VoxelizeMultithreadSlave(resolution, size, center, voxelsLength, orJobs, stack, i));
                orJobs.CompleteAdding();
                await task;
            }
        }

        private static void VoxelizeMultithreadSlave(
            (int x, int y, int z) resolution, Vector3 size, Vector3 center, int voxelsLength,
            BlockingCollection<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)> orJobs,
            RawPooledList<(Vector3[] vertices, int verticesCount, int[] triangles)> stack, int i)
        {
            (Vector3[] vertices, int verticesCount, int[] triangles) content = stack[i];

            if (unchecked((uint)content.verticesCount > (uint)content.vertices.Length))
            {
                Debug.Assert(false, "Index out of range.");
                return;
            }

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

            bool[] voxels = ArrayPool<bool>.Shared.Rent(voxelsLength);
            Span<Voxelizer.VoxelInfo> voxelsInfo = MemoryMarshal.Cast<bool, Voxelizer.VoxelInfo>(voxels);

            Voxelizer.Voxelize(
                MemoryMarshal.Cast<Vector3, System.Numerics.Vector3>(content.vertices.AsSpan(0, content.verticesCount)),
                content.triangles,
                voxelsInfo,
                Unsafe.As<Vector3, System.Numerics.Vector3>(ref size),
                resolution
            );

            int index = resolution.z * (resolution.y * xMinMultiple);
            for (int x = xMinMultiple; x < xMaxMultiple; x++)
            {
                index += resolution.z * yMinMultiple;
                for (int y = yMinMultiple; y < yMaxMultiple; y++)
                {
                    index += zMinMultiple;
                    for (int z = zMinMultiple; z < zMaxMultiple; z++)
                    {
                        Debug.Assert(index == GetIndex(ref resolution, x, y, z));
                        voxels[index] = voxelsInfo[index].Fill;
                        index++;
                    }
                    index += resolution.z - zMaxMultiple;
                }
                index += resolution.z * (resolution.y - yMaxMultiple);
            }

            orJobs.Add((voxels, xMinMultiple, yMinMultiple, zMinMultiple, xMaxMultiple, yMaxMultiple, zMaxMultiple));
        }

        private static (int x, int y, int z) OrMultithread((int x, int y, int z) resolution, bool[] voxels, (bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple) tuple)
        {
            int index = resolution.z * (resolution.y * tuple.xMinMultiple);
            for (int x = tuple.xMinMultiple; x < tuple.xMaxMultiple; x++)
            {
                index += resolution.z * tuple.yMinMultiple;
                for (int y = tuple.yMinMultiple; y < tuple.yMaxMultiple; y++)
                {
                    index += tuple.zMinMultiple;
                    for (int z = tuple.zMinMultiple; z < tuple.zMaxMultiple; z++)
                    {
                        Debug.Assert(index == GetIndex(ref resolution, x, y, z));
                        voxels[index] |= tuple.voxels[index];
                        index++;
                    }
                    index += resolution.z - tuple.zMaxMultiple;
                }
                index += resolution.z * (resolution.y - tuple.yMaxMultiple);
            }

            return resolution;
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            stack.Dispose();
            ArrayPool<bool>.Shared.Return(voxels);
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
