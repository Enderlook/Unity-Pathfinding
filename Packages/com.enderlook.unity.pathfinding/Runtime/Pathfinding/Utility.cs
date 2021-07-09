using Enderlook.Collections.Pooled.LowLevel;

using System;
using System.Runtime.CompilerServices;
using System.Threading;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    internal static class Utility
    {
        public /*readonly*/ static bool UseMultithreading = Application.platform == RuntimePlatform.WebGLPlayer || SystemInfo.processorCount == 1;
    }

    public sealed class MeshGenerationOptions
    {
        private int @lock;
        private RawPooledList<(int current, int total)> tasks = RawPooledList<(int current, int total)>.Create();
        private int currentStep;
        private int currentSteps;

        public float Percentage {
            get {
                Lock();
                {
                    if (tasks.Count == 0)
                    {
                        Unlock();
                        return 1;
                    }

                    float pecentage = 0;
                    float factor = 1;
                    for (int i = 0; i < tasks.Count; i++)
                    {
                        (int current, int total) tuple = tasks[i];
                        pecentage += (tuple.current / tuple.total) * factor;
                    }

                    int currentStep = this.currentStep;
                    int currentSteps = this.currentSteps;
                    while (currentStep > currentSteps)
                    {
                        currentStep = this.currentStep;
                        currentSteps = this.currentSteps;
                    }

                    Unlock();

                    if (this.currentSteps == 0)
                        return pecentage;
                    return pecentage + currentStep / currentSteps * factor;
                }
            }
        }

        /// <summary>
        /// Whenever it should use multithreading internally or be single threaded.
        /// </summary>
        public bool UseMultithreading;

        /// <summary>
        /// Whenever it should add additional yields on tasks.
        /// </summary>
        private bool AddAdditionalYields;

        /// <summary>
        /// Resolution of the voxelization.
        /// </summary>
        public Resolution Resolution {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => resolution;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                value.DebugAssert(nameof(value));
                resolution = value;
            }
        }
        private Resolution resolution;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Validate() => resolution.ThrowIfDefault();

        /// <summary>
        /// Maximum amount of cells between two floors to be considered neighbours.
        /// </summary>
        public int MaxTraversableStep;

        /// <summary>
        /// Minimum height between a floor and a ceil to be considered traversable.
        /// </summary>
        public int MinTraversableHeight;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool MustYield() => AddAdditionalYields;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PushTask(int steps, string name) => PushTask(steps, 0, name);

        internal void PushTask(int steps, int step, string name)
        {
            Lock();
            {
                if (tasks.Count > 0)
                    tasks[tasks.Count - 1].current = this.currentStep;
                tasks.Add((0, steps));
                currentStep = step;
                currentSteps = steps;
            }
            Unlock();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void StepTask()
        {
            Debug.Assert(tasks.Count > 0);
            Debug.Assert(currentStep < currentSteps);
            Interlocked.Increment(ref currentStep);
        }

        internal void PopTask()
        {
            Lock();
            {
                Debug.Assert(tasks.Count > 0);
                Debug.Assert(currentStep == currentSteps);
                tasks.RemoveAt(tasks.Count - 1);
                (int current, int total) tuple = tasks[tasks.Count - 1];
                currentStep = tuple.current;
                currentSteps = tuple.total;
            }
            Unlock();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Lock()
        {
            while (Interlocked.Exchange(ref @lock, 1) == 1) ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Unlock() => @lock = 0;
    }
}