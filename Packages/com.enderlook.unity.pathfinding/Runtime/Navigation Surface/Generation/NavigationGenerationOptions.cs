using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Mathematics;
using Enderlook.Unity.Pathfinding.Utils;
using Enderlook.Unity.Threading;

using System;
using System.Runtime.CompilerServices;
using System.Threading;

using UnityEngine;

#if TRACK_NAVIGATION_GENERATION_LOCATION
using StackTrace = System.Diagnostics.StackTrace;
#endif

namespace Enderlook.Unity.Pathfinding.Generation
{
    internal sealed class NavigationGenerationOptions
    {
        private static readonly SendOrPostCallback executionTimeSliceSet = e =>
        {
            NavigationGenerationOptions self = (NavigationGenerationOptions)e;
            self.nextYield = Time.realtimeSinceStartup + self.executionTimeSlice;
        };

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

        private float nextYield = float.PositiveInfinity;
        internal RawPooledQueue<Action> continuations = RawPooledQueue<Action>.Create();
        private int continuationsLock;

        /// <summary>
        /// Whenever it should use multithreading internally or be single threaded.
        /// </summary>
        public bool UseMultithreading = Info.SupportMultithreading;

        /// <summary>
        /// If <see cref="UseMultithreading"/> is <see langword="false"/>, the execution is sliced in multiple frames where this value determines the amount of seconds executed on each frame.<br/>
        /// Use 0 to disable this feature.<br/>
        /// For part of the execution which must be completed on the main thread, this value is always used regardless of <see cref="UseMultithreading"/> value.
        /// </summary>
        public float ExecutionTimeSlice {
            get => executionTimeSlice == float.PositiveInfinity ? 0 : executionTimeSlice;
            set {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();

                if (value == 0)
                {
                    executionTimeSlice = float.PositiveInfinity;
                    nextYield = float.PositiveInfinity;
                }
                else
                {
                    float oldExecutionTimeSlice = executionTimeSlice;
                    executionTimeSlice = value;
                    if (oldExecutionTimeSlice == float.PositiveInfinity)
                    {
                        if (UnityThread.IsMainThread)
                            nextYield = Time.realtimeSinceStartup + value;
                        else
                            UnityThread.RunNow(executionTimeSliceSet, this);
                    }
                }
            }
        }
        private float executionTimeSlice = float.PositiveInfinity;

        /// <summary>
        /// Whenever it should to use time slice.
        /// </summary>
        public bool ShouldUseTimeSlice => executionTimeSlice != float.PositiveInfinity && UnityThread.IsMainThread;

        /// <summary>
        /// Voxelization parameters information
        /// </summary>
        public VoxelizationParameters VoxelizationParameters { get; private set; }

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

        public void SetVoxelizationParameters(float voxelSize, Vector3 min, Vector3 max)
        {
            //VoxelizationParameters = VoxelizationParameters.WithVoxelSize(min, max, voxelSize);
            float min_ = Mathematic.Min(min.x, min.y, min.z);
            float max_ = Mathematic.Max(max.x, max.y, max.z);
            VoxelizationParameters = VoxelizationParameters.WithVoxelSize(Vector3.one * min_, Vector3.one * max_, voxelSize);
        }

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
        public Yielder StepTaskAndYield(
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
            return Yield_();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Yielder StepTaskAndYield(int count
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
            return Yield_();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Yielder StepTaskAndYield<TYield>(
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
            return Yield_<TYield>();
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Yielder Yield(
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
            return Yield_();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Yielder Yield<TYield>(
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
            return Yield_<TYield>();
        }

        public void Poll()
        {
            if (executionTimeSlice == 0)
                return;
            nextYield = Time.realtimeSinceStartup + executionTimeSlice;
            while (Time.realtimeSinceStartup < nextYield)
            {
                Lock(ref continuationsLock);
                bool found = continuations.TryDequeue(out Action action);
                Unlock(ref continuationsLock);
                if (!found)
                    return;
                action();
            }
        }

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
        private Yielder Yield_()
        {
            Debug.Assert(UnityThread.IsMainThread);
            float nextYield = this.nextYield;
            if (nextYield != float.PositiveInfinity // Prevent an unnecessary call to a Unity API
                && Time.realtimeSinceStartup < nextYield)
                nextYield = default;
            return new Yielder(this, nextYield);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Yielder Yield_<TYield>() => UseYields<TYield>() ? Yield_() : new Yielder(this, float.NegativeInfinity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnqueueContinuation(Action continuation)
        {
            Lock(ref continuationsLock);
            continuations.Enqueue(continuation);
            Unlock(ref continuationsLock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Lock(ref int @lock)
        {
            while (Interlocked.Exchange(ref @lock, 1) == 1) ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Unlock(ref int @lock) => @lock = 0;

        public readonly struct Yielder : INotifyCompletion
        {
            private readonly NavigationGenerationOptions options;
            private readonly float token;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Yielder(NavigationGenerationOptions options, float token)
            {
                this.options = options;
                this.token = token;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Yielder GetAwaiter() => this;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void GetResult() { }

            public bool IsCompleted => options.nextYield > token;

            public void OnCompleted(Action continuation)
            {
                if (IsCompleted)
                    continuation();
                options.EnqueueContinuation(continuation);
            }
        }

        public struct WithYield { }

        public struct WithoutYield { }

        [System.Diagnostics.Conditional("UNITY_ASSERTIONS")]
        private static void DebugAssertYield<T>() => Debug.Assert(typeof(T) == typeof(WithYield) || typeof(T) == typeof(WithoutYield));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool UseYields<T>()
        {
            DebugAssertYield<T>();
            return typeof(T) == typeof(WithYield);
        }
    }
}