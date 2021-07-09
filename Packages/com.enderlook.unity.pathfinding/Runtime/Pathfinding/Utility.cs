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

        private float nextYield = float.PositiveInfinity;
        private RawPooledQueue<Action> continuations = RawPooledQueue<Action>.Create();

        /// <summary>
        /// Whenever it should use multithreading internally or be single threaded.
        /// </summary>
        public bool UseMultithreading;

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
                        nextYield = Time.realtimeSinceStartup  + value;
                    executionTimeSlice = value;
                }

                void Throw() => throw new ArgumentOutOfRangeException(nameof(value), "Can't be negative.");
            }
        }

        private float executionTimeSlice = float.PositiveInfinity;

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
        public int MaxTraversableStep {
            get => maxTraversableStep;
            set {
                if (value < 1)
                    Throw();
                maxTraversableStep = value;

                void Throw() => throw new ArgumentOutOfRangeException(nameof(maxTraversableStep), "Must be positive.");
            }
        }

        private int maxTraversableStep = 1;

        /// <summary>
        /// Minimum height between a floor and a ceil to be considered traversable.
        /// </summary>
        public int MinTraversableHeight {
            get => minTraversableHeight;
            set {
                if (value < 1)
                    Throw();
                minTraversableHeight = value;

                void Throw() => throw new ArgumentOutOfRangeException(nameof(minTraversableHeight), "Must be positive.");
            }
        }
        private int minTraversableHeight = 1;

        /// <summary>
        /// Get the completition percentage of the generation.
        /// </summary>
        /// <returns>Completition percentage.</returns>
        public float GetCompletitionPercentage()
        {
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
                return pecentage + (currentStep / (float)currentSteps * factor);
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
        internal bool StepTaskAndCheckIfMustYield()
        {
            Debug.Assert(tasks.Count > 0);
            Debug.Assert(currentStep < currentSteps);
            Interlocked.Increment(ref currentStep);
            return CheckIfMustYield();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool CheckIfMustYield() => Time.realtimeSinceStartup >= nextYield;

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
    }
}