using Enderlook.Pools;
using Enderlook.Unity.Pathfinding.Utils;

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Generation
{
    internal partial struct Voxelizer
    {
        private bool CalculateMeshesBounds_SingleThread<TYield>(ref int i, ref int j, ref Vector3 min, ref Vector3 max)
        {
            TimeSlicer timeSlicer = options.TimeSlicer;
            int count = meshInformations.Count;
#if DEBUG
            int k = i;
#endif
            ref MeshInformation current = ref meshInformations[i];
            ref MeshInformation end = ref Unsafe.Add(ref meshInformations[count - 1], 1);
            while (Unsafe.IsAddressLessThan(ref current, ref end))
            {
#if DEBUG
                Debug.Assert(Unsafe.AreSame(ref current, ref meshInformations[k++]));
#endif

                if (CalculateMeshBounds<TYield>(options, ref j, ref current))
                {
                    i = MathHelper.GetIndex(meshInformations, ref current);
#if DEBUG
                    Debug.Assert(i == j);
#endif
                    return true;
                }

                min = Vector3.Min(min, current.Min);
                max = Vector3.Max(max, current.Max);

                current = ref Unsafe.Add(ref current, 1);
                options.StepTask();
            }
            return false;
        }

        private sealed class CalculateMeshesBounds_MultiThread : CalculateBounds_MuliThread<MeshInformation>
        {
            private readonly Action<int> action;

            public CalculateMeshesBounds_MultiThread() => action = Process;

            public static void Calculate(NavigationGenerationOptions options, ArraySlice<MeshInformation> list, ref Vector3 min, ref Vector3 max)
            {
                ObjectPool<CalculateMeshesBounds_MultiThread> pool = ObjectPool<CalculateMeshesBounds_MultiThread>.Shared;
                CalculateMeshesBounds_MultiThread instance = pool.Rent();
                {
                    instance.options = options;
                    instance.list = list;
                    options.PushTask(2, "Calculate Bounding Box of Box Colliders");
                    {
                        options.PushTask(list.Length, "Calculate Individual Bounding Boxe");
                        {
                            Parallel.For(0, list.Length, instance.action);
                        }
                        options.StepPopTask();

                        instance.Merge(ref min, ref max);
                    }
                    options.StepPopTask();
                    instance.options = default;
                    instance.list = default;
                }
                pool.Return(instance);
            }

            private void Process(int index)
            {
                int i = 0;
                bool value = CalculateMeshBounds<Toggle.No>(options, ref i, ref list[index]);
                Debug.Assert(!value);
                options.StepTask();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CalculateMeshBounds<TYield>(NavigationGenerationOptions options, ref int i, ref MeshInformation pack)
        {
#if DEBUG
            int j = i;
#endif
            ref Vector3 current = ref pack.Vertices[i];
            ref Vector3 end = ref Unsafe.Add(ref pack.Vertices[pack.Vertices.Length - 1], 1);
            TimeSlicer timeSlicer = options.TimeSlicer;
            Vector3 min = pack.Min;
            Vector3 max = pack.Max;
            while (Unsafe.IsAddressLessThan(ref current, ref end))
            {
#if DEBUG
                Debug.Assert(Unsafe.AreSame(ref current, ref pack.Vertices[j++]));
#endif

                current = TranslatePoint(current, pack.Rotation, pack.LocalScale, pack.WorldPosition);

                Vector3 a = Vector3.Min(min, current);
                Vector3 b = Vector3.Max(max, current);

                // We use Vector3.Min and Vector3.Max methods because Size or LossyScale may have negative values in some of its axis.
                min = Vector3.Min(a, b);
                max = Vector3.Max(a, b);

                current = ref Unsafe.Add(ref current, 1);

                if (Toggle.IsToggled<TYield>() && timeSlicer.MustYield())
                {
                    pack.Min = min;
                    pack.Max = max;
                    unsafe
                    {
                        long currentPointer = ((IntPtr)Unsafe.AsPointer(ref current)).ToInt64();
                        long startPointer = ((IntPtr)Unsafe.AsPointer(ref pack.Vertices[0])).ToInt64();
                        i = (int)((currentPointer - startPointer) / Unsafe.SizeOf<Vector3>());
#if DEBUG
                        Debug.Assert(i == j);
#endif
                    }
                    return true;
                }
            }
            pack.Min = min;
            pack.Max = max;
            return false;
        }
    }
}