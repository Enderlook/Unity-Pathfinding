using Enderlook.Mathematics;
using Enderlook.Pools;
using Enderlook.Unity.Pathfinding.Utils;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Debug = UnityEngine.Debug;

namespace Enderlook.Unity.Pathfinding.Generation
{
    internal partial struct Voxelizer
    {
        private struct VoxelizeBoxClosure
        {
            private readonly Vector3 voxelSize;
            private readonly Vector3 minAnchor;
            private readonly int xMinMultiple;
            private readonly int yMinMultiple;
            private readonly int zMinMultiple;
            private readonly int xMaxMultiple;
            private readonly int yMaxMultiple;
            private readonly int zMaxMultiple;
            private readonly Vector3 point1;
            private readonly Vector3 point2;
            private readonly Vector3 point3;
            private readonly Vector3 point4;
            private readonly Vector3 point5;
            private readonly Vector3 point6;
            private readonly Vector3 point7;
            private readonly Vector3 point8;
            private readonly Edge<Vector3> edge1;
            private readonly Edge<Vector3> edge2;
            private readonly Edge<Vector3> edge3;
            private readonly Edge<Vector3> edge4;
            private readonly Edge<Vector3> edge5;
            private readonly Edge<Vector3> edge6;
            private readonly Edge<Vector3> edge7;
            private readonly Edge<Vector3> edge8;
            private readonly Edge<Vector3> edge9;
            private readonly Edge<Vector3> edge10;
            private readonly Edge<Vector3> edge11;
            private readonly Edge<Vector3> edge12;
            private readonly Vector3 i;
            private readonly Vector3 j;
            private readonly Vector3 k;
            private int x;
            private int y;
            private int z;

            public VoxelizeBoxClosure(NavigationGenerationOptions options, UnityEngine.Vector3 min, UnityEngine.Vector3 max)
            {
                VoxelizationParameters parameters = options.VoxelizationParameters;
                voxelSize = Vector3.One * parameters.VoxelSize;
                minAnchor = parameters.Min.ToNumerics();
                point1 = min.ToNumerics();
                point2 = max.ToNumerics();

                CalculateMultiplesVolumeIndexes(min, max, parameters, out int xMinMultiple, out int yMinMultiple, out int zMinMultiple, out int xMaxMultiple, out int yMaxMultiple, out int zMaxMultiple);

                this.xMinMultiple = x = xMinMultiple;
                this.yMinMultiple = y = yMinMultiple;
                this.zMinMultiple = z = zMinMultiple;
                this.xMaxMultiple = xMaxMultiple;
                this.yMaxMultiple = yMaxMultiple;
                this.zMaxMultiple = zMaxMultiple;

                point3 = new Vector3(point1.X, point1.Y, point2.Z);
                point4 = new Vector3(point1.X, point2.Y, point1.Z);
                point5 = new Vector3(point2.X, point1.Y, point1.Z);
                point6 = new Vector3(point1.X, point2.Y, point2.Z);
                point7 = new Vector3(point2.X, point1.Y, point2.Z);
                point8 = new Vector3(point1.X, point2.Y, point2.Z);

                edge1 = new Edge<Vector3>(point6, point2);
                edge2 = new Edge<Vector3>(point2, point8);
                edge3 = new Edge<Vector3>(point8, point4);
                edge4 = new Edge<Vector3>(point4, point6);
                edge5 = new Edge<Vector3>(point3, point7);
                edge6 = new Edge<Vector3>(point7, point5);
                edge7 = new Edge<Vector3>(point5, point1);
                edge8 = new Edge<Vector3>(point1, point3);
                edge9 = new Edge<Vector3>(point6, point3);
                edge10 = new Edge<Vector3>(point2, point7);
                edge11 = new Edge<Vector3>(point8, point5);
                edge12 = new Edge<Vector3>(point4, point1);

                i = point2 - point1;
                j = point4 - point1;
                k = point5 - point1;
            }

