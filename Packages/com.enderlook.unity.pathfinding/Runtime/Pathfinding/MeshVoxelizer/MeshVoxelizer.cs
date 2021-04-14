using Enderlook.Collections.Pooled;
using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Unity.Jobs;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
        private List<Vector3> vertices;

        /// <summary>
        /// Voxelization result.
        /// </summary>
        public Span<bool> Voxels => voxels.AsSpan(0, VoxelsLength);

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

        private RawPooledList<(Memory<Vector3> vertices, int[] triangles)> stack;

        /// <summary>
        /// Creates a new voxelizer of meshes.
        /// </summary>
        /// <param name="resolution">Resolution of the voxelization.</param>
        /// <param name="bounds">Bounds where meshes are voxelized.</param>
        public MeshVoxelizer((int x, int y, int z) resolution, Bounds bounds)
        {
            this.resolution = resolution;
            this.bounds = bounds;
            int length = resolution.x * resolution.y * resolution.z;
            voxels = ArrayPool<bool>.Shared.Rent(length);
            Array.Clear(voxels, 0, length);
            vertices = new List<Vector3>();
            stack = RawPooledList<(Memory<Vector3> vertices, int[] triangles)>.Create();
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

            stack.Add((vertices_.AsMemory(0, count), triangles));
        }

        /// <summary>
        /// Voxelizes all enqueued meshes.
        /// </summary>
        public JobHandle Process()
        {
            RawPooledList<(Memory<Vector3> vertices, int[] triangles)> stack_ = stack;
            stack = RawPooledList<(Memory<Vector3> vertices, int[] triangles)>.Create();

            if (stack_.Count == 0)
                return default;

            int voxelsLength = VoxelsLength;
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;

            if (Application.platform == RuntimePlatform.WebGLPlayer || stack_.Count == 1)
                return new SimpleJob(resolution, stack_, voxelsLength, center, size, voxels).Schedule();
            else
            {
                PooledStack<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)> orJobs = new PooledStack<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)>(stack_.Count);
                JobHandle[] handles = ArrayPool<JobHandle>.Shared.Rent(stack_.Count);
                try
                {
                    for (int i = 0; i < stack_.Count; i++)
                    {
                        (Memory<Vector3> vertices, int[] triangles) = stack_[i];
                        handles[i] = new VoxelizeJob(orJobs, resolution, voxelsLength, vertices, triangles, center, size).Schedule();
                    }

                    JobHandle lastJob = default;
                    JobHandle _ = handles[stack_.Count - 1];
                    for (int i = 0; i < stack_.Count; i++)
                    {
                        lastJob = new OrJob(voxels, resolution, orJobs).Schedule(JobHandle.CombineDependencies(lastJob, handles[i]));
                    }

                    return new FinalizeJob(orJobs).Schedule(lastJob);
                }
                finally
                {
                    ArrayPool<JobHandle>.Shared.Return(handles);
                }
            }
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
