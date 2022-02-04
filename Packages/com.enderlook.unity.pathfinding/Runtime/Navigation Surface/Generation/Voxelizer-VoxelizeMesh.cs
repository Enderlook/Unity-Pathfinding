using Enderlook.Mathematics;
using Enderlook.Pools;
using Enderlook.Unity.Pathfinding.Utils;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Debug = UnityEngine.Debug;
using Mathf = UnityEngine.Mathf;

namespace Enderlook.Unity.Pathfinding.Generation
{
    internal partial struct Voxelizer
    {
        private static async ValueTask VoxelizeMesh<TYield, TInterlock>(
            TimeSlicer timeSlicer,
            VoxelizationParameters parameters,
            ArraySlice<bool> destination,
            MeshInformation content,
            ArraySlice<VoxelInfo> voxels)

        {
            Debug.Assert(voxels.Length >= parameters.VoxelsCount);

            Vector3 voxelSize = Vector3.One * parameters.VoxelSize;
            Vector3 minAnchor = parameters.Min.ToNumerics();

            // Build triangles.
            // For each triangle, perform SAT (Separation axis theorem) intersection check with the AABcs within the triangle AABB.
            for (int i = 0, n = content.Triangles.Length; i < n; i += 3)
            {
                // Reverse order to avoid range check.
                Vector3 v3 = content.Vertices[content.Triangles[i + 2]].ToNumerics();
                Vector3 v2 = content.Vertices[content.Triangles[i + 1]].ToNumerics();
                Vector3 v1 = content.Vertices[content.Triangles[i]].ToNumerics();
                Triangle<Vector3> triangle = new Triangle<Vector3>(v1, v2, v3);

                Vector3 cross = Vector3.Cross(triangle.Second - triangle.First, triangle.Third - triangle.First);
                bool isTriangleFrontFacing = Vector3.Dot(cross, Vector3.UnitZ) <= 0f;
                BoundingBox<Vector3> triangleBounds = triangle.BoundingBox();

                Vector3 triangleMin = triangleBounds.Min - minAnchor;
                Vector3 triangleMax = triangleBounds.Max - minAnchor;

                // Find triangle AABB, select a sub grid.
                // Note: Maybe Mathf.FloorToInt and Mathf.CeilToInt should be replaced with Mathf.RoundToInt(...).
                int iminX = Mathf.Clamp(Mathf.FloorToInt(triangleMin.X / parameters.VoxelSize), 0, parameters.Width - 1);
                int iminY = Mathf.Clamp(Mathf.FloorToInt(triangleMin.Y / parameters.VoxelSize), 0, parameters.Height - 1);
                int iminZ = Mathf.Clamp(Mathf.FloorToInt(triangleMin.Z / parameters.VoxelSize), 0, parameters.Depth - 1);
                int imaxX = Mathf.Clamp(Mathf.CeilToInt(triangleMax.X / parameters.VoxelSize), 0, parameters.Width - 1);
                int imaxY = Mathf.Clamp(Mathf.CeilToInt(triangleMax.Y / parameters.VoxelSize), 0, parameters.Height - 1);
                int imaxZ = Mathf.Clamp(Mathf.CeilToInt(triangleMax.Z / parameters.VoxelSize), 0, parameters.Depth - 1);

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
                                    Voxelize_FillVoxel(parameters, voxels, isTriangleFrontFacing, index_, x, y, z);
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
                                while (StepForwardToBackFace(x, y, ref indexBack, ref iback))
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

            {
                CalculateMultiplesVolumeIndexes(content.Min, content.Max, parameters, out int xMinMultiple, out int yMinMultiple, out int zMinMultiple, out int xMaxMultiple, out int yMaxMultiple, out int zMaxMultiple);

                int index = parameters.Depth * (parameters.Height * xMinMultiple);
                for (int x = xMinMultiple; x < xMaxMultiple; x++)
                {
                    index += parameters.Depth * yMinMultiple;
                    for (int y = yMinMultiple; y < yMaxMultiple; y++)
                    {
                        index += zMinMultiple;
                        for (int z = zMinMultiple; z < zMaxMultiple; z++, index++)
                        {
                            Debug.Assert(index == parameters.GetIndex(x, y, z));
                            if (voxels[index].Fill)
                            {
                                if (Toggle.IsToggled<TInterlock>())
                                    // HACK: By reinterpreting the bool[] into int[] we can use interlocked operations to set the flags.
                                    // During the construction of this array we already reserved an additional space at the end to prevent
                                    // modifying undefined memory in case of setting the last used element of the voxel.
                                    // 1 is equal to Unsafe.As<bool, int>(ref stackalloc bool[sizeof(int) / sizeof(bool)] { true, false, false, false }[0]);
                                    InterlockedOr(ref Unsafe.As<bool, int>(ref destination[index]), 1);
                                else
                                    destination[index] = true;
                            }
                            if (Toggle.IsToggled<TYield>())
                                await timeSlicer.Yield();
                        }
                        index += parameters.Depth - zMaxMultiple;
                    }
                    index += parameters.Depth * (parameters.Height - yMaxMultiple);
                }
            }

            bool FillFromIFrontToIBack(int x, int y, ref int index, int iback, ref int z2)
            {
                while (true)
                {
                    if (z2 >= iback)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, z2));
                    voxels[index].Fill = true;
                    z2++;
                    index++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, z2));
                    voxels[index].Fill = true;
                    z2++;
                    index++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, z2));
                    voxels[index].Fill = true;
                    z2++;
                    index++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, z2));
                    voxels[index].Fill = true;
                    z2++;
                    index++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, z2));
                    voxels[index].Fill = true;
                    z2++;
                    index++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, z2));
                    voxels[index].Fill = true;
                    z2++;
                    index++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, z2));
                    voxels[index].Fill = true;
                    z2++;
                    index++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, z2));
                    voxels[index].Fill = true;
                    z2++;
                    index++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, z2));
                    voxels[index].Fill = true;
                    z2++;
                    index++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, z2));
                    voxels[index].Fill = true;
                    z2++;
                    index++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, z2));
                    voxels[index].Fill = true;
                    z2++;
                    index++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, z2));
                    voxels[index].Fill = true;
                    z2++;
                    index++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, z2));
                    voxels[index].Fill = true;
                    z2++;
                    index++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, z2));
                    voxels[index].Fill = true;
                    z2++;
                    index++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, z2));
                    voxels[index].Fill = true;
                    z2++;
                    index++;

                    if (z2 >= iback)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, z2));
                    voxels[index].Fill = true;
                    z2++;
                    index++;

                    if (timeSlicer.MustYield())
                        return true;
                }
                return false;
            }

            bool StepForwardToBackFace(int x, int y, ref int index, ref int iback)
            {
                while (true)
                {
                    if (iback >= parameters.Depth || !voxels[index].IsBackFace)
                        break;
                    iback++;
                    index++;
                    Debug.Assert(index == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index].IsBackFace)
                        break;
                    iback++;
                    index++;
                    Debug.Assert(index == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index].IsBackFace)
                        break;
                    iback++;
                    index++;
                    Debug.Assert(index == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index].IsBackFace)
                        break;
                    iback++;
                    index++;
                    Debug.Assert(index == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index].IsBackFace)
                        break;
                    iback++;
                    index++;
                    Debug.Assert(index == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index].IsBackFace)
                        break;
                    iback++;
                    index++;
                    Debug.Assert(index == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index].IsBackFace)
                        break;
                    iback++;
                    index++;
                    Debug.Assert(index == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index].IsBackFace)
                        break;
                    iback++;
                    index++;
                    Debug.Assert(index == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index].IsBackFace)
                        break;
                    iback++;
                    index++;
                    Debug.Assert(index == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index].IsBackFace)
                        break;
                    iback++;
                    index++;
                    Debug.Assert(index == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index].IsBackFace)
                        break;
                    iback++;
                    index++;
                    Debug.Assert(index == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index].IsBackFace)
                        break;
                    iback++;
                    index++;
                    Debug.Assert(index == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index].IsBackFace)
                        break;
                    iback++;
                    index++;
                    Debug.Assert(index == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index].IsBackFace)
                        break;
                    iback++;
                    index++;
                    Debug.Assert(index == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index].IsBackFace)
                        break;
                    iback++;
                    index++;
                    Debug.Assert(index == parameters.GetIndex(x, y, iback));

                    if (iback >= parameters.Depth || !voxels[index].IsBackFace)
                        break;
                    iback++;
                    index++;
                    Debug.Assert(index == parameters.GetIndex(x, y, iback));

                    if (timeSlicer.MustYield())
                        return true;
                }
                return false;
            }

            bool FillVoxels(Triangle<Vector3> triangle, bool isTriangleFrontFacing, int imaxZ, ref int index, int x, int y, ref int z)
            {
                while (true)
                {
                    if (z > imaxZ)
                        break;
                    BoundingBox<Vector3> box = BoundingBox.FromCenter((new Vector3(x, y, z) * voxelSize) + minAnchor, voxelSize);
                    if (triangle.Intersects(box))
                        Voxelize_FillVoxel(parameters, voxels, isTriangleFrontFacing, index, x, y, z);
                    z++;
                    index++;

                    if (z > imaxZ)
                        break;
                    box = BoundingBox.FromCenter((new Vector3(x, y, z) * voxelSize) + minAnchor, voxelSize);
                    if (triangle.Intersects(box))
                        Voxelize_FillVoxel(parameters, voxels, isTriangleFrontFacing, index, x, y, z);
                    z++;
                    index++;

                    if (z > imaxZ)
                        break;
                    box = BoundingBox.FromCenter((new Vector3(x, y, z) * voxelSize) + minAnchor, voxelSize);
                    if (triangle.Intersects(box))
                        Voxelize_FillVoxel(parameters, voxels, isTriangleFrontFacing, index, x, y, z);
                    z++;
                    index++;

                    if (z > imaxZ)
                        break;
                    box = BoundingBox.FromCenter((new Vector3(x, y, z) * voxelSize) + minAnchor, voxelSize);
                    if (triangle.Intersects(box))
                        Voxelize_FillVoxel(parameters, voxels, isTriangleFrontFacing, index, x, y, z);
                    z++;
                    index++;

                    if (timeSlicer.MustYield())
                        return true;
                }
                return false;
            }

            bool StepForwardToCavity(ref int index, ref int iback)
            {
                while (true)
                {
                    if (iback >= parameters.Depth || voxels[index].Fill)
                        break;
                    iback++;
                    index++;

                    if (iback >= parameters.Depth || voxels[index].Fill)
                        break;
                    iback++;
                    index++;

                    if (iback >= parameters.Depth || voxels[index].Fill)
                        break;
                    iback++;
                    index++;

                    if (iback >= parameters.Depth || voxels[index].Fill)
                        break;
                    iback++;
                    index++;

                    if (iback >= parameters.Depth || voxels[index].Fill)
                        break;
                    iback++;
                    index++;

                    if (iback >= parameters.Depth || voxels[index].Fill)
                        break;
                    iback++;
                    index++;

                    if (iback >= parameters.Depth || voxels[index].Fill)
                        break;
                    iback++;
                    index++;

                    if (iback >= parameters.Depth || voxels[index].Fill)
                        break;
                    iback++;
                    index++;

                    if (iback >= parameters.Depth || voxels[index].Fill)
                        break;
                    iback++;
                    index++;

                    if (iback >= parameters.Depth || voxels[index].Fill)
                        break;
                    iback++;
                    index++;

                    if (iback >= parameters.Depth || voxels[index].Fill)
                        break;
                    iback++;
                    index++;

                    if (iback >= parameters.Depth || voxels[index].Fill)
                        break;
                    iback++;
                    index++;

                    if (iback >= parameters.Depth || voxels[index].Fill)
                        break;
                    iback++;
                    index++;

                    if (iback >= parameters.Depth || voxels[index].Fill)
                        break;
                    iback++;
                    index++;

                    if (iback >= parameters.Depth || voxels[index].Fill)
                        break;
                    iback++;
                    index++;

                    if (iback >= parameters.Depth || voxels[index].Fill)
                        break;
                    iback++;
                    index++;

                    if (timeSlicer.MustYield())
                        return true;
                }
                return false;
            }

            bool CalculateFront(int x, int y, ref int ifront, ref int index)
            {
                while (true)
                {
                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index].IsFrontFace)
                        break;
                    ifront++;
                    index++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index].IsFrontFace)
                        break;
                    ifront++;
                    index++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index].IsFrontFace)
                        break;
                    ifront++;
                    index++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index].IsFrontFace)
                        break;
                    ifront++;
                    index++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index].IsFrontFace)
                        break;
                    ifront++;
                    index++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index].IsFrontFace)
                        break;
                    ifront++;
                    index++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index].IsFrontFace)
                        break;
                    ifront++;
                    index++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index].IsFrontFace)
                        break;
                    ifront++;
                    index++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index].IsFrontFace)
                        break;
                    ifront++;
                    index++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index].IsFrontFace)
                        break;
                    ifront++;
                    index++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index].IsFrontFace)
                        break;
                    ifront++;
                    index++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index].IsFrontFace)
                        break;
                    ifront++;
                    index++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index].IsFrontFace)
                        break;
                    ifront++;
                    index++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index].IsFrontFace)
                        break;
                    ifront++;
                    index++;

                    if (ifront >= parameters.Depth)
                        break;
                    Debug.Assert(index == parameters.GetIndex(x, y, ifront));
                    if (!voxels[index].IsFrontFace)
                        break;
                    ifront++;
                    index++;

                    if (timeSlicer.MustYield())
                        return true;
                }

                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Voxelize_FillVoxel(VoxelizationParameters parameters, ArraySlice<VoxelInfo> voxels, bool isTriangleFrontFacing, int index, int x, int y, int z)
        {
            Debug.Assert(index == parameters.GetIndex(x, y, z));
            ref VoxelInfo voxel = ref voxels[index];
            if (!voxel.Fill)
                voxel.Front = isTriangleFrontFacing;
            else
                voxel.Front &= isTriangleFrontFacing;
            voxel.Fill = true;
        }

        private sealed class VoxelizeMeshes_MultiThread
        {
            private readonly Action<int> action;
            private NavigationGenerationOptions options;
            private ArraySlice<MeshInformation> list;
            private ArraySlice<bool> voxels;

            public VoxelizeMeshes_MultiThread() => action = Process;

            public static void Calculate(NavigationGenerationOptions options, ArraySlice<MeshInformation> list, ArraySlice<bool> voxels)
            {
                ObjectPool<VoxelizeMeshes_MultiThread> pool = ObjectPool<VoxelizeMeshes_MultiThread>.Shared;
                VoxelizeMeshes_MultiThread instance = pool.Rent();
                {
                    instance.options = options;
                    instance.voxels = voxels;
                    instance.list = list;

                    Parallel.For(0, list.Length, instance.action);

                    instance.options = default;
                    instance.voxels = default;
                    instance.list = default;
                }
                pool.Return(instance);
            }

            private void Process(int index)
            {
                VoxelizationParameters parameters = options.VoxelizationParameters;
                ArraySlice<VoxelInfo> voxelsInfo = new ArraySlice<VoxelInfo>(parameters.VoxelsCount + (sizeof(int) / sizeof(bool)), true);

                MeshInformation content = list[index];
                ValueTask task = VoxelizeMesh<Toggle.No, Toggle.Yes>(
                    options.TimeSlicer,
                    parameters,
                    voxels,
                    content,
                    voxelsInfo);
                Debug.Assert(task.IsCompleted);
                content.Dispose();

                voxelsInfo.Dispose();
                options.StepTask();
            }
        }

        /// <summary>
        /// Information of a voxel.
        /// </summary>
        private struct VoxelInfo
        {
            /// <summary>
            /// Whenever this voxel has content or is empty.
            /// </summary>
            public bool Fill;
            public bool Front;

            /// <summary>
            /// Whenever it's the front face.
            /// </summary>
            public bool IsFrontFace
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Fill && Front;
            }

            /// <summary>
            /// Whenever it's the back face.
            /// </summary>
            public bool IsBackFace
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Fill && !Front;
            }
        }
    }
}