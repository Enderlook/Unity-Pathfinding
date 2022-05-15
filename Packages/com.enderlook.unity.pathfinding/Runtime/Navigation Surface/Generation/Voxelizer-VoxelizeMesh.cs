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
                        int z = iminZ;
                        while (VoxelizeMesh_FillVoxels<TYield, TInterlock>(timeSlicer, parameters, voxels, destination, voxelSize, minAnchor, triangle, isTriangleFrontFacing, imaxZ, ref index_, x, y, ref z))
                            await timeSlicer.Yield();
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
                        while (VoxelizeMesh_CalculateFront<TYield>(timeSlicer, parameters, voxels, ref ifront, ref indexFront
#if DEBUG
                            , x, y
#endif
                        ))
                            await timeSlicer.Yield();

                        if (ifront >= parameters.Depth)
                            break;

                        int iback = ifront;
                        int indexBack = indexFront;

                        // Step forward to cavity.
                        while (VoxelizeMesh_StepForwardToCavity<TYield>(timeSlicer, parameters, voxels, ref iback, ref indexBack))
                            await timeSlicer.Yield();

                        if (iback >= parameters.Depth)
                            break;

                        // Check if iback is back voxel.
                        Debug.Assert(indexBack == parameters.GetIndex(x, y, iback));
                        if (voxels[indexBack].IsBackFace)
                        {
                            // Step forward to back face.
                            while (VoxelizeMesh_StepForwardToBackFace<TYield>(timeSlicer, parameters, voxels, ref iback, ref indexBack
#if DEBUG
                                , x, y
#endif
                            ))
                                await timeSlicer.Yield();
                        }

                        Debug.Assert(indexFront == parameters.GetIndex(x, y, ifront));
                        // Fill from ifront to iback.
                        int z2 = ifront;
                        while (VoxelizeMesh_FillFrontFrontToBack<TYield, TInterlock>(timeSlicer, parameters, voxels, destination, ref indexFront, iback, ref z2
#if DEBUG
                            , x, y
#endif
                        ))
                            await timeSlicer.Yield();

                        z = iback;
                    }
                    baseIndex += parameters.Depth;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool VoxelizeMesh_FillFrontFrontToBack<TYield, TInterlock>(TimeSlicer timeSlicer, in VoxelizationParameters parameters, in ArraySlice<VoxelInfo> voxels, in ArraySlice<bool> destinations, ref int index, int iback, ref int z
#if DEBUG
            , int x, int y
#endif
            )
        {
            /* This code is equivalent to:
             *  for (int z = ifront; z < iback; z++, index++)
             *  {
             *      Debug.Assert(index == parameters.GetIndex(x, y, z));
             *      voxels[index].Fill = true;
             *      if (Togle.IsToggled<TInterlock>())
             *          InterlockedOr(ref Unsafe.As<bool, int>(ref destinations[index]), 1);
             *      else
             *          destinations[index] = true;
             *  }
             */

#if DEBUG
            Debug.Assert(voxels.Length > index + iback - z - 1
                && index + iback - z - 1 == parameters.GetIndex(x, y, iback - 1));
#endif
            ref VoxelInfo start = ref voxels[index];
            ref VoxelInfo voxel = ref start;
            ref bool destination = ref destinations[index];
            int unroll = Toggle.IsToggled<TInterlock>() ? 4 : 16;
            ref VoxelInfo end = ref Unsafe.Add(ref start, iback - z - unroll);
#if DEBUG
            int i = index;
#endif

            while (Unsafe.IsAddressLessThan(ref voxel, ref end))
            {
                voxel.Fill = true;
                Unsafe.Add(ref voxel, 1).Fill = true;
                Unsafe.Add(ref voxel, 2).Fill = true;
                Unsafe.Add(ref voxel, 3).Fill = true;
                if (!Toggle.IsToggled<TInterlock>())
                {
                    Unsafe.Add(ref voxel, 4).Fill = true;
                    Unsafe.Add(ref voxel, 5).Fill = true;
                    Unsafe.Add(ref voxel, 6).Fill = true;
                    Unsafe.Add(ref voxel, 7).Fill = true;
                    Unsafe.Add(ref voxel, 8).Fill = true;
                    Unsafe.Add(ref voxel, 9).Fill = true;
                    Unsafe.Add(ref voxel, 10).Fill = true;
                    Unsafe.Add(ref voxel, 11).Fill = true;
                    Unsafe.Add(ref voxel, 12).Fill = true;
                    Unsafe.Add(ref voxel, 13).Fill = true;
                    Unsafe.Add(ref voxel, 14).Fill = true;
                    Unsafe.Add(ref voxel, 15).Fill = true;

                    unsafe {
                        new Span<bool>(Unsafe.AsPointer(ref destination), unroll).Fill(true);
                    }
                }
                else
                    // HACK: By reinterpreting the bool[] into int[] we can use interlocked operations to set the flags.
                    // During the construction of this array we already reserved an additional space at the end to prevent
                    // modifying undefined memory in case of setting the last used element of the voxel.
                    // 0b00000001_00000001_00000001_00000001 is equal to is equal to Unsafe.As<bool, int>(ref stackalloc bool[sizeof(int) / sizeof(bool)] { true, true, true, true }[0]);
                    InterlockedOr(ref Unsafe.As<bool, int>(ref destination), 0b00000001_00000001_00000001_00000001);

                voxel = ref Unsafe.Add(ref voxel, unroll);
                destination = ref Unsafe.Add(ref destination, unroll);
#if DEBUG
                i += unroll;
#endif

                if (Toggle.IsToggled<TYield>() && timeSlicer.MustYield())
                {
                    int offset = MathHelper.IndexesTo(ref start, ref voxel);
                    index += offset;
                    z += offset;
#if DEBUG
                    Debug.Assert(i == index);
#endif
                    return true;
                }
            }

            end = ref Unsafe.Add(ref end, unroll);
            while (Unsafe.IsAddressLessThan(ref voxel, ref end))
            {
                voxel.Fill = true;
                voxel = ref Unsafe.Add(ref voxel, 1);
                if (Toggle.IsToggled<TInterlock>())
                    // HACK: By reinterpreting the bool[] into int[] we can use interlocked operations to set the flags.
                    // During the construction of this array we already reserved an additional space at the end to prevent
                    // modifying undefined memory in case of setting the last used element of the voxel.
                    // 0b00000000_00000000_00000000_00000001 is equal to Unsafe.As<bool, int>(ref stackalloc bool[sizeof(int) / sizeof(bool)] { true, false, false, false }[0]);
                    InterlockedOr(ref Unsafe.As<bool, int>(ref destination), 0b00000000_00000000_00000000_00000001);
                else
                    destination = true;
                destination = ref Unsafe.Add(ref destination, 1);
#if DEBUG
                i++;
#endif
            }

            {
                int offset = MathHelper.IndexesTo(ref start, ref voxel);
                index += offset;
                z += offset;
#if DEBUG
                Debug.Assert(i == index && z == iback);
#endif
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool VoxelizeMesh_StepForwardToBackFace<TYield>(TimeSlicer timeSlicer, in VoxelizationParameters parameters, in ArraySlice<VoxelInfo> voxels, ref int iback, ref int index
#if DEBUG
            , int x, int y
#endif
            )
        {
            /* This code is equivalent to:
             *  for (; iback < parameters.Depth && voxels[index].IsBackFace; iback++, index++)
             *     Debug.Assert(index == parameters.GetIndex(x, y, iback));
             */

            Debug.Assert(voxels.Length > index + parameters.Depth - iback);
            ref VoxelInfo start = ref voxels[index];
            ref VoxelInfo voxel = ref start;
            const int unroll = 16;
            ref VoxelInfo end = ref Unsafe.Add(ref start, parameters.Depth - iback - unroll);
#if DEBUG
            int i = index, b = iback;
#endif
            while (Unsafe.IsAddressLessThan(ref voxel, ref end))
            {
                if (!voxel.IsBackFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(i == parameters.GetIndex(x, y, b) && b < parameters.Depth);
                i++;
                b++;
#endif

                if (!voxel.IsBackFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(i == parameters.GetIndex(x, y, b) && b < parameters.Depth);
                i++;
                b++;
#endif

                if (!voxel.IsBackFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(i == parameters.GetIndex(x, y, b) && b < parameters.Depth);
                i++;
                b++;
#endif

                if (!voxel.IsBackFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(i == parameters.GetIndex(x, y, b) && b < parameters.Depth);
                i++;
                b++;
#endif

                if (!voxel.IsBackFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(i == parameters.GetIndex(x, y, b) && b < parameters.Depth);
                i++;
                b++;
#endif

                if (!voxel.IsBackFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(i == parameters.GetIndex(x, y, b) && b < parameters.Depth);
                i++;
                b++;
#endif

                if (!voxel.IsBackFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(i == parameters.GetIndex(x, y, b) && b < parameters.Depth);
                i++;
                b++;
#endif

                if (!voxel.IsBackFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(i == parameters.GetIndex(x, y, b) && b < parameters.Depth);
                i++;
                b++;
#endif

                if (!voxel.IsBackFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(i == parameters.GetIndex(x, y, b) && b < parameters.Depth);
                i++;
                b++;
#endif

                if (!voxel.IsBackFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(i == parameters.GetIndex(x, y, b) && b < parameters.Depth);
                i++;
                b++;
#endif

                if (!voxel.IsBackFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(i == parameters.GetIndex(x, y, b) && b < parameters.Depth);
                i++;
                b++;
#endif

                if (!voxel.IsBackFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(i == parameters.GetIndex(x, y, b) && b < parameters.Depth);
                i++;
                b++;
#endif

                if (!voxel.IsBackFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(i == parameters.GetIndex(x, y, b) && b < parameters.Depth);
                i++;
                b++;
#endif

                if (!voxel.IsBackFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(i == parameters.GetIndex(x, y, b) && b < parameters.Depth);
                i++;
                b++;
#endif

                if (!voxel.IsBackFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(i == parameters.GetIndex(x, y, b) && b < parameters.Depth);
                i++;
                b++;
#endif

                if (!voxel.IsBackFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(i == parameters.GetIndex(x, y, b) && b < parameters.Depth);
                i++;
                b++;
#endif

                if (Toggle.IsToggled<TYield>() && timeSlicer.MustYield())
                {
                    int offset = MathHelper.IndexesTo(ref start, ref voxel);
                    index += offset;
                    iback += offset;
#if DEBUG
                    Debug.Assert(i == index && b == iback);
#endif
                    return true;
                }
            }

            end = ref Unsafe.Add(ref start, unroll);
            while (Unsafe.IsAddressLessThan(ref voxel, ref end))
            {
                if (!voxel.IsBackFace) break;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(i == parameters.GetIndex(x, y, b) && b < parameters.Depth);
                i++;
                b++;
#endif
            }

        end:
            {
                int offset = MathHelper.IndexesTo(ref start, ref voxel);
                index += offset;
                iback += offset;
#if DEBUG
                Debug.Assert(i == index && b == iback);
#endif
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool VoxelizeMesh_StepForwardToCavity<TYield>(TimeSlicer timeSlicer, in VoxelizationParameters parameters, in ArraySlice<VoxelInfo> voxels, ref int iback, ref int index)
        {
            /* This code is equivalent to:
             *  for (; iback < parameters.Depth && !voxels[index].Fill; iback++, index++) { }
             */

            Debug.Assert(voxels.Length > index + parameters.Depth - iback);
            ref VoxelInfo start = ref voxels[index];
            ref VoxelInfo voxel = ref start;
            const int unroll = 16;
            ref VoxelInfo end = ref Unsafe.Add(ref start, parameters.Depth - iback - unroll);
#if DEBUG
            int i = index, b = iback;
#endif
            while (Unsafe.IsAddressLessThan(ref voxel, ref end))
            {
                if (voxel.Fill) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(b < parameters.Depth);
                i++;
                b++;
#endif

                if (voxel.Fill) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(b < parameters.Depth);
                i++;
                b++;
#endif

                if (voxel.Fill) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(b < parameters.Depth);
                i++;
                b++;
#endif

                if (voxel.Fill) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(b < parameters.Depth);
                i++;
                b++;
#endif

                if (voxel.Fill) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(b < parameters.Depth);
                i++;
                b++;
#endif

                if (voxel.Fill) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(b < parameters.Depth);
                i++;
                b++;
#endif

                if (voxel.Fill) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(b < parameters.Depth);
                i++;
                b++;
#endif

                if (voxel.Fill) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(b < parameters.Depth);
                i++;
                b++;
#endif

                if (voxel.Fill) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(b < parameters.Depth);
                i++;
                b++;
#endif

                if (voxel.Fill) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(b < parameters.Depth);
                i++;
                b++;
#endif

                if (voxel.Fill) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(b < parameters.Depth);
                i++;
                b++;
#endif

                if (voxel.Fill) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(b < parameters.Depth);
                i++;
                b++;
#endif

                if (voxel.Fill) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(b < parameters.Depth);
                i++;
                b++;
#endif

                if (voxel.Fill) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(b < parameters.Depth);
                i++;
                b++;
#endif

                if (voxel.Fill) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(b < parameters.Depth);
                i++;
                b++;
#endif

                if (voxel.Fill) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(b < parameters.Depth);
                i++;
                b++;
#endif

                if (Toggle.IsToggled<TYield>() && timeSlicer.MustYield())
                {
                    int offset = MathHelper.IndexesTo(ref start, ref voxel);
                    index += offset;
                    iback += offset;
#if DEBUG
                    Debug.Assert(i == index && b == iback);
#endif
                    return true;
                }
            }

            end = ref Unsafe.Add(ref end, unroll);
            while (Unsafe.IsAddressLessThan(ref voxel, ref end))
            {
                if (voxel.Fill) break;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(b < parameters.Depth);
                i++;
                b++;
#endif
            }

            end:
            {
                int offset = MathHelper.IndexesTo(ref start, ref voxel);
                index += offset;
                iback += offset;
#if DEBUG
                Debug.Assert(i == index && b == iback);
#endif
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool VoxelizeMesh_CalculateFront<TYield>(TimeSlicer timeslicer, in VoxelizationParameters parameters, in ArraySlice<VoxelInfo> voxels, ref int ifront, ref int index
#if DEBUG
            , int x, int y
#endif
            )
        {
            /* This code is equivalent to:
             *  for (; ifront < parameters.Depth; ifront++, index++)
             *  {
             *      Debug.Assert(index == parameters.GetIndex(x, y, ifront));
             *      if (!voxels[index].IsFrontFace)
             *      break;
             *  }
             */

#if DEBUG
            Debug.Assert(index == parameters.GetIndex(x, y, ifront)
                && parameters.Depth - 1 - ifront + index == parameters.GetIndex(x, y, parameters.Depth - 1)
                && voxels.Length > index + parameters.Depth - ifront);
#endif
            ref VoxelInfo start = ref voxels[index];
            ref VoxelInfo voxel = ref start;
            const int unroll = 16;
#if DEBUG
            int i = index;
            int f = ifront;
#endif
            ref VoxelInfo end = ref Unsafe.Add(ref start, parameters.Depth - ifront - unroll);
            while (Unsafe.IsAddressLessThan(ref voxel, ref end))
            {
                if (!voxel.IsFrontFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(f < parameters.Depth);
                i++;
                f++;
#endif

                if (!voxel.IsFrontFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(f < parameters.Depth);
                i++;
                f++;
#endif

                if (!voxel.IsFrontFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(f < parameters.Depth);
                i++;
                f++;
#endif

                if (!voxel.IsFrontFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(f < parameters.Depth);
                i++;
                f++;
#endif

                if (!voxel.IsFrontFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(f < parameters.Depth);
                i++;
                f++;
#endif

                if (!voxel.IsFrontFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(f < parameters.Depth);
                i++;
                f++;
#endif

                if (!voxel.IsFrontFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(f < parameters.Depth);
                i++;
                f++;
#endif

                if (!voxel.IsFrontFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(f < parameters.Depth);
                i++;
                f++;
#endif

                if (!voxel.IsFrontFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(f < parameters.Depth);
                i++;
                f++;
#endif

                if (!voxel.IsFrontFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(f < parameters.Depth);
                i++;
                f++;
#endif

                if (!voxel.IsFrontFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(f < parameters.Depth);
                i++;
                f++;
#endif

                if (!voxel.IsFrontFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(f < parameters.Depth);
                i++;
                f++;
#endif

                if (!voxel.IsFrontFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(f < parameters.Depth);
                i++;
                
                f++;
#endif

                if (!voxel.IsFrontFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(f < parameters.Depth);
                i++;
                f++;
#endif

                if (!voxel.IsFrontFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(f < parameters.Depth);
                i++;
                f++;
#endif

                if (!voxel.IsFrontFace) goto end;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(f < parameters.Depth);
                i++;
                f++;
#endif

                if (Toggle.IsToggled<TYield>() && timeslicer.MustYield())
                {
                    int offset = MathHelper.IndexesTo(ref start, ref voxel);
                    index += offset;
                    ifront += offset;
#if DEBUG
                    Debug.Assert(i == index && f == ifront);
#endif
                    return true;
                }
            }

            end = ref Unsafe.Add(ref start, unroll);
            while (Unsafe.IsAddressLessThan(ref voxel, ref end))
            {
                if (!voxel.IsFrontFace) break;
                voxel = ref Unsafe.Add(ref voxel, 1);
#if DEBUG
                Debug.Assert(f < parameters.Depth);
                i++;
                f++;
#endif
            }

            end:
            { 
                int offset = MathHelper.IndexesTo(ref start, ref voxel);
                index += offset;
                ifront += offset;
#if DEBUG
                Debug.Assert(i == index && f == ifront);
#endif
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool VoxelizeMesh_FillVoxels<TYield, TInterlock>(TimeSlicer timeSlicer, in VoxelizationParameters parameters, in ArraySlice<VoxelInfo> voxels, in ArraySlice<bool> destinations, Vector3 voxelSize, Vector3 minAnchor, Triangle<Vector3> triangle, bool isTriangleFrontFacing, int imaxZ, ref int index, int x, int y, ref int z)
        {
            /* This code is equivalent to:
             *  for (int z = iminZ; z <= imaxZ; z++, index++)
             *  {
             *      BoundingBox<Vector3> box = BoundingBox.FromCenter((new Vector3(x, y, z) * voxelSize) + minAnchor, voxelSize);
             *      if (triangle.Intersects(box))
             *      {
             *          Debug.Assert(index_ == parameters.GetIndex(x, y, z));
             *          ref VoxelInfo voxel = ref voxels[index];
             *          if (!voxel.Fill)
             *              voxel.Front = isTriangleFrontFacing;
             *          else
             *              voxel.Front &= isTriangleFrontFacing;
             *          voxel.Fill = true;
             *          if (Toggle.IsToggled<TInterlock>())
             *              InterlockedOr(ref Unsafe.As<bool, int>(ref destinations[index]), 1);
             *          else
             *              destinations[index] = true;
             *      }
             *  }
             */

            Debug.Assert(index == parameters.GetIndex(x, y, z)
                && imaxZ - z + index == parameters.GetIndex(x, y, imaxZ)
                && voxels.Length > imaxZ - z + index);
            int oldZ = z;
            ref VoxelInfo start = ref voxels[index];
            ref VoxelInfo voxel = ref start;
            ref bool destination = ref destinations[index];
#if DEBUG
            int i = index;
#endif

            const int unroll = 4;
            int stopAt = imaxZ - unroll;
            while (z < stopAt)
            {
                int flags = 0;

                BoundingBox<Vector3> box = BoundingBox.FromCenter((new Vector3(x, y, z++) * voxelSize) + minAnchor, voxelSize);
                if (triangle.Intersects(box))
                {
                    if (!voxel.Fill)
                        voxel.Front = isTriangleFrontFacing;
                    else
                        voxel.Front &= isTriangleFrontFacing;
                    voxel.Fill = true;
                    if (Toggle.IsToggled<TInterlock>())
                        // Hack: This is equal to bool[] { true, false, false, false }
                        flags = 0b00000000_00000000_00000000_00000001;
                    else
                        destination = true;
                }
#if DEBUG
                i++;
#endif
                voxel = ref Unsafe.Add(ref voxel, 1);
                if (!Toggle.IsToggled<TInterlock>())
                    destination = ref Unsafe.Add(ref destination, 1);

                box = BoundingBox.FromCenter((new Vector3(x, y, z++) * voxelSize) + minAnchor, voxelSize);
                if (triangle.Intersects(box))
                {
                    if (!voxel.Fill)
                        voxel.Front = isTriangleFrontFacing;
                    else
                        voxel.Front &= isTriangleFrontFacing;
                    voxel.Fill = true;
                    if (Toggle.IsToggled<TInterlock>())
                        // Hack: This is equal to bool[] { false, true, false, false }
                        flags |= 0b00000000_00000000_00000001_00000000;
                    else
                        destination = true;
                }
#if DEBUG
                i++;
#endif
                voxel = ref Unsafe.Add(ref voxel, 1);
                if (!Toggle.IsToggled<TInterlock>())
                    destination = ref Unsafe.Add(ref destination, 1);

                box = BoundingBox.FromCenter((new Vector3(x, y, z++) * voxelSize) + minAnchor, voxelSize);
                if (triangle.Intersects(box))
                {
                    if (!voxel.Fill)
                        voxel.Front = isTriangleFrontFacing;
                    else
                        voxel.Front &= isTriangleFrontFacing;
                    voxel.Fill = true;
                    if (Toggle.IsToggled<TInterlock>())
                        // Hack: This is equal to bool[] { false, false, true, false }
                        flags |= 0b00000000_00000001_00000000_00000000;
                    else
                        destination = true;
                }
#if DEBUG
                i++;
#endif
                voxel = ref Unsafe.Add(ref voxel, 1);
                if (!Toggle.IsToggled<TInterlock>())
                    destination = ref Unsafe.Add(ref destination, 1);

                box = BoundingBox.FromCenter((new Vector3(x, y, z++) * voxelSize) + minAnchor, voxelSize);
                if (triangle.Intersects(box))
                {
                    if (!voxel.Fill)
                        voxel.Front = isTriangleFrontFacing;
                    else
                        voxel.Front &= isTriangleFrontFacing;
                    voxel.Fill = true;
                    if (Toggle.IsToggled<TInterlock>())
                        // Hack: This is equal to bool[] { false, false, false, true }
                        flags |= 0b00000001_00000000_00000000_00000000;
                    else
                        destination = true;
                }
#if DEBUG
                i++;
#endif
                voxel = ref Unsafe.Add(ref voxel, 1);
                if (!Toggle.IsToggled<TInterlock>())
                 destination = ref Unsafe.Add(ref destination, 1);
                else
                {
                    // HACK: By reinterpreting the bool[] into int[] we can use interlocked operations to set the flags.
                    // During the construction of this array we already reserved an additional space at the end to prevent
                    // modifying undefined memory in case of setting the last used element of the voxel.
                    // int_ allow to set up to 4 consecutive boolean values.
                    InterlockedOr(ref Unsafe.As<bool, int>(ref destination), flags);
                    destination = ref Unsafe.Add(ref destination, 4);
                }

                if (Toggle.IsToggled<TYield>() && timeSlicer.MustYield())
                {
                    index += MathHelper.IndexesTo(ref start, ref voxel);
#if DEBUG
                    Debug.Assert(index == parameters.GetIndex(x, y, z) && i == index);
#endif
                    return true;
                }
            }

            while (z <= imaxZ)
            {
                BoundingBox<Vector3> box = BoundingBox.FromCenter((new Vector3(x, y, z++) * voxelSize) + minAnchor, voxelSize);
                if (triangle.Intersects(box))
                {
                    if (!voxel.Fill)
                        voxel.Front = isTriangleFrontFacing;
                    else
                        voxel.Front &= isTriangleFrontFacing;
                    voxel.Fill = true;
                    destination = true;
                }
#if DEBUG
                i++;
#endif
                voxel = ref Unsafe.Add(ref voxel, 1);
                destination = ref Unsafe.Add(ref destination, 1);
            }

            index += z - oldZ;
#if DEBUG
            Debug.Assert(index - 1 == parameters.GetIndex(x, y, z - 1) && i == index);
#endif

            return false;
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