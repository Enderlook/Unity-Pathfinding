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
        private bool CalculateBoxesBounds_SingleThread<TYield>(ref int i, ref Vector3 min, ref Vector3 max)
        {
            TimeSlicer timeSlicer = options.TimeSlicer;
            int count = boxInformations.Count;
#if DEBUG
            int j = i;
#endif
            ref BoxInformation current = ref boxInformations[i];
            ref BoxInformation end = ref Unsafe.Add(ref boxInformations[count - 1], 1);
            while (Unsafe.IsAddressLessThan(ref current, ref end))
            {
#if DEBUG
                Debug.Assert(Unsafe.AreSame(ref current, ref boxInformations[j++]));
#endif

                Vector3 extent = current.Size / 2;
                Vector3 a = TranslatePoint(current.Center, -extent, current.Rotation, current.LossyScale, current.WorldPosition);
                Vector3 b = TranslatePoint(current.Center, extent, current.Rotation, current.LossyScale, current.WorldPosition);

                // We use Vector3.Min and Vector3.Max methods because Size or LossyScale may have negative values in some of its axis.
                Vector3 min_ = Vector3.Min(a, b);
                Vector3 max_ = Vector3.Max(a, b);

                current.Min = min_;
                current.Max = max_;

                min = Vector3.Min(min, min_);
                max = Vector3.Max(max, max_);

                current = ref Unsafe.Add(ref current, 1);
                options.StepTask();

                if (Toggle.IsToggled<TYield>() && timeSlicer.MustYield())
                {
                    i = MathHelper.GetIndex(boxInformations, ref current);
#if DEBUG
                    Debug.Assert(i == j);
#endif
                    return true;
                }
            }
            return false;
        }

        private sealed class CalculateBoxesBounds_MultiThread : CalculateBounds_MuliThread<BoxInformation>
        {
            private readonly Action<int> action;

            public CalculateBoxesBounds_MultiThread() => action = Process;

            public static void Calculate(NavigationGenerationOptions options, ArraySlice<BoxInformation> list, ref Vector3 min, ref Vector3 max)
            {
                ObjectPool<CalculateBoxesBounds_MultiThread> pool = ObjectPool<CalculateBoxesBounds_MultiThread>.Shared;
                CalculateBoxesBounds_MultiThread instance = pool.Rent();
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
                ref BoxInformation current = ref list[index];
                Vector3 extent = current.Size / 2;
                Vector3 a = TranslatePoint(current.Center, -extent, current.Rotation, current.LossyScale, current.WorldPosition);
                Vector3 b = TranslatePoint(current.Center, extent, current.Rotation, current.LossyScale, current.WorldPosition);
                // We use Vector3.Min and Vector3.Max methods because Size or LossyScale may have negative values in some of its axis.
                current.Min = Vector3.Min(a, b);
                current.Max = Vector3.Max(a, b);
                options.StepTask();
            }
        }
    }
}
