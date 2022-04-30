using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Pools;
using Enderlook.Unity.Threading;

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Utils
{
    /// <summary>
    /// A time slicer system that allow to yield over multiple frames.
    /// </summary>
    public sealed class TimeSlicer : IValueTaskSource, IWatchdog<TimeSlicer.YieldAwait, TimeSlicer.YieldAwait>, IThreadingPreference<TimeSlicer.ToUnityAwait, TimeSlicer.ToUnityAwait>
    {
        private static readonly Action<(TimeSlicer timeSlicer, Action continuation)> runSynchronously = self => self.timeSlicer.RunSynchronouslyBody(self.continuation);

        private static RawPooledList<TimeSlicer> continuationsToRun = RawPooledList<TimeSlicer>.Create();
        private static RawPooledList<TimeSlicer> tasksToCompleteList = Info.SupportMultithreading ? default : RawPooledList<TimeSlicer>.Create();
        private static readonly BlockingCollection<TimeSlicer> tasksToCompleteCollection = Info.SupportMultithreading ? new BlockingCollection<TimeSlicer>() : null;
        private static int staticLock;

        static TimeSlicer()
        {
            UnityThread.OnUpdate += Work;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += () =>
            {
                if (!UnityEditor.EditorApplication.isPlaying)
                    Work();
            };
#endif

            if (Info.SupportMultithreading)
            {
                Task.Factory.StartNew(async () =>
                {
                    while (true)
                        await tasksToCompleteCollection.Take().GetTaskLock();
                }, TaskCreationOptions.LongRunning).Unwrap();
            }

            void Work()
            {
                Debug.Assert(UnityThread.IsMainThread);

                RawPooledList<TimeSlicer> list;
                StaticLock();
                {
                    list = continuationsToRun;
                    continuationsToRun = RawPooledList<TimeSlicer>.Create();
                }
                StaticUnlock();

                for (int i = 0; i < list.Count; i++)
                {
                    TimeSlicer timeSlicer = list[i];
                    timeSlicer.nextYield = Time.realtimeSinceStartup + timeSlicer.executionTimeSlice;
                    // Check if continuation is null in case RunSynchronously was executed before or during the execution of this method.
                    // Since the firt part of RunSynchronously can happen in a background thread, we must do this pop threadsasfe.
                    Action continuation = timeSlicer.PopYieldContinuationOrNullThreadSafe();
                    if (continuation is null)
                        continue;
                    continuation();
                }

                list.Dispose();

                if (!Info.SupportMultithreading)
                {
                    StaticLock();
                    {
                        list = tasksToCompleteList;
                        tasksToCompleteList = RawPooledList<TimeSlicer>.Create();
                    }
                    StaticUnlock();

                    for (int i = 0; i < list.Count; i++)
                        list[i].GetTaskLock().GetAwaiter().GetResult();

                    list.Dispose();
                }
            }
        }

        /// <summary>
        /// Determines at which time the next yield must be done.
        /// </summary>
        private float nextYield = float.PositiveInfinity;

        /// <summary>
        /// Whenever it should use time slice.
        /// </summary>
        public bool ShouldUseTimeSlice => nextYield != float.PositiveInfinity && UnityThread.IsMainThread;

        /// <summary>
        /// Determines the execution time allowed per yield when executing in the Unity thread.
        /// </summary>
        private float executionTimeSlice = float.PositiveInfinity;

        /// <summary>
        /// Stores the original value of <see cref="executionTimeSlice"/> while method <see cref="RunSynchronously"/> is being executed.<br/>
        /// Use <see cref="float.NaN"/> is no value is stored.
        /// </summary>
        private float oldExecutionTimeSlice = float.NaN;

        private bool IsRunningSynchronously => !float.IsNaN(oldExecutionTimeSlice);

        /// <summary>
        /// Whenever it shoud use multithreading when possible.
        /// </summary>
        public bool PreferMultithreading { get; set; } = Info.SupportMultithreading;

        /// <summary>
        /// If <see cref="PreferMultithreading"/> is <see langword="false"/>, the execution is sliced in multiple frames where this value determines the amount of seconds executed on each frame.<br/>
        /// Use 0 to disable this feature.<br/>
        /// For part of the execution which must be completed on the main thread, this value must always be used regardless of <see cref="PreferMultithreading"/> value.<br/>
        /// If <see cref="RunSynchronously"/> is being executed, this value is overwritted as <c>0</c> until the execution of <see cref="MarkAsCompleted"/>.
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
                        nextYield = 0;
                }
            }
        }

        /// <summary>
        /// Stores continuation produced by <see cref="YieldAwait"/> and <see cref="ParentAwait"/>.
        /// </summary>
        /// <remarks>We not use interlocking primitives to read this field because we always do it from the Unity thread.</remarks>
        private Action yieldContinuation;

        private TimeSlicer parent;
        private RawPooledList<TimeSlicer> children = RawPooledList<TimeSlicer>.Create();

        private ValueTask task;

        private ValueTask GetTaskLock()
        {
            ValueTask task;
            Lock();
            {
                task = this.task;
                this.task = default;
            }
            Unlock();
            return task;
        }

        private short version;
        private int @lock;

        private LifeCycle lifeCycle;

        private bool CanContinue => lifeCycle != LifeCycle.CanNotContinue;

        /// <summary>
        /// Whenever cancellation was requested by user.
        /// </summary>
        public bool IsCancellationRequested => !CanContinue;

        /// <summary>
        /// Whenever the underlying task is completed or <see langword="default"/>.
        /// </summary>
        public bool IsCompleted
        {
            get
            {
                bool isCompleted;
                Lock();
                {
                    // If the slicer is empty, the default task would return completed.
                    isCompleted = task.IsCompleted;
                }
                Unlock();
                // Don't assert if markedAsCompleted is true because for newly created instances is false.
                Debug.Assert(!isCompleted || yieldContinuation is null);
                return isCompleted;
            }
        }

        private bool IsCompletedLockless
        {
            get
            {   // If the slicer is empty, the default task would return completed.
                bool isCompleted = task.IsCompleted;
                // Don't assert if markedAsCompleted is true because for newly created instances is false.
                Debug.Assert(!isCompleted || yieldContinuation is null);
                return isCompleted;
            }
        }

        /// <summary>
        /// Set the associated task of this <see cref="TimeSlicer"/>.
        /// </summary>
        /// <param name="task">Task to associate.</param>
        public void SetTask(ValueTask task)
        {
            Lock();
            {
                this.task = task;
                Debug.Assert(lifeCycle == LifeCycle.Empty);
                lifeCycle = LifeCycle.TaskSetted;
            }
            Unlock();
        }

        /// <summary>
        /// Set a parent slicer to this <see cref="TimeSlicer"/>.
        /// </summary>
        /// <param name="parent">Parent <see cref="TimeSlicer"/>.</param>
        public void SetParent(TimeSlicer parent)
        {
            Debug.Assert(lifeCycle == LifeCycle.Empty);
            Debug.Assert(yieldContinuation is null);
            if (parent.IsCompleted)
                return;

            this.parent = parent;
            parent.Lock();
            {
                if (!parent.IsCompletedLockless)
                    parent.children.Add(this);
                else
                {
                    this.parent = default;
                    Debug.Assert(yieldContinuation is null);
                }
            }
            parent.Unlock();
        }

        /// <summary>
        /// Wait until parent allow execution.
        /// </summary>
        /// <returns>Awaiter of parent.</returns>
        public ParentAwait WaitForParentCompletion() => new ParentAwait(this);

        /// <summary>
        /// Reset content of this <see cref="TimeSlicer"/>.
        /// </summary>
        public void Reset()
        {
            version++;
            Lock();
            {
                parent = null;
                children.Clear();
                yieldContinuation = null;
                task = default;
                lifeCycle = LifeCycle.Empty;
            }
            Unlock();
        }

        /// <summary>
        /// Builds a <see cref="ValueTask"/> from this <see cref="TimeSlicer"/>.
        /// </summary>
        /// <returns><see cref="ValueTask"/> from this <see cref="TimeSlicer"/>.</returns>
        public ValueTask AsTask() => new ValueTask(this, version);

        /// <summary>
        /// Request cancellation of this <see cref="TimeSlicer"/>.<br/>
        /// To cancell immediately, follow the call with <see cref="RunSynchronously"/>.
        /// </summary>
        public void RequestCancellation() => lifeCycle = LifeCycle.CanNotContinue;

        /// <summary>
        /// Forces <see cref="TimeSlicer"/> to run synchronously.
        /// </summary>
        public void RunSynchronously()
        {
            parent?.RunSynchronously();

            if (!task.IsCompleted)
            {
                oldExecutionTimeSlice = executionTimeSlice;
                executionTimeSlice = float.PositiveInfinity;
                nextYield = float.PositiveInfinity;

                Action continuation;
                int sleepFor = 0;
                while (true)
                {
                    continuation = PopYieldContinuationOrNullThreadSafe();
                    if (!(continuation is null))
                        break;

                    if (lifeCycle == LifeCycle.MarkedAsCompleted)
                    {
                        Debug.Assert(yieldContinuation is null);
                        // Don't assert if task is completed because it may not, we await that at the end of this method.
                        goto end;
                    }

                    bool break_;
                    Lock();
                    {
                        break_ = task.IsCompleted;
                    }
                    Unlock();
                    if (break_)
                    {
                        Debug.Assert(lifeCycle == LifeCycle.MarkedAsCompleted);
                        Debug.Assert(yieldContinuation is null);
                        goto end;
                    }

                    Thread.Sleep(sleepFor);
                    sleepFor = Math.Min(sleepFor + 10, 100);
                }

                if (UnityThread.IsMainThread)
                    RunSynchronouslyBody(continuation);
                else
                    UnityThread.RunNow(runSynchronously, (this, continuation));

                end:
                // TODO: Research why awaiting here the inner tasks results in a deadlock. We should await here.

                executionTimeSlice = oldExecutionTimeSlice;
                oldExecutionTimeSlice = float.NaN;
                nextYield = 0;
            }
        }

        private void RunSynchronouslyBody(Action continuation)
        {
            Debug.Assert(!(continuation is null));
            Debug.Assert(lifeCycle == LifeCycle.TaskSetted || lifeCycle == LifeCycle.CanNotContinue);
#if DEBUG
            Lock();
            {
                Debug.Assert(!task.IsCompleted);
            }
            Unlock();
#endif
            continuation();
            int sleepFor = 0;
            while (true)
            {
                continuation = PopYieldContinuationOrNullThreadSafe();
                if (continuation is null)
                {
                    if (lifeCycle == LifeCycle.MarkedAsCompleted)
                    {
                        Debug.Assert(yieldContinuation is null);
                        // Don't assert if task is completed because it may not, we await that in the caller method.
                        break;
                    }

                    bool break_;
                    Lock();
                    {
                        break_ = task.IsCompleted;
                    }
                    Unlock();
                    if (break_)
                    {
                        Debug.Assert(yieldContinuation is null);
                        break;
                    }

                    Thread.Sleep(sleepFor);
                    sleepFor = Math.Min(sleepFor + 10, 100);
                }
                else
                {
                    sleepFor = 0;
                    continuation();
                }
            }
        }

        /// <summary>
        /// Marks this <see cref="TimeSlicer"/> as finalized.
        /// </summary>
        public void MarkAsCompleted()
        {
            Debug.Assert(lifeCycle == LifeCycle.TaskSetted || lifeCycle == LifeCycle.CanNotContinue);
            Debug.Assert(yieldContinuation is null);

            RawPooledList<TimeSlicer> children;
            Lock();
            {
                children = this.children;
                this.children = RawPooledList<TimeSlicer>.Create();
            }
            Unlock();

            // If multithreading is supported, we always queue the completion in a background thread.
            if (Info.SupportMultithreading)
                tasksToCompleteCollection.Add(this);
            else
            {
                // If not we must check if we are in RunSynchronously emthod.
                if (float.IsNaN(oldExecutionTimeSlice))
                {
                    // Since we are not inside RunSynchronously, we need to queue the task completion in the main thread.
                    StaticLock();
                    {
                        tasksToCompleteList.Add(this);
                    }
                    StaticUnlock();
                }
                else
                {
                    // Since we are inside RunSynchronously, we don't want to queue but complete the task right now,
                    // however, since this emthod is executed inside the task it will produce a deadlock,
                    // so we complete it in the caller of this method.
                }
            }

            lifeCycle = LifeCycle.MarkedAsCompleted;

            foreach (TimeSlicer child in children)
            {
                Action continuation = child.PopYieldContinuationOrNullThreadSafe();
                child.parent = null;
                if (!(continuation is null))
                {
                    if (child.PreferMultithreading)
                        // Time slicers which support multithreading are run in a background thread.
                        Task.Run(continuation);
                    else
                    {
                        // Time slicers which doesn't support multithreading must be run in the main thread so we let the polling take care of it.
                        Lock();
                        {
                            continuationsToRun.Add(child);
                        }
                        Unlock();
                    }
                }
            }
            children.Dispose();
        }

        /// <summary>
        /// An awaiter that waits until next frame if <see cref="ExecutionTimeSlice"/> has been exceeded.
        /// </summary>
        /// <returns>Awaiter to suspend execution if execution time has been exceeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public YieldAwait Yield()
        {
            Debug.Assert(UnityThread.IsMainThread);
            return new YieldAwait(this, Time.realtimeSinceStartup);
        }

        /// <summary>
        /// Determines if the <see cref="ExecutionTimeSlice"/> has been exceeded.
        /// </summary>
        /// <returns><see langword="true"/> if the <see cref="ExecutionTimeSlice"/> has been exceeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MustYield() => nextYield < Time.realtimeSinceStartup;

        /// <summary>
        /// Continues execution in the Unity thread.
        /// </summary>
        /// <returns>Awaiter to continue execution in the Unity thread.</returns>
        public ToUnityAwait ToUnity() => new ToUnityAwait(this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetYieldContinuation(Action continuation)
        {
#if DEBUG
            Action old = Interlocked.Exchange(ref yieldContinuation, continuation);
            Debug.Assert(old is null);
#else
            yieldContinuation = continuation;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Action PopYieldContinuationOrNullThreadSafe() => Interlocked.Exchange(ref yieldContinuation, null);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Lock()
        {
            while (Interlocked.Exchange(ref @lock, 1) == 1) ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Unlock() => @lock = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StaticLock()
        {
            while (Interlocked.Exchange(ref staticLock, 1) == 1) ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StaticUnlock() => staticLock = 0;

        /// <inheritdoc cref="IValueTaskSource.GetStatus(short)"/>
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

        /// <inheritdoc cref="IValueTaskSource.OnCompleted(Action{object}, object, short, ValueTaskSourceOnCompletedFlags)"/>
        void IValueTaskSource.OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            if (token != version) ThrowArgumentException_InvalidToken();
            if (continuation is null) ThrowArgumentNullException_Continuation();

            // TODO: Is fine to ignore flags parameter?

            Lock();
            {
                task = task.Preserve();
            }
            Unlock();
            task.GetAwaiter().OnCompleted(OnCompletedContinuation.Rent(continuation, state));
        }

        private sealed class OnCompletedContinuation
        {
            private readonly Action action;
            private Action<object> continuation;
            private object state;

            public OnCompletedContinuation() => action = Callback;

            public static Action Rent(Action<object> continuation, object state)
            {
                OnCompletedContinuation instance = ObjectPool<OnCompletedContinuation>.Shared.Rent();
                instance.continuation = continuation;
                instance.state = state;
                return instance.action;
            }

            private void Callback()
            {
                Action<object> continuation = this.continuation;
                object state = this.state;
                this.continuation = default;
                this.state = default;
                ObjectPool<OnCompletedContinuation>.Shared.Return(this);
                continuation(state);
            }
        }

        /// <inheritdoc cref="IValueTaskSource.GetResult(short)"/>
        void IValueTaskSource.GetResult(short token)
        {
            if (token != version) ThrowArgumentException_InvalidToken();
            RunSynchronously();
        }

        /// <inheritdoc cref="IWatchdog{TAwaitable, Awaiter}.CanContinue(out TAwaitable)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IWatchdog<YieldAwait, YieldAwait>.CanContinue(out YieldAwait awaitable)
        {
            awaitable = Yield();
            return CanContinue;
        }

        private static void ThrowArgumentException_InvalidToken()
            => throw new ArgumentException("Invalid token.", "token");

        private static void ThrowArgumentNullException_Continuation()
            => throw new ArgumentNullException("continuation");

        private enum LifeCycle
        {
            Empty,
            TaskSetted,
            MarkedAsCompleted,
            CanNotContinue,
        }

        public readonly struct YieldAwait : IAwaitable<YieldAwait>, IAwaiter, INotifyCompletion
        {
            private readonly TimeSlicer timeSlicer;
            private readonly float token;
#if DEBUG
            private readonly int version;
#endif

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public YieldAwait(TimeSlicer timeSlicer, float token)
            {
                this.timeSlicer = timeSlicer;
                this.token = token;
#if DEBUG
                version = timeSlicer.version;
#endif
            }

            /// <inheritdoc cref="IAwaitable{TAwaiter}.GetAwaiter"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public YieldAwait GetAwaiter()
            {
#if DEBUG
                Debug.Assert(version == timeSlicer.version);
#endif
                return this;
            }

            /// <inheritdoc cref="IAwaitable{TAwaiter}.GetAwaiter"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void GetResult()
            {
#if DEBUG
                Debug.Assert(version == timeSlicer.version);
#endif
            }

            /// <inheritdoc cref="IAwaiter.IsCompleted"/>
            public bool IsCompleted
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
#if DEBUG
                    Debug.Assert(version == timeSlicer.version);
#endif
                    return timeSlicer.nextYield > token;
                }
            }

            /// <inheritdoc cref="INotifyCompletion.OnCompleted(Action)"/>
            public void OnCompleted(Action continuation)
            {
#if DEBUG
                Debug.Assert(version == timeSlicer.version);
#endif
                if (IsCompleted)
                    continuation();
                timeSlicer.SetYieldContinuation(continuation);
                StaticLock();
                {
                    continuationsToRun.Add(timeSlicer);
                }
                StaticUnlock();
            }
        }

        public readonly struct ParentAwait : IAwaitable<ParentAwait>, IAwaiter, INotifyCompletion
        {
            private readonly TimeSlicer timeSlicer;

#if DEBUG
            private readonly int version;
#endif

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ParentAwait(TimeSlicer timeSlicer)
            {
                this.timeSlicer = timeSlicer;
#if DEBUG
                version = timeSlicer.version;
#endif
            }

            /// <inheritdoc cref="IAwaitable{TAwaiter}.GetAwaiter"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ParentAwait GetAwaiter()
            {
#if DEBUG
                Debug.Assert(version == timeSlicer.version);
#endif
                return this;
            }

            /// <inheritdoc cref="IAwaitable{TAwaiter}.GetAwaiter"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void GetResult()
            {
#if DEBUG
                Debug.Assert(version == timeSlicer.version);
#endif
            }

            /// <inheritdoc cref="IAwaiter.IsCompleted"/>
            public bool IsCompleted
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
#if DEBUG
                    Debug.Assert(version == timeSlicer.version);
#endif
                    return timeSlicer.parent is null;
                }
            }

            /// <inheritdoc cref="INotifyCompletion.OnCompleted(Action)"/>
            public void OnCompleted(Action continuation)
            {
#if DEBUG
                Debug.Assert(version == timeSlicer.version);
#endif
                if (IsCompleted)
                    continuation();
                timeSlicer.SetYieldContinuation(continuation);
            }
        }

        public readonly struct ToUnityAwait : IAwaitable<ToUnityAwait>, IAwaiter, INotifyCompletion
        {
            private readonly TimeSlicer timeSlicer;

#if DEBUG
            private readonly int version;
#endif

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ToUnityAwait(TimeSlicer timeSlicer)
            {
                this.timeSlicer = timeSlicer;
#if DEBUG
                version = timeSlicer.version;
#endif
            }

            /// <inheritdoc cref="IAwaitable{TAwaiter}.GetAwaiter"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ToUnityAwait GetAwaiter()
            {
#if DEBUG
                Debug.Assert(version == timeSlicer.version);
#endif
                return this;
            }

            /// <inheritdoc cref="IAwaitable{TAwaiter}.GetAwaiter"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void GetResult()
            {
#if DEBUG
                Debug.Assert(version == timeSlicer.version);
#endif
            }

            /// <inheritdoc cref="IAwaiter.IsCompleted"/>
            public bool IsCompleted
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
#if DEBUG
                    Debug.Assert(version == timeSlicer.version);
#endif
                    return UnityThread.IsMainThread;
                }
            }

            /// <inheritdoc cref="INotifyCompletion.OnCompleted(Action)"/>
            public void OnCompleted(Action continuation)
            {
#if DEBUG
                Debug.Assert(version == timeSlicer.version);
#endif
                if (IsCompleted)
                    continuation();
                timeSlicer.SetYieldContinuation(continuation);
                // Early check to prevent unnecesary storage of timer.
                if (!timeSlicer.IsRunningSynchronously)
                {
                    StaticLock();
                    {
                        continuationsToRun.Add(timeSlicer);
                    }
                    StaticUnlock();
                }
            }
        }
    }
}