            public bool VoxelizeBox<TYield, TInterlock>(NavigationGenerationOptions options, ArraySlice<bool> voxels)
            {
                TimeSlicer timeSlicer = options.TimeSlicer;
                VoxelizationParameters parameters = options.VoxelizationParameters;
                int index = parameters.Depth * (parameters.Height * xMinMultiple);
                for (; x < xMaxMultiple; x++, y = yMinMultiple)
                {
                    index += parameters.Depth * yMinMultiple;
                    for (; y < yMaxMultiple; y++, z = zMinMultiple)
                    {
                        index += zMinMultiple;
                        for (; z < zMaxMultiple; z++, index++)
                        {
                            Debug.Assert(index == parameters.GetIndex(x, y, z));
                            BoundingBox<Vector3> bound = BoundingBox.FromCenter((new Vector3(x, y, z) * voxelSize) + minAnchor, voxelSize);
                            Vector3 bPoint1 = bound.Min;
                            Vector3 bPoint2 = bound.Max;

                            if (
                                // Since colliders are usually larger than a voxel, we check first if the voxel is inside the collider.
                                Contains(bPoint1)
                                || Contains(bPoint2)
                                || Contains(new Vector3(bPoint1.X, bPoint1.Y, bPoint2.Z)) // bPoint3
                                || Contains(new Vector3(bPoint1.X, bPoint2.Y, bPoint1.Z)) // bPoint4
                                || Contains(new Vector3(bPoint2.X, bPoint1.Y, bPoint1.Z)) // bPoint5
                                || Contains(new Vector3(bPoint1.X, bPoint2.Y, bPoint2.Z)) // bPoint6
                                || Contains(new Vector3(bPoint2.X, bPoint1.Y, bPoint2.Z)) // bPoint7
                                || Contains(new Vector3(bPoint1.X, bPoint2.Y, bPoint2.Z)) // bPoint8
                                // Otherwise check if the edges of the collider intersect with the voxel.
                                || bound.Intersects(edge1)
                                || bound.Intersects(edge2)
                                || bound.Intersects(edge3)
                                || bound.Intersects(edge4)
                                || bound.Intersects(edge5)
                                || bound.Intersects(edge6)
                                || bound.Intersects(edge7)
                                || bound.Intersects(edge8)
                                || bound.Intersects(edge9)
                                || bound.Intersects(edge10)
                                || bound.Intersects(edge11)
                                || bound.Intersects(edge12)
                                // Finaly check if the collider is inside the voxel.
                                || bound.Contains(point1)
                                || bound.Contains(point2)
                                || bound.Contains(point3)
                                || bound.Contains(point4)
                                || bound.Contains(point5)
                                || bound.Contains(point6)
                                || bound.Contains(point7)
                                || bound.Contains(point8)
                               )
                            {
                                if (Toggle.IsToggled<TInterlock>())
                                    // HACK: By reinterpreting the bool[] into int[] we can use interlocked operations to set the flags.
                                    // During the construction of this array we already reserved an additional space at the end to prevent
                                    // modifying undefined memory in case of setting the last used element of the voxel.
                                    // 1 is equal to Unsafe.As<bool, int>(ref stackalloc bool[sizeof(int) / sizeof(bool)] { true, false, false, false }[0]);
                                    InterlockedOr(ref Unsafe.As<bool, int>(ref voxels[index]), 1);
                                else
                                    voxels[index] = true;
                            }

                            if (Toggle.IsToggled<TYield>() && timeSlicer.MustYield())
                                return true;
                        }
                        index += parameters.Depth - zMaxMultiple;
                    }
                    index += parameters.Depth * (parameters.Height - yMaxMultiple);
                }
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool Contains(Vector3 point)
            {
                Vector3 v = point - point1;
                float vi = Vector3.Dot(v, i);
                if (0 < vi || vi < Vector3.Dot(i, i))
                    return true;
                float vj = Vector3.Dot(v, j);
                if (0 < vj || vj < Vector3.Dot(j, j))
                    return true;
                float vk = Vector3.Dot(v, k);
                if (0 < vk || vk < Vector3.Dot(k, k))
                    return true;
                return false;
            }

            public static bool ShouldVoxelize(NavigationGenerationOptions options, BoxInformation box, out VoxelizeBoxClosure closure)
            {
                if (!options.VoxelizationParameters.Bounds.Intersects(new UnityEngine.Bounds(box.Center, box.Size)))
                {
                    closure = default;
                    return false;
                }
                                
                closure = new VoxelizeBoxClosure(options, box.Min, box.Max);
                return true;
            }
        }
                
        private sealed class VoxelizeBoxes_MultiThread
        {
            private readonly Action<int> action;
            private ArraySlice<BoxInformation> list;
            private NavigationGenerationOptions options;
            private ArraySlice<bool> voxels;

            public VoxelizeBoxes_MultiThread() => action = Process;

            public static void Calculate(NavigationGenerationOptions options, ArraySlice<bool> voxels, ArraySlice<BoxInformation> list)
            {
                ObjectPool<VoxelizeBoxes_MultiThread> pool = ObjectPool<VoxelizeBoxes_MultiThread>.Shared;
                VoxelizeBoxes_MultiThread instance = pool.Rent();
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
                if (VoxelizeBoxClosure.ShouldVoxelize(options, list[index], out VoxelizeBoxClosure closure))
                {
                    bool value = closure.VoxelizeBox<Toggle.No, Toggle.Yes>(options, voxels);
                    Debug.Assert(!value);
                }
                options.StepTask();
            }
        }
    }
}
