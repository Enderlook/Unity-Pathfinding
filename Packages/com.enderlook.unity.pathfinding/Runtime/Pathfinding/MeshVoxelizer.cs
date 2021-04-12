using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Unity.Jobs;
using Enderlook.Voxelization;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Unity.Jobs;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    /// <summary>
    /// A voxelizer of multiple meshes.
    /// </summary>
    internal struct MeshVoxelizer : IDisposable
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

            int voxelsLength = VoxelsLength;
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;

            if (Application.platform == RuntimePlatform.WebGLPlayer || stack_.Count == 1)
                return new SimpleJob(resolution, stack_, voxelsLength, center, size, voxels).Schedule();
            else
            {
                StrongBox<(RawPooledStack<bool[]> toProcess, RawPooledStack<bool[]> free)> box = new StrongBox<(RawPooledStack<bool[]> toProcess, RawPooledStack<bool[]> free)>((RawPooledStack<bool[]>.Create(), RawPooledStack<bool[]>.Create()));

                int count = stack_.Count;
                int count_ = count / 2 * 2;
                JobHandle[] handles = ArrayPool<JobHandle>.Shared.Rent(count_ + 1);
                try
                {
                    int handlesIndex = 0;

                    for (int i = 0; i < count_;)
                    {
                        (Memory<Vector3> vertices, int[] triangles) = stack_[i++];
                        JobHandle jobHandle1 = new VoxelizeJob(box, resolution, vertices, triangles, center, size).Schedule();

                        (vertices, triangles) = stack_[i++];
                        JobHandle jobHandle2 = new VoxelizeJob(box, resolution, vertices, triangles, center, size).Schedule();

                        Debug.Assert(handlesIndex <= count_);
                        handles[handlesIndex++] = new OrJob(box, voxelsLength).Schedule(JobHandle.CombineDependencies(jobHandle1, jobHandle2));
                    }

                    int c = handlesIndex;
                    while (c > 1)
                    {
                        int c_ = c / 2 * 2;
                        int i = 0;
                        for (int j = 0; j < c_;)
                            handles[i++] = new OrJob(box, voxelsLength).Schedule(JobHandle.CombineDependencies(handles[j++], handles[j++]));
                        if (c % 2 == 1)
                        {
                            Debug.Assert(c - 1 == c_);
                            handles[i++] = handles[c_];
                        }

                        c = i;
                    }

                    JobHandle lastJob;
                    if (count % 2 == 1)
                    {
                        Debug.Assert(count - 1 == count_);
                        (Memory<Vector3> vertices, int[] triangles) = stack_[count_];
                        JobHandle jobHandle = new VoxelizeJob(box, resolution, vertices, triangles, center, size).Schedule();
                        lastJob = new OrJob(box, voxelsLength).Schedule(JobHandle.CombineDependencies(jobHandle, handles[0]));
                    }
                    else
                        lastJob = handles[0];

                    return new EndJob(box, voxels, voxelsLength, stack_, handles).Schedule(lastJob);
                }
                catch
                {
                    ArrayPool<JobHandle>.Shared.Return(handles);
                    while (box.Value.free.TryPop(out bool[] array))
                        ArrayPool<bool>.Shared.Return(array);
                    while (box.Value.toProcess.TryPop(out bool[] array))
                        ArrayPool<bool>.Shared.Return(array);
                    stack_.Dispose();
                    throw;
                }
            }
        }

        private struct EndJob : IManagedJob
        {
            private StrongBox<(RawPooledStack<bool[]> toProcess, RawPooledStack<bool[]> free)> box;
            private bool[] voxels;
            private int voxelsLength;
            private RawPooledList<(Memory<Vector3> vertices, int[] triangles)> stack;
            private JobHandle[] handles;

            public EndJob(StrongBox<(RawPooledStack<bool[]> toProcess, RawPooledStack<bool[]> free)> box, bool[] voxels, int voxelsLength, RawPooledList<(Memory<Vector3> vertices, int[] triangles)> stack, JobHandle[] handles)
            {
                this.box = box;
                this.voxels = voxels;
                this.voxelsLength = voxelsLength;
                this.stack = stack;
                this.handles = handles;
            }

            public void Execute()
            {
                try
                {
                    Debug.Assert(box.Value.toProcess.Count == 1);
                    Or(voxels, box.Value.toProcess.Pop(), voxelsLength);
                }
                finally
                {
                    ArrayPool<JobHandle>.Shared.Return(handles);
                    while (box.Value.free.TryPop(out bool[] array))
                        ArrayPool<bool>.Shared.Return(array);
                    while (box.Value.toProcess.TryPop(out bool[] array))
                        ArrayPool<bool>.Shared.Return(array);
                    stack.Dispose();
                }
            }
        }

        private struct SimpleJob : IManagedJob
        {
            private (int x, int y, int z) resolution;
            private RawPooledList<(Memory<Vector3> vertices, int[] triangles)> stack;
            private int voxelsLength;
            private Vector3 center;
            private Vector3 size;
            private bool[] voxels;

            public SimpleJob((int x, int y, int z) resolution, RawPooledList<(Memory<Vector3> vertices, int[] triangles)> stack, int voxelsLength, Vector3 center, Vector3 size, bool[] voxels)
            {
                this.resolution = resolution;
                this.stack = stack;
                this.voxelsLength = voxelsLength;
                this.center = center;
                this.size = size;
                this.voxels = voxels;
            }

            public void Execute()
            {
                bool[] voxels_ = ArrayPool<bool>.Shared.Rent(voxelsLength);

                int count = stack.Count;
                for (int i = 0; i < count; i++)
                {
                    (Memory<Vector3> vertices, int[] triangles) = stack[i];

                    Span<Vector3> vertices_ = vertices.Span;
                    for (int j = 0; j < vertices.Length; j++)
                        vertices_[j] -= center;

                    Voxelizer.Voxelize(
                        MemoryMarshal.Cast<Vector3, System.Numerics.Vector3>(vertices_),
                        triangles,
                        voxels_,
                        Unsafe.As<Vector3, System.Numerics.Vector3>(ref size),
                        resolution
                    );

                    // TODO: This can be optimized to only copy relevant voxels instead of the whole array.
                    Or(voxels, voxels_, voxelsLength);
                    voxels_.AsSpan(0, voxelsLength).Clear();
                }

                stack.Dispose();
            }
        } 

        private struct OrJob : IManagedJob
        {
            private StrongBox<(RawPooledStack<bool[]> toProcess, RawPooledStack<bool[]> free)> box;
            private int voxelsLength;

            public OrJob(StrongBox<(RawPooledStack<bool[]> toProcess, RawPooledStack<bool[]> free)> box, int voxelsLength)
            {
                this.box = box;
                this.voxelsLength = voxelsLength;
            }

            public void Execute()
            {
                bool[] a;
                bool[] b;

                lock (box)
                {
                    a = box.Value.toProcess.Pop();
                    b = box.Value.toProcess.Pop();
                }

                // TODO: This can be optimized to only copy relevant voxels instead of the whole array.
                Or(a, b, voxelsLength);

                lock (box)
                {
                    box.Value.toProcess.Push(a);
                    box.Value.free.Push(b);
                }
            }
        }

        private struct VoxelizeJob : IManagedJob
        {
            private StrongBox<(RawPooledStack<bool[]> toProcess, RawPooledStack<bool[]> free)> box;
            private (int x, int y, int z) resolution;
            private Memory<Vector3> vertices;
            private int[] triangles;
            private Vector3 center;
            private Vector3 size;

            public VoxelizeJob(StrongBox<(RawPooledStack<bool[]> toProcess, RawPooledStack<bool[]> free)> box, (int x, int y, int z) resolution, Memory<Vector3> vertices, int[] triangles, Vector3 center, Vector3 size)
            {
                this.box = box;
                this.resolution = resolution;
                this.vertices = vertices;
                this.triangles = triangles;
                this.center = center;
                this.size = size;
            }

            public void Execute()
            {
                bool[] voxels;
                lock (box)
                    box.Value.free.TryPop(out voxels);
                if (voxels is null)
                    voxels = ArrayPool<bool>.Shared.Rent(resolution.x * resolution.y * resolution.z);

                Span<Vector3> vertices_ = vertices.Span;
                for (int j = 0; j < vertices.Length; j++)
                    vertices_[j] -= center;

                Voxelizer.Voxelize(
                    MemoryMarshal.Cast<Vector3, System.Numerics.Vector3>(vertices_),
                    triangles,
                    voxels,
                    Unsafe.As<Vector3, System.Numerics.Vector3>(ref size),
                    resolution
                );

                lock (box)
                    box.Value.toProcess.Push(voxels);
            }
        }

        private static void Or(bool[] a, bool[] b, int count)
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

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            stack.Dispose();
            ArrayPool<bool>.Shared.Return(voxels);
        }
    }
}
