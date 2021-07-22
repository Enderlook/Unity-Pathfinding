using Enderlook.Collections.Pooled.LowLevel;

using System;
using System.Runtime.CompilerServices;
using System.Threading;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    public sealed class MeshGenerationOptions
    {
        private int @lock;
        private RawPooledList<(int current, int total)> tasks = RawPooledList<(int current, int total)>.Create();
        private int currentStep;
        private int currentSteps;

        private float nextYield = float.PositiveInfinity;
        private RawPooledQueue<Action> continuations = RawPooledQueue<Action>.Create();

        /// <summary>
        /// Whenever it should use multithreading internally or be single threaded.
        /// </summary>
        public bool UseMultithreading = Application.platform != RuntimePlatform.WebGLPlayer && SystemInfo.processorCount > 1;

        /// <summary>
        /// If <see cref="UseMultithreading"/> is <see langword="false"/>, the execution is sliced in multiple frames where this value determines the amount of seconds executed on each frame.<br/>
        /// Use 0 to disable this feature.<br/>
        /// For part <see langword="sealed"/> of the execution which must be completed on the main thread, this value is always used regardless of <see cref="UseMultithreading"/> value.
        /// </summary>
        public float ExecutionTimeSlice {
            get => executionTimeSlice == float.PositiveInfinity ? 0 : executionTimeSlice;
            set {
                if (value < 0)
                    Throw();

                if (value == 0)
                {
                    executionTimeSlice = float.PositiveInfinity;
                    nextYield = float.PositiveInfinity;
                }
                else
                {
                    if (executionTimeSlice == float.PositiveInfinity)
                        nextYield = Time.realtimeSinceStartup + value;
                    executionTimeSlice = value;
                }

                void Throw() => throw new ArgumentOutOfRangeException(nameof(value), "Can't be negative.");
            }
        }
        private float executionTimeSlice = float.PositiveInfinity;

        /// <summary>
        /// Whenever it has time slice.
        /// </summary>
        internal bool HasTimeSlice => executionTimeSlice != float.PositiveInfinity;

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
        internal ref readonly Resolution Resolution_ => ref resolution;
        private Resolution resolution;

        /// <summary>
        /// Maximum amount of cells between two floors to be considered neighbours.
        /// </summary>
        public int MaximumTraversableStep {
            get => maximumTraversableStep;
            set {
                if (value < 1)
                    Throw();
                maximumTraversableStep = value;

                void Throw() => throw new ArgumentOutOfRangeException(nameof(maximumTraversableStep), "Must be positive.");
            }
        }

        private int maximumTraversableStep = 1;

        /// <summary>
        /// Minimum height between a floor and a ceil to be considered traversable.
        /// </summary>
        public int MininimumTraversableHeight {
            get => minimumTraversableHeight;
            set {
                if (value < 1)
                    Throw();
                minimumTraversableHeight = value;

                void Throw() => throw new ArgumentOutOfRangeException(nameof(minimumTraversableHeight), "Must be positive.");
            }
        }
        private int minimumTraversableHeight = 1;

        /// <summary>
        /// Minimum distance for blur.
        /// </summary>
        public int DistanceBlurThreshold {
            get => distanceBlurThreshold;
            set {
                if (value < 1)
                    Throw();
                distanceBlurThreshold = value;

                void Throw() => throw new ArgumentOutOfRangeException(nameof(distanceBlurThreshold), "Can't be negative.");
            }
        }
        private int distanceBlurThreshold = 1;

        /// <summary>
        /// Size of the agent that will traverse this regions.
        /// </summary>
        public int AgentSize {
            get => agentSize;
            set {
                if (value < 1)
                    Throw();
                agentSize = value;

                void Throw() => throw new ArgumentOutOfRangeException(nameof(agentSize), "Can't be negative.");
            }
        }
        private int agentSize;

        /// <summary>
        /// Regions with less that this amount of voxels are nullified.
        /// </summary>
        public int MinimumRegionSurface {
            get => minimumRegionSurface;
            set {
                if (value < 0)
                    Throw();
                minimumRegionSurface = value;

                void Throw() => throw new ArgumentOutOfRangeException(nameof(minimumRegionSurface), "Can't be negative.");
            }
        }
        private int minimumRegionSurface = 2;

        /// <summary>
        /// The size of the non-navigable border around the heightfield.
        /// </summary>
        public int RegionBorderThickness {
            get => regionBorderThickness;
            set {
                if (value < 0)
                    Throw();
                regionBorderThickness = value;

                void Throw() => throw new ArgumentOutOfRangeException(nameof(minimumRegionSurface), "Can't be negative.");
            }
        }
        private int regionBorderThickness;

        /// <summary>
        /// Get the completition percentage of the generation.
        /// </summary>
        /// <returns>Completition percentage.</returns>
        public float Progress {
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
                    for (int i = 0; i < tasks.Count - 1; i++)
                    {
                        (int current, int total) tuple = tasks[i];
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

                    Unlock();

                    if (this.currentSteps == 0)
                        return pecentage;
                    return pecentage + (currentStep / (float)currentSteps * factor);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Validate() => resolution.ThrowIfDefault();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PushTask(int steps, string name) => PushTask(steps, 0, name);

        internal void PushTask(int steps, int step, string name)
        {
            Lock();
            {
                if (tasks.Count > 0)
                    tasks[tasks.Count - 1].current = currentStep;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void StepTask(int count)
        {
            Debug.Assert(tasks.Count > 0);
            Debug.Assert(count > 0);
            Debug.Assert(currentStep < currentSteps);
            int value = Interlocked.Add(ref currentStep, count);
            Debug.Assert(value < currentSteps);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool StepTaskAndCheckIfMustYield()
        {
            StepTask();
            return CheckIfMustYield();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool StepTaskAndCheckIfMustYield(int count)
        {
            StepTask(count);
            return CheckIfMustYield();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool StepTaskAndCheckIfMustYield<TYield>()
        {
            StepTask();
            return CheckIfMustYield<TYield>();
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
        internal bool CheckIfMustYield() => Time.realtimeSinceStartup >= nextYield;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool CheckIfMustYield<TYield>()
        {
            if (UseYields<TYield>())
                return CheckIfMustYield();
            return false;
        }

        internal Yielder Yield()
        {
            Debug.Assert(ExecutionTimeSlice > 0);
            return new Yielder(this);
        }

        internal void Poll()
        {
            if (executionTimeSlice == 0)
                return;
            nextYield = Time.realtimeSinceStartup + executionTimeSlice;
            while (Time.realtimeSinceStartup < nextYield && continuations.TryDequeue(out Action action))
                action();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Lock()
        {
            while (Interlocked.Exchange(ref @lock, 1) == 1) ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Unlock() => @lock = 0;

        internal readonly struct Yielder : INotifyCompletion
        {
            private readonly MeshGenerationOptions options;
            private readonly float token;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Yielder(MeshGenerationOptions options)
            {
                this.options = options;
                token = options.nextYield;
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
                options.continuations.Enqueue(continuation);
            }
        }

        internal struct WithYield { }
        internal struct WithoutYield { }

        [System.Diagnostics.Conditional("UNITY_ASSERTIONS")]
        private static void DebugAssertYield<T>() => Debug.Assert(typeof(T) == typeof(WithYield) || typeof(T) == typeof(WithoutYield));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool UseYields<T>()
        {
            DebugAssertYield<T>();
            return typeof(T) == typeof(WithYield);
        }
    }
}