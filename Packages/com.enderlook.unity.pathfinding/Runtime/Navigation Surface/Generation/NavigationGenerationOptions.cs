using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Mathematics;
using Enderlook.Unity.Pathfinding.Utils;

using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;

#if TRACK_NAVIGATION_GENERATION_LOCATION
using StackTrace = System.Diagnostics.StackTrace;
#endif

namespace Enderlook.Unity.Pathfinding.Generation
{
    internal sealed class NavigationGenerationOptions
    {
        private int stepLock;
        private RawPooledList<(int current, int total, string name)> tasks = RawPooledList<(int current, int total, string name)>.Create();
        private int currentStep;
        private int currentSteps;

#if TRACK_NAVIGATION_GENERATION_LOCATION
        private int lastSourceLineNumber;
        private string lastMemberName;
        private string lastSourceFilePath;
        private StackTrace lastStackTrace;

        public string GetLastLocation() => $"{lastSourceFilePath}:{lastSourceLineNumber} {lastMemberName}\n{lastStackTrace}";
#endif

        /// <summary>
        /// Voxelization parameters information
        /// </summary>
        public VoxelizationParameters VoxelizationParameters { get; private set; }

        private readonly TimeSlicer timeSlicer = new TimeSlicer();

        /// <inheritdoc cref="TimeSlicer.ExecutionTimeSlice"/>
        public int ExecutionTimeSlice
        {
            get => (int)(timeSlicer.ExecutionTimeSlice * 1000);
            set => timeSlicer.ExecutionTimeSlice = value / 1000f;
        }

        /// <inheritdoc cref="TimeSlicer.ShouldUseTimeSlice"/>
        public bool ShouldUseTimeSlice => timeSlicer.ShouldUseTimeSlice;

        /// <inheritdoc cref="TimeSlicer.UseMultithreading"/>
        public bool UseMultithreading
        {
            get => timeSlicer.UseMultithreading;
            set => timeSlicer.UseMultithreading = value;
        }

        /// <inheritdoc cref="TimeSlicer.IsCompleted"/>
        public bool IsCompleted => timeSlicer.IsCompleted;

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
        public void SetTask(ValueTask task) => timeSlicer.SetTask(task);

        /// <inheritdoc cref="TimeSlicer.AsTask"/>
        public ValueTask AsTask() => timeSlicer.AsTask();

