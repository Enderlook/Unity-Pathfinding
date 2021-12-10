﻿using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Unity.Threading;

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Utils
{
    /// <summary>
    /// A time slicer system that allow to yield over multiple calls of <see cref="Poll"/>.
    /// </summary>
    internal sealed class TimeSlicer : IValueTaskSource
    {
        private static readonly Action<TimeSlicer> executionTimeSliceSet = self
            => self.nextYield = Time.realtimeSinceStartup + self.executionTimeSlice;

        private static readonly Action<TimeSlicer> runSynchronously = self =>
        {
            while (!self.task.IsCompleted)
            {
                Lock(ref self.continuationsLock);
                bool found = self.continuations.TryDequeue(out Action action);
                Unlock(ref self.continuationsLock);
                if (!found)
                    return;
                action();
            }
        };

        private float nextYield = float.PositiveInfinity;

        private RawPooledQueue<Action> continuations = RawPooledQueue<Action>.Create();
        private int continuationsLock;

        private ValueTask task;
        private int taskLock;

        private short version;

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
        public bool IsCompleted
        {
            get {
                Lock(ref taskLock);
                bool isCompleted = task.IsCompleted;
                Unlock(ref taskLock);
                return isCompleted;
            }
        }

        /// <summary>
        /// Set the associated task of this slicer.
        /// </summary>
        /// <param name="task">Task to associate.</param>
        public void SetTask(ValueTask task) => this.task = task;

        /// <summary>
        /// Builds a <see cref="ValueTask"/> from this <see cref="TimeSlicer"/>.
        /// </summary>
        /// <returns><see cref="ValueTask"/> from this <see cref="TimeSlicer"/>.</returns>
        public ValueTask AsTask() => new ValueTask(this, version);

        /// <inheritdoc cref="ITimeSlicer{TAwaitable, TAwaiter}.Poll"/>
        public void Poll()
        {
            Debug.Assert(UnityThread.IsMainThread);
            if (executionTimeSlice == 0 || continuations.Count == 0)
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

        /// <summary>
        /// Forces completition of all continuations.
        /// </summary>
        public void RunSynchronously()
        {
            if (!task.IsCompleted)
            {
                if (UnityThread.IsMainThread)
                {
                    while (!task.IsCompleted)
                    {
                        Lock(ref continuationsLock);
                        bool found = continuations.TryDequeue(out Action action);
                        Unlock(ref continuationsLock);
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
        private static void Lock(ref int @lock)
        {
            while (Interlocked.Exchange(ref @lock, 1) == 1) ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Unlock(ref int @lock) => @lock = 0;

        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
        {
            if (token != version) ThrowArgumentException_InvalidToken();

            if (task.IsCompletedSuccessfully)
                return ValueTaskSourceStatus.Succeeded;
            if (task.IsFaulted)
                return ValueTaskSourceStatus.Faulted;
            if (task.IsCanceled)
                return ValueTaskSourceStatus.Canceled;
            return ValueTaskSourceStatus.Pending;
        }

        void IValueTaskSource.OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            if (token != version) ThrowArgumentException_InvalidToken();
            if (continuation is null) ThrowArgumentNullException_Continuation();

            // TODO: Is fine to ignore flags parameter?

            Lock(ref taskLock);
            task = task.Preserve();
            Unlock(ref taskLock);
            task.GetAwaiter().OnCompleted(() => continuation(state));
        }

        void IValueTaskSource.GetResult(short token)
        {
            if (token != version) ThrowArgumentException_InvalidToken();
            RunSynchronously();
        }

        private static void ThrowArgumentException_InvalidToken()
            => throw new ArgumentException("Invalid token.", "token");

        private static void ThrowArgumentNullException_Continuation()
            => throw new ArgumentNullException("continuation");

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
                Lock(ref timeSlicer.continuationsLock);
                timeSlicer.continuations.Enqueue(continuation);
                Unlock(ref timeSlicer.continuationsLock);
            }
        }
    }
}