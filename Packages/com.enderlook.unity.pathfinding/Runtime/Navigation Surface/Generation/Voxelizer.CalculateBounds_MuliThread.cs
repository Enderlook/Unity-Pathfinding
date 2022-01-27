using Enderlook.Unity.Pathfinding.Utils;

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Generation
{
    internal partial struct Voxelizer
    {
        private class CalculateBounds_MuliThread<T> where T : IMinMax
        {
            private readonly Action<int> action;
            protected NavigationGenerationOptions options;
            protected ArraySlice<T> list;
            private IndexPartitioner partitioner;
            private Vector3 min;
            private Vector3 max;

            public CalculateBounds_MuliThread() => action = Process;

            protected void Merge(ref Vector3 min, ref Vector3 max)
            {
                min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                options.PushTask(list.Length, "Merging Individual Bounding Boxes");
                {
                    // The overhead of multithreading here may not be worthly, so we check the amount of work before using it.
                    if (list.Length > 5000) // TODO: Research a better threshold.
                    {
                        partitioner = new IndexPartitioner(0, list.Length);
                        Parallel.For(0, partitioner.PartsCount, action);
                        min = this.min;
                        max = this.max;
                    }
                    else
                    {
#if UNITY_ASSERTIONS
                        int j = 0;
#endif
                        ref T current = ref list[0];
                        ref T end = ref Unsafe.Add(ref list[list.Length - 1], 1);
                        for (int i = 0; i < list.Length; i++)
                        {
#if UNITY_ASSERTIONS
                            Debug.Assert(Unsafe.AreSame(ref current, ref list[j++]));
#endif
                            min = Vector3.Min(min, current.Min);
                            max = Vector3.Max(max, current.Max);
                            current = ref Unsafe.Add(ref current, 1);
                            options.StepTask();
                        }
                    }
                }
                options.StepPopTask();
            }

            private void Process(int index)
            {
                (int fromInclusive, int toExclusive) tuple = partitioner[index];
                Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                for (int j = tuple.fromInclusive; j < tuple.toExclusive; j++)
                {
                    T pack = list[j];
                    min = Vector3.Min(min, pack.Min);
                    max = Vector3.Max(max, pack.Max);
                    options.StepTask();
                }

                lock (this)
                {
                    this.min = Vector3.Max(this.min, min);
                    this.max = Vector3.Max(this.max, max);
                }
            }
        }
    }
}
