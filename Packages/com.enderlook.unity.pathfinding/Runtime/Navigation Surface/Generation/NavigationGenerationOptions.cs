using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Unity.Pathfinding.Utils;

using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Generation
{
    internal sealed class NavigationGenerationOptions
    {
        private int stepLock;
        private RawPooledList<(int current, int total, string name)> tasks = RawPooledList<(int current, int total, string name)>.Create();
        private int currentStep;
        private int currentSteps;

        /// <summary>
        /// Voxelization parameters information
        /// </summary>
        public VoxelizationParameters VoxelizationParameters { get; private set; }

        /// <summary>
        /// Time slicer used.
        /// </summary>
        public readonly TimeSlicer TimeSlicer = new TimeSlicer();

        /// <inheritdoc cref="TimeSlicer.ExecutionTimeSlice"/>
        public int ExecutionTimeSlice
        {
            get => (int)(TimeSlicer.ExecutionTimeSlice * 1000);
            set => TimeSlicer.ExecutionTimeSlice = value / 1000f;
        }

        /// <inheritdoc cref="TimeSlicer.ShouldUseTimeSlice"/>
        public bool ShouldUseTimeSlice => TimeSlicer.ShouldUseTimeSlice;

        /// <inheritdoc cref="TimeSlicer.UseMultithreading"/>
        public bool UseMultithreading
        {
            get => TimeSlicer.UseMultithreading;
            set => TimeSlicer.UseMultithreading = value;
        }

        /// <inheritdoc cref="TimeSlicer.IsCompleted"/>
        public bool IsCompleted => TimeSlicer.IsCompleted;

        /// <summary>
        /// Maximum amount of cells between two floors to be considered neighbours.
        /// </summary>
        public int MaximumTraversableStep {
            get => maximumTraversableStep;
            set {
                if (value < 1) ThrowHelper.ThrowArgumentOutOfRangeException_ValueMustBeGreaterThanZero();
                maximumTraversableStep = value;
            }
        }

        private int maximumTraversableStep = 1;

        /// <summary>
        /// Minimum height between a floor and a ceil to be considered traversable.
        /// </summary>
        public int MininimumTraversableHeight {
            get => minimumTraversableHeight;
            set {
                if (value < 1) ThrowHelper.ThrowArgumentOutOfRangeException_ValueMustBeGreaterThanZero();
                minimumTraversableHeight = value;
            }
        }
        private int minimumTraversableHeight = 1;

        /// <summary>
        /// Minimum distance for blur.
        /// </summary>
        public int DistanceBlurThreshold {
            get => distanceBlurThreshold;
            set {
                if (value < 1) ThrowHelper.ThrowArgumentOutOfRangeException_ValueMustBeGreaterThanZero();
                distanceBlurThreshold = value;
            }
        }
        private int distanceBlurThreshold = 1;

        /// <summary>
        /// Size of the agent that will traverse this regions.
        /// </summary>
        public int AgentSize {
            get => agentSize;
            set {
                if (value < 1) ThrowHelper.ThrowArgumentOutOfRangeException_ValueMustBeGreaterThanZero();
                agentSize = value;
            }
        }
        private int agentSize;

        /// <summary>
        /// Regions with less that this amount of voxels are nullified.
        /// </summary>
        public int MinimumRegionSurface {
            get => minimumRegionSurface;
            set {
                if (value < 1) ThrowHelper.ThrowArgumentOutOfRangeException_ValueMustBeGreaterThanZero();
                minimumRegionSurface = value;
            }
        }
        private int minimumRegionSurface = 2;

        /// <summary>
        /// The size of the non-navigable border around the heightfield.
        /// </summary>
        public int RegionBorderThickness {
            get => regionBorderThickness;
            set {
                if (value < 1) ThrowHelper.ThrowArgumentOutOfRangeException_ValueMustBeGreaterThanZero();
                regionBorderThickness = value;
            }
        }
        private int regionBorderThickness;

        /// <summary>
        /// Get the completion percentage of the generation.
        /// </summary>
        /// <returns>Completion percentage.</returns>
        public float Progress {
            get {
                Lock(ref stepLock);
                {
                    if (tasks.Count == 0)
                    {
                        Unlock(ref stepLock);
                        return 1;
                    }

                    float pecentage = 0;
                    float factor = 1;
                    for (int i = 0; i < tasks.Count - 1; i++)
                    {
                        (int current, int total, string name) tuple = tasks[i];
                        pecentage += (tuple.current / (float)tuple.total) * factor;
                        factor *= 1f / tuple.total;
                    }

                    int currentStep = this.currentStep;
                    int currentSteps = this.currentSteps;
                    int j = 0;
                    while (currentStep > currentSteps && j++ < 100)
                    {
                        currentStep = this.currentStep;
                        currentSteps = this.currentSteps;
                    }
                    Debug.Assert(currentStep <= currentSteps);

                    Unlock(ref stepLock);

                    if (this.currentSteps == 0)
                        return pecentage;
                    return pecentage + (currentStep / (float)currentSteps * factor);
                }
            }
        }

        /// <inheritdoc cref="TimeSlicer.SetTask(ValueTask)"/>
        public void SetTask(ValueTask task) => TimeSlicer.SetTask(task);

        /// <inheritdoc cref="TimeSlicer.AsTask"/>
        public ValueTask AsTask() => TimeSlicer.AsTask();

        public void SetVoxelizationParameters(float voxelSize, Vector3 min, Vector3 max)
            => VoxelizationParameters = VoxelizationParameters.WithVoxelSize(min, max, voxelSize);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PushTask(int steps, string name)
        {
            Lock(ref stepLock);
            PushTask_(steps, 0, name);
            Unlock(ref stepLock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PushTask(int steps, int step, string name)
        {
            Lock(ref stepLock);
            PushTask_(steps, step, name);
            Unlock(ref stepLock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StepTask() => StepTask_<Toggle.Yes>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StepTask(int count) => StepTask_<Toggle.Yes>(count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PopTask()
        {
            Lock(ref stepLock);
            PopTask_();
            Unlock(ref stepLock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StepPopTask()
        {
            Lock(ref stepLock);
            PopTask_();
            StepTask_<Toggle.No>();
            Unlock(ref stepLock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StepPopPushTask(int steps, string name)
        {
            Lock(ref stepLock);
            PopTask_();
            StepTask_<Toggle.No>();
            PushTask_(steps, 0, name);
            Unlock(ref stepLock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StepPopPushTask(int steps, int step, string name)
        {
            Lock(ref stepLock);
            PopTask_();
            StepTask_<Toggle.No>();
            PushTask_(steps, step, name);
            Unlock(ref stepLock);
        }

        /// <inheritdoc cref="TimeSlicer.Poll"/>
        public void Poll() => TimeSlicer.Poll();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushTask_(int steps, int step, string name)
        {
            if (tasks.Count > 0)
                tasks[tasks.Count - 1].current = currentStep;
            tasks.Add((0, steps, name));
            currentStep = step;
            currentSteps = steps;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StepTask_<TLock>()
        {
            Debug.Assert(tasks.Count > 0);
            Debug.Assert(currentStep < currentSteps);
            int oldStep;
            if (Toggle.IsToggled<TLock>())
                oldStep = Interlocked.Increment(ref currentStep);
            else
            {
                Debug.Assert(stepLock == 1);
                oldStep = currentStep++;
            }
            Debug.Assert(oldStep <= currentSteps);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StepTask_<TLock>(int count)
        {
            Debug.Assert(tasks.Count > 0);
            Debug.Assert(count > 0);
            Debug.Assert(currentStep < currentSteps);
            int oldStep;
            if (Toggle.IsToggled<TLock>())
                oldStep = Interlocked.Add(ref currentStep, count);
            else
            {
                Debug.Assert(stepLock == 1);
                oldStep = currentStep += count;
            }
            Debug.Assert(oldStep < currentSteps);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PopTask_()
        {
            Debug.Assert(tasks.Count > 0);
            Debug.Assert(currentStep == currentSteps);
            int index = tasks.Count - 1;
            tasks.RemoveAt(index);
            if (index > 0)
            {
                (int current, int total, string name) tuple = tasks[tasks.Count - 1];
                currentStep = tuple.current;
                currentSteps = tuple.total;
            }
            else
                currentStep = currentSteps = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Lock(ref int @lock)
        {
            while (Interlocked.Exchange(ref @lock, 1) == 1) ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Unlock(ref int @lock) => @lock = 0;
    }
}