using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Unity.Threading;

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Utils
{
    /// <summary>
    /// A time slicer system that allow to yield over multiple calls of <see cref="Poll"/>.
    /// </summary>
    internal sealed class TimeSlicer
    {
        private static readonly Action<TimeSlicer> executionTimeSliceSet = self
            => self.nextYield = Time.realtimeSinceStartup + self.executionTimeSlice;

        private static readonly Action<TimeSlicer> runSynchronously = self =>
        {
            while (!self.task.IsCompleted)
            {
                self.Lock();
                bool found = self.continuations.TryDequeue(out Action action);
                self.Unlock();
                if (!found)
                    return;
                action();
            }
        };

        private float nextYield = float.PositiveInfinity;
        private RawPooledQueue<Action> continuations = RawPooledQueue<Action>.Create();
        private int @lock;
        private ValueTask task;

        /// <summary>
        /// If <see cref="UseMultithreading"/> is <see langword="false"/>, the execution is sliced in multiple frames where this value determines the amount of seconds executed on each frame.<br/>
        /// Use 0 to disable this feature.<br/>
        /// For part of the execution which must be completed on the main thread, this value is always used regardless of <see cref="UseMultithreading"/> value.
        /// </summary>
        public float ExecutionTimeSlice
        {
            get => executionTimeSlice == float.PositiveInfinity ? 0 : executionTimeSlice;
            set
            {
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
        /// Whenever the underlying task is completed.
        /// </summary>
        public bool IsCompleted => task.IsCompleted;

        /// <summary>
        /// Set the associated task of this slicer.
        /// </summary>
        /// <param name="task">Task to associate.</param>
        public void SetTask(ValueTask task) => this.task = task;

        /// <summary>
        /// Builds a <see cref="NavigationTask"/> from this <see cref="TimeSlicer"/>.
        /// </summary>
        /// <returns><see cref="NavigationTask"/> from this <see cref="TimeSlicer"/>.</returns>
        public NavigationTask AsTask() => new NavigationTask(this);

        /// <inheritdoc cref="ITimeSlicer{TAwaitable, TAwaiter}.Poll"/>
        public void Poll()
        {
            Debug.Assert(UnityThread.IsMainThread);
            if (executionTimeSlice == 0 || continuations.Count == 0)
                return;
            nextYield = Time.realtimeSinceStartup + executionTimeSlice;
            while (Time.realtimeSinceStartup < nextYield)
            {
                Lock();
                bool found = continuations.TryDequeue(out Action action);
                Unlock();
                if (!found)
                    return;
                action();
            }
        }

        /// <summary>
        /// Forces completition of all continuations.
        /// </summary>
        public void CompleteNow()
        {
            if (!task.IsCompleted)
            {
                if (UnityThread.IsMainThread)
                {
                    while (!task.IsCompleted)
                    {
                        Lock();
                        bool found = continuations.TryDequeue(out Action action);
                        Unlock();
                        if (!found)
                            return;
                        action();
                    }
                }
                else
                    UnityThread.RunNow(runSynchronously, this);
            }

            task.GetAwaiter().GetResult();
            task = default;
        }

        /// <summary>
        /// Schedules a continuation action for this task.
        /// </summary>
        /// <param name="continuation">Continuation to schedule.</param>
        public void OnCompleted(Action continuation) => task.GetAwaiter().OnCompleted(continuation);

        /// <inheritdoc cref="ITimeSlicer{TAwaitable, TAwaiter}.Yield"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Yielder Yield()
        {
            Debug.Assert(UnityThread.IsMainThread);
            float nextYield = this.nextYield;
            if (nextYield != float.PositiveInfinity // Prevent an unnecessary call to a Unity API
                && Time.realtimeSinceStartup < nextYield)
                nextYield = default;
            return new Yielder(this, nextYield);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Yielder Yield<TYield>() => Toggle.IsToggled<TYield>() ? Yield() : new Yielder(this, float.NegativeInfinity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Lock()
        {
            while (Interlocked.Exchange(ref @lock, 1) == 1) ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Unlock() => @lock = 0;

        public readonly struct Yielder : IAwaitable<Yielder>, IAwaiter, INotifyCompletion
        {
            private readonly TimeSlicer timeSlicer;
            private readonly float token;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Yielder(TimeSlicer timeSlicer, float token)
            {
                this.timeSlicer = timeSlicer;
                this.token = token;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Yielder GetAwaiter() => this;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void GetResult() { }

            public bool IsCompleted => timeSlicer.nextYield > token;

            public void OnCompleted(Action continuation)
            {
                if (IsCompleted)
                    continuation();
                timeSlicer.Lock();
                timeSlicer.continuations.Enqueue(continuation);
                timeSlicer.Unlock();
            }
        }
    }
}