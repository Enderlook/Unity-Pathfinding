using Enderlook.Collections.Pooled.LowLevel;

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
        private RawPooledList<(int current, int total)> tasks = RawPooledList<(int current, int total)>.Create();
        private int currentStep;

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
        internal void PushTask(int steps, string name)
        {
            tasks.Add((currentStep, steps));
            currentStep = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PushTask(int steps, int currentStep, string name)
        {
            tasks.Add((currentStep, steps));
            this.currentStep = currentStep;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void StepTask()
        {
            Debug.Assert(tasks.Count > 0);
            Debug.Assert(currentStep < tasks[tasks.Count - 1].total);
            Interlocked.Increment(ref currentStep);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PopTask()
        {
            Debug.Assert(tasks.Count > 0);
            Debug.Assert(currentStep == tasks[tasks.Count - 1].total);
            tasks.RemoveAt(tasks.Count - 1);
            currentStep = tasks[tasks.Count - 1].current;
        }
    }
}