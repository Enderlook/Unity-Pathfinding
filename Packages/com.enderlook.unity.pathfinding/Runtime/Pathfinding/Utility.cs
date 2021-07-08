using Enderlook.Collections.Pooled.LowLevel;

using System.Runtime.CompilerServices;

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

        public bool UseMultithreading;
        private bool AddAdditionalYields;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool MustYield() => AddAdditionalYields;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PushTask(int steps)
        {
            tasks.Add((currentStep, steps));
            currentStep = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void StepTask()
        {
            Debug.Assert(tasks.Count > 0);
            Debug.Assert(currentStep < tasks[tasks.Count - 1].total);
            currentStep++;
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