        public void SetVoxelizationParameters(float voxelSize, Vector3 min, Vector3 max)
            => VoxelizationParameters = VoxelizationParameters.WithVoxelSize(min, max, voxelSize);

#if TRACK_NAVIGATION_GENERATION_LOCATION
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TrackTrace(string memberName, string sourceFilePath, int sourceLineNumber)
        {
            lastMemberName = memberName;
            lastSourceFilePath = sourceFilePath;
            lastSourceLineNumber = sourceLineNumber;
            lastStackTrace = new StackTrace();
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PushTask(int steps, string name
#if TRACK_NAVIGATION_GENERATION_LOCATION
            , [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0
#endif
            )
        {
            Lock(ref stepLock);
            PushTask_(steps, 0, name);
#if TRACK_NAVIGATION_GENERATION_LOCATION
            TrackTrace(memberName, sourceFilePath, sourceLineNumber);
#endif
            Unlock(ref stepLock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PushTask(int steps, int step, string name
#if TRACK_NAVIGATION_GENERATION_LOCATION
            , [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0
#endif
            )
        {
            Lock(ref stepLock);
            PushTask_(steps, step, name);
#if TRACK_NAVIGATION_GENERATION_LOCATION
            TrackTrace(memberName, sourceFilePath, sourceLineNumber);
#endif
            Unlock(ref stepLock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StepTask(
#if TRACK_NAVIGATION_GENERATION_LOCATION
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0
#endif
            )
        {
#if TRACK_NAVIGATION_GENERATION_LOCATION
            Lock(ref stepLock);
            StepTask_<Toggle.No>();
            TrackTrace(memberName, sourceFilePath, sourceLineNumber);
            Unlock(ref stepLock);
#else
            StepTask_<Toggle.Yes>();
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StepTask(int count
#if TRACK_NAVIGATION_GENERATION_LOCATION
            , [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0
#endif
            )
        {
#if TRACK_NAVIGATION_GENERATION_LOCATION
            Lock(ref stepLock);
            StepTask_<Toggle.No>(count);
            TrackTrace(memberName, sourceFilePath, sourceLineNumber);
            Unlock(ref stepLock);
#else
            StepTask_<Toggle.Yes>(count);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PopTask(
#if TRACK_NAVIGATION_GENERATION_LOCATION
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0
#endif
            )
        {
            Lock(ref stepLock);
            PopTask_();
#if TRACK_NAVIGATION_GENERATION_LOCATION
            TrackTrace(memberName, sourceFilePath, sourceLineNumber);
#endif
            Unlock(ref stepLock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSlicer.Yielder StepTaskAndYield(
#if TRACK_NAVIGATION_GENERATION_LOCATION
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0
#endif
            )
        {
#if TRACK_NAVIGATION_GENERATION_LOCATION
            Lock(ref stepLock);
            StepTask_<Toggle.No>();
            TrackTrace(memberName, sourceFilePath, sourceLineNumber);
            Unlock(ref stepLock);
#else
            StepTask_<Toggle.Yes>();
#endif
            return timeSlicer.Yield();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSlicer.Yielder StepTaskAndYield(int count
#if TRACK_NAVIGATION_GENERATION_LOCATION
            , [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0
#endif
            )
        {
#if TRACK_NAVIGATION_GENERATION_LOCATION
            Lock(ref stepLock);
            StepTask_<Toggle.No>(count);
            TrackTrace(memberName, sourceFilePath, sourceLineNumber);
            Unlock(ref stepLock);
#else
            StepTask_<Toggle.Yes>(count);
#endif
            return timeSlicer.Yield();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSlicer.Yielder StepTaskAndYield<TYield>(
#if TRACK_NAVIGATION_GENERATION_LOCATION
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0
#endif
            )
        {
#if TRACK_NAVIGATION_GENERATION_LOCATION
            Lock(ref stepLock);
            StepTask_<Toggle.No>();
            TrackTrace(memberName, sourceFilePath, sourceLineNumber);
            Unlock(ref stepLock);
#else
            StepTask_<Toggle.Yes>();
#endif
            return timeSlicer.Yield<TYield>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StepPopTask(
#if TRACK_NAVIGATION_GENERATION_LOCATION
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0
#endif
            )
        {
            Lock(ref stepLock);
            PopTask_();
            StepTask_<Toggle.No>();
#if TRACK_NAVIGATION_GENERATION_LOCATION
            TrackTrace(memberName, sourceFilePath, sourceLineNumber);
#endif
            Unlock(ref stepLock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StepPopPushTask(int steps, string name
#if TRACK_NAVIGATION_GENERATION_LOCATION
            , [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0
#endif
            )
        {
            Lock(ref stepLock);
            PopTask_();
            StepTask_<Toggle.No>();
            PushTask_(steps, 0, name);
#if TRACK_NAVIGATION_GENERATION_LOCATION
            TrackTrace(memberName, sourceFilePath, sourceLineNumber);
#endif
            Unlock(ref stepLock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StepPopPushTask(int steps, int step, string name
#if TRACK_NAVIGATION_GENERATION_LOCATION
            , [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0
#endif
            )
        {
            Lock(ref stepLock);
            PopTask_();
            StepTask_<Toggle.No>();
            PushTask_(steps, step, name);
#if TRACK_NAVIGATION_GENERATION_LOCATION
            TrackTrace(memberName, sourceFilePath, sourceLineNumber);
#endif
            Unlock(ref stepLock);
        }

        /// <inheritdoc cref="TimeSlicer.Yield"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSlicer.Yielder Yield(
#if TRACK_NAVIGATION_GENERATION_LOCATION
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0
#endif
            )
        {
#if TRACK_NAVIGATION_GENERATION_LOCATION
            Lock(ref stepLock);
            TrackTrace(memberName, sourceFilePath, sourceLineNumber);
            Unlock(ref stepLock);
#endif
            return timeSlicer.Yield();
        }

        /// <inheritdoc cref="TimeSlicer.Yield{TYield}"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSlicer.Yielder Yield<TYield>(
#if TRACK_NAVIGATION_GENERATION_LOCATION
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0
#endif
            )
        {
#if TRACK_NAVIGATION_GENERATION_LOCATION
            Lock(ref stepLock);
            TrackTrace(memberName, sourceFilePath, sourceLineNumber);
            Unlock(ref stepLock);
#endif
            return timeSlicer.Yield<TYield>();
        }

        public void Poll() => timeSlicer.Poll();

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