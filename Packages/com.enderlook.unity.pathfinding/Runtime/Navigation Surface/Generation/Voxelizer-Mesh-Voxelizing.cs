using Enderlook.Mathematics;
using Enderlook.Unity.Pathfinding.Utils;

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Mathf = UnityEngine.Mathf;

namespace Enderlook.Unity.Pathfinding.Generation
{
    internal partial struct Voxelizer
    {
        /// <summary>
        /// Voxelices the content of a mesh.
        /// </summary>
        /// <param name="vertices">Vertice of the mesh.</param>
        /// <param name="triangles">Indexes from <paramref name="vertices"/> that forms the triangles of the mesh.</param>
        /// <param name="voxels">Produced voxels.</param>
        /// <param name="parameters">Parameters of voxelization.</param>
        private static async ValueTask Voxelize<TYield>(
            TimeSlicer timeSlicer,
            ReadOnlyArraySlice<UnityEngine.Vector3> vertices,
            ReadOnlyArraySlice<int> triangles,
            ArraySlice<VoxelInfo> voxels,
            VoxelizationParameters parameters)
        {
            Debug.Assert(voxels.Length >= parameters.VoxelsCount);

            Vector3 voxelSize = Vector3.One * parameters.VoxelSize;
            Vector3 minAnchor = parameters.Min.ToNumerics();

            // Build triangles.
            // For each triangle, perform SAT (Separation axis theorem) intersection check with the AABcs within the triangle AABB.
            for (int i = 0, n = triangles.Length; i < n; i += 3)
            {
                // Reverse order to avoid range check.
                Vector3 v3 = vertices[triangles[i + 2]].ToNumerics();
                Vector3 v2 = vertices[triangles[i + 1]].ToNumerics();
                Vector3 v1 = vertices[triangles[i]].ToNumerics();
                Triangle<Vector3> triangle = new Triangle<Vector3>(v1, v2, v3);

                Vector3 cross = Vector3.Cross(triangle.Second - triangle.First, triangle.Third - triangle.First);
                bool isTriangleFrontFacing = Vector3.Dot(cross, Vector3.UnitZ) <= 0f;
                BoundingBox<Vector3> triangleBounds = triangle.BoundingBox();

                Vector3 min = triangleBounds.Min - minAnchor;
                Vector3 max = triangleBounds.Max - minAnchor;

                // Find triangle AABB, select a sub grid.
                // Note: Maybe Mathf.FloorToInt and Mathf.CeilToInt should be replaced with Mathf.RoundToInt(...).
                int iminX = Mathf.Clamp(Mathf.FloorToInt(min.X / parameters.VoxelSize), 0, parameters.Width - 1);
                int iminY = Mathf.Clamp(Mathf.FloorToInt(min.Y / parameters.VoxelSize), 0, parameters.Height - 1);
                int iminZ = Mathf.Clamp(Mathf.FloorToInt(min.Z / parameters.VoxelSize), 0, parameters.Depth - 1);
                int imaxX = Mathf.Clamp(Mathf.CeilToInt(max.X / parameters.VoxelSize), 0, parameters.Width - 1);
                int imaxY = Mathf.Clamp(Mathf.CeilToInt(max.Y / parameters.VoxelSize), 0, parameters.Height - 1);
                int imaxZ = Mathf.Clamp(Mathf.CeilToInt(max.Z / parameters.VoxelSize), 0, parameters.Depth - 1);

                int index_ = parameters.GetIndex(iminX, iminY, iminZ);
                int yIndexIncrease = parameters.Depth - imaxZ + iminZ - 1;
                int xIndexIncrease = (parameters.Height - imaxY + iminY - 1) * parameters.Depth;
                for (int x = iminX; x <= imaxX; x++)
                {
                    for (int y = iminY; y <= imaxY; y++)
                    {
                        if (Toggle.IsToggled<TYield>())
                        {
                            int z = iminZ;
                            while (FillVoxels(triangle, isTriangleFrontFacing, imaxZ, ref index_, x, y, ref z))
                                await timeSlicer.Yield();
                        }
                        else
                        {
                            for (int z = iminZ; z <= imaxZ; z++, index_++)
                            {
                                BoundingBox<Vector3> box = BoundingBox.FromCenter((new Vector3(x, y, z) * voxelSize) + minAnchor, voxelSize);
                                if (triangle.Intersects(box))
                                    Voxelize_FillVoxel(voxels, parameters, isTriangleFrontFacing, index_, x, y, z);
                            }
                        }
                        index_ += yIndexIncrease;
                    }
                    index_ += xIndexIncrease;
                }
            }

            int baseIndex = 0;
            for (int x = 0; x < parameters.Width; x++)
            {
                for (int y = 0; y < parameters.Height; y++)
                {
                    Debug.Assert(baseIndex == parameters.GetIndex(x, y, 0));
                    for (int z = 0; z < parameters.Depth; z++)
                    {
                        int index = baseIndex + z;
                        Debug.Assert(index == parameters.GetIndex(x, y, z));
                        if (!voxels[index].Fill)
                            continue;

                        int ifront = z;
                        int indexFront = index;
                        if (Toggle.IsToggled<TYield>())
                        {
                            while (CalculateFront(x, y, ref ifront, ref indexFront))
                                await timeSlicer.Yield();
                        }
                        else
                        {
                            for (; ifront < parameters.Depth; ifront++, indexFront++)
                            {
                                Debug.Assert(indexFront == parameters.GetIndex(x, y, ifront));
                                if (!voxels[indexFront].IsFrontFace)
                                    break;
                            }
                        }

                        if (ifront >= parameters.Depth)
                            break;

                        int iback = ifront;
                        int indexBack = indexFront;

                        // Step forward to cavity.
                        if (Toggle.IsToggled<TYield>())
                        {
                            while (StepForwardToCavity(ref indexBack, ref iback))
                                await timeSlicer.Yield();
                        }
                        else
                        {
                            for (; iback < parameters.Depth && !voxels[indexBack].Fill; iback++, indexBack++) { }
                        }

                        if (iback >= parameters.Depth)
                            break;

                        // Check if iback is back voxel.
                        Debug.Assert(indexBack == parameters.GetIndex(x, y, iback));
                        if (voxels[indexBack].IsBackFace)
                        {
                            // Step forward to back face.
                            if (Toggle.IsToggled<TYield>())
                            {
                                if (StepForwardToBackFace(x, y, ref indexBack, ref iback))
                                    await timeSlicer.Yield();
                            }
                            else
                            {
                                for (; iback < parameters.Depth && voxels[indexBack].IsBackFace; iback++, indexBack++)
                                    Debug.Assert(indexBack == parameters.GetIndex(x, y, iback));
                            }
                        }

                        Debug.Assert(indexFront == parameters.GetIndex(x, y, ifront));
                        // Fill from ifront to iback.
                        if (Toggle.IsToggled<TYield>())
                        {
                            int z2 = ifront;
                            while (FillFromIFrontToIBack(x, y, ref indexFront, iback, ref z2))
                                await timeSlicer.Yield();
                        }
                        else
                        {
                            for (int z2 = ifront; z2 < iback; z2++, indexFront++)
                            {
                                Debug.Assert(indexFront == parameters.GetIndex(x, y, z2));
                                voxels[indexFront].Fill = true;
                            }
                        }

                        z = iback;
                    }
                    baseIndex += parameters.Depth;
                }
            }

            bool FillFromIFrontToIBack(int x, int y, ref int index_, int iback, ref int z2)
            {
                while (true)
                {
                    if (z2 >= iback)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, z2));
                    voxels[index_].Fill = true;
                    z2++;
                    index_++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, z2));
                    voxels[index_].Fill = true;
                    z2++;
                    index_++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, z2));
                    voxels[index_].Fill = true;
                    z2++;
                    index_++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, z2));
                    voxels[index_].Fill = true;
                    z2++;
                    index_++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, z2));
                    voxels[index_].Fill = true;
                    z2++;
                    index_++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, z2));
                    voxels[index_].Fill = true;
                    z2++;
                    index_++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, z2));
                    voxels[index_].Fill = true;
                    z2++;
                    index_++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, z2));
                    voxels[index_].Fill = true;
                    z2++;
                    index_++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, z2));
                    voxels[index_].Fill = true;
                    z2++;
                    index_++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, z2));
                    voxels[index_].Fill = true;
                    z2++;
                    index_++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, z2));
                    voxels[index_].Fill = true;
                    z2++;
                    index_++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, z2));
                    voxels[index_].Fill = true;
                    z2++;
                    index_++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, z2));
                    voxels[index_].Fill = true;
                    z2++;
                    index_++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, z2));
                    voxels[index_].Fill = true;
                    z2++;
                    index_++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, z2));
                    voxels[index_].Fill = true;
                    z2++;
                    index_++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, z2));
                    voxels[index_].Fill = true;
                    z2++;
                    index_++;

                    if (timeSlicer.MustYield())
                        return true;
                }
                return false;
            }

            bool StepForwardToBackFace(int x, int y, ref int index_, ref int iback)
            {
                while (true)
                {
                    if (iback >= parameters.Depth || !voxels[index_].IsBackFace)
                        break;
                    iback++;
                    index_++;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index_].IsBackFace)
                        break;
                    iback++;
                    index_++;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index_].IsBackFace)
                        break;
                    iback++;
                    index_++;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index_].IsBackFace)
                        break;
                    iback++;
                    index_++;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index_].IsBackFace)
                        break;
                    iback++;
                    index_++;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index_].IsBackFace)
                        break;
                    iback++;
                    index_++;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index_].IsBackFace)
                        break;
                    iback++;
                    index_++;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index_].IsBackFace)
                        break;
                    iback++;
                    index_++;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index_].IsBackFace)
                        break;
                    iback++;
                    index_++;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index_].IsBackFace)
                        break;
                    iback++;
                    index_++;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index_].IsBackFace)
                        break;
                    iback++;
                    index_++;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index_].IsBackFace)
                        break;
                    iback++;
                    index_++;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index_].IsBackFace)
                        break;
                    iback++;
                    index_++;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index_].IsBackFace)
                        break;
                    iback++;
                    index_++;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index_].IsBackFace)
                        break;
                    iback++;
                    index_++;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index_].IsBackFace)
                        break;
                    iback++;
                    index_++;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, iback));

                    if (timeSlicer.MustYield())
                        return true;
                }
                return false;
            }

            bool FillVoxels(Triangle<Vector3> triangle, bool isTriangleFrontFacing, int imaxZ, ref int index_, int x, int y, ref int z)
            {
                while (true)
                {
                    if (z > imaxZ)
                        break;
                    BoundingBox<Vector3> box = BoundingBox.FromCenter((new Vector3(x, y, z) * voxelSize) + minAnchor, voxelSize);
                    if (triangle.Intersects(box))
                        Voxelize_FillVoxel(voxels, parameters, isTriangleFrontFacing, index_, x, y, z);
                    z++;
                    index_++;

                    if (z > imaxZ)
                        break;
                    box = BoundingBox.FromCenter((new Vector3(x, y, z) * voxelSize) + minAnchor, voxelSize);
                    if (triangle.Intersects(box))
                        Voxelize_FillVoxel(voxels, parameters, isTriangleFrontFacing, index_, x, y, z);
                    z++;
                    index_++;

                    if (z > imaxZ)
                        break;
                    box = BoundingBox.FromCenter((new Vector3(x, y, z) * voxelSize) + minAnchor, voxelSize);
                    if (triangle.Intersects(box))
                        Voxelize_FillVoxel(voxels, parameters, isTriangleFrontFacing, index_, x, y, z);
                    z++;
                    index_++;

                    if (z > imaxZ)
                        break;
                    box = BoundingBox.FromCenter((new Vector3(x, y, z) * voxelSize) + minAnchor, voxelSize);
                    if (triangle.Intersects(box))
                        Voxelize_FillVoxel(voxels, parameters, isTriangleFrontFacing, index_, x, y, z);
                    z++;
                    index_++;

                    if (timeSlicer.MustYield())
                        return true;
                }
                return false;
            }

            bool StepForwardToCavity(ref int index_, ref int iback)
            {
                while (true)
                {
                    if (iback >= parameters.Depth || voxels[index_].Fill)
                        break;
                    iback++;
                    index_++;

                    if (iback >= parameters.Depth || voxels[index_].Fill)
                        break;
                    iback++;
                    index_++;

                    if (iback >= parameters.Depth || voxels[index_].Fill)
                        break;
                    iback++;
                    index_++;

                    if (iback >= parameters.Depth || voxels[index_].Fill)
                        break;
                    iback++;
                    index_++;

                    if (iback >= parameters.Depth || voxels[index_].Fill)
                        break;
                    iback++;
                    index_++;

                    if (iback >= parameters.Depth || voxels[index_].Fill)
                        break;
                    iback++;
                    index_++;

                    if (iback >= parameters.Depth || voxels[index_].Fill)
                        break;
                    iback++;
                    index_++;

                    if (iback >= parameters.Depth || voxels[index_].Fill)
                        break;
                    iback++;
                    index_++;

                    if (iback >= parameters.Depth || voxels[index_].Fill)
                        break;
                    iback++;
                    index_++;

                    if (iback >= parameters.Depth || voxels[index_].Fill)
                        break;
                    iback++;
                    index_++;

                    if (iback >= parameters.Depth || voxels[index_].Fill)
                        break;
                    iback++;
                    index_++;

                    if (iback >= parameters.Depth || voxels[index_].Fill)
                        break;
                    iback++;
                    index_++;

                    if (iback >= parameters.Depth || voxels[index_].Fill)
                        break;
                    iback++;
                    index_++;

                    if (iback >= parameters.Depth || voxels[index_].Fill)
                        break;
                    iback++;
                    index_++;

                    if (iback >= parameters.Depth || voxels[index_].Fill)
                        break;
                    iback++;
                    index_++;

                    if (iback >= parameters.Depth || voxels[index_].Fill)
                        break;
                    iback++;
                    index_++;

                    if (timeSlicer.MustYield())
                        return true;
                }
                return false;
            }

            bool CalculateFront(int x, int y, ref int ifront, ref int index_)
            {
                while (true)
                {
                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index_].IsFrontFace)
                        break;
                    ifront++;
                    index_++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index_].IsFrontFace)
                        break;
                    ifront++;
                    index_++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index_].IsFrontFace)
                        break;
                    ifront++;
                    index_++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index_].IsFrontFace)
                        break;
                    ifront++;
                    index_++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index_].IsFrontFace)
                        break;
                    ifront++;
                    index_++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index_].IsFrontFace)
                        break;
                    ifront++;
                    index_++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index_].IsFrontFace)
                        break;
                    ifront++;
                    index_++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index_].IsFrontFace)
                        break;
                    ifront++;
                    index_++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index_].IsFrontFace)
                        break;
                    ifront++;
                    index_++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index_].IsFrontFace)
                        break;
                    ifront++;
                    index_++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index_].IsFrontFace)
                        break;
                    ifront++;
                    index_++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index_].IsFrontFace)
                        break;
                    ifront++;
                    index_++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index_].IsFrontFace)
                        break;
                    ifront++;
                    index_++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index_].IsFrontFace)
                        break;
                    ifront++;
                    index_++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index_ == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index_].IsFrontFace)
                        break;
                    ifront++;
                    index_++;

                    if (timeSlicer.MustYield())
                        return true;
                }

                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Voxelize_FillVoxel(ArraySlice<VoxelInfo> voxels, VoxelizationParameters parameters, bool isTriangleFrontFacing, int index_, int x, int y, int z)
        {
            Debug.Assert(index_ == parameters.GetIndex(x, y, z));
            ref VoxelInfo voxel = ref voxels[index_];
            if (!voxel.Fill)
                voxel.Front = isTriangleFrontFacing;
            else
                voxel.Front &= isTriangleFrontFacing;
            voxel.Fill = true;
        }

        /// <summary>
        /// Information of a voxel.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct VoxelInfo
        {
            private byte flags;

            /// <summary>
            /// Whenever this voxel has content or is empty.
            /// </summary>
            public bool Fill {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (flags & 1 << 1) > 0;
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set {
                    if (value)
                        flags |= 1 << 1;
                    else
                        flags = (byte)(flags & ~(1 << 1));
                }
            }

            internal bool Front {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (flags & 1 << 2) > 0;
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set {
                    if (value)
                        flags |= 1 << 2;
                    else
                        flags = (byte)(flags & ~(1 << 2));
                }
            }

            /// <summary>
            /// Whenever it's the front face.
            /// </summary>
            public bool IsFrontFace {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Fill && Front;
            }

            /// <summary>
            /// Whenever it's the back face.
            /// </summary>
            public bool IsBackFace {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Fill && !Front;
            }
        }
    }
}
