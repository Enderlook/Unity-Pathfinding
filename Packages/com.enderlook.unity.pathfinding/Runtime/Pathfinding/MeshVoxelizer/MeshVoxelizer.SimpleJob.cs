using Enderlook.Collections.Pooled.LowLevel;
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
        }
    }
}
