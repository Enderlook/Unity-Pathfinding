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
            ref MeshInformation start = ref meshInformations[i];
            ref MeshInformation current = ref start;
            ref MeshInformation end = ref Unsafe.Add(ref current, count - i);
            while (Unsafe.IsAddressLessThan(ref current, ref end))
            {
                if (CalculateMeshBounds<TYield>(options, ref j, ref current))
                {
                    i += MathHelper.IndexesTo(ref start, ref current);
#if DEBUG
                    Debug.Assert(k == i);
#endif
                    return true;
                }

                min = Vector3.Min(min, current.Min);
                max = Vector3.Max(max, current.Max);

#if DEBUG
                Debug.Assert(Unsafe.AreSame(ref current, ref meshInformations[k++]));
#endif
                current = ref Unsafe.Add(ref current, 1);
                options.StepTask();
            }
#if DEBUG
            Debug.Assert(k == count);
#endif
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
            ref Vector3 start = ref pack.Vertices[i];
            ref Vector3 current = ref start;
            ref Vector3 end = ref Unsafe.Add(ref start, pack.Vertices.Length - i);
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
                    if (!Unsafe.IsAddressLessThan(ref current, ref end))
                        goto end;

                    pack.Min = min;
                    pack.Max = max;
                    i += MathHelper.IndexesTo(ref start, ref current);
#if DEBUG
                    Debug.Assert(i == j);
#endif
                    return true;
                }
            }
            end:
            pack.Min = min;
            pack.Max = max;
            i = 0;
            return false;
        }
    }
}