using System;
using System.Runtime.CompilerServices;

using Unity.Jobs;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Represents a handle for operations that can be run with multithreading.<br/>
    /// Despite this object accepting a <see cref="JobHandle"/> it's necessary to manualy mark it as completed.
    /// </summary>
    internal struct ProcessHandle : IProcessHandle, IProcessHandleSourceCompletition
    {
        private const string CAN_NOT_COMPLETE_SINGLE_THREAD = "Can't force completition of a single thread task. This method can only be call if " + nameof(IsComplete) + "is true. Use " + nameof(EndFromSync) + " instead.";
        private const string CAN_NOT_END_IF_IS_EMPTY_COMPLETED_OR_IS_NOT_SINGLE_THREAD = "Can not execute" + nameof(EndFromSync) + " if process is empty, completed, or is not single thread.";
        private const string CAN_NOT_END_IF_IS_EMPTY_COMPLETED_OR_IS_NOT_JOB_HANDLE = "Can not execute" + nameof(EndFromJobHandle) + " if process is empty, completed, or is not a job handle.";
        private const string IS_ALREADY_COMPLETED = "Can't complete an already completed process.";
        private const string IS_BEING_USED = "Process handle is not empty. It has a process in progress.";
        private const string JOB_HANDLE_COMPLETED_BUt_IS_COMPLETE_IS_FALSE = "The JobHandle was completed but the completed flag wasn't true.";

        private Mode mode;
        private JobHandle jobHandle;

        /// <inheritdoc cref="IProcessHandle.IsComplete"/>
        public bool IsComplete {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => !IsPending;
        }

        /// <inheritdoc cref="IProcessHandle.IsPending"/>
        public bool IsPending {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set;
        }

        /// <inheritdoc cref="IProcessHandle.Complete"/>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="mode"/> is <see cref="Mode.SingleThread"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Complete()
        {
            switch (mode)
            {
                case Mode.JobHandle:
                    jobHandle.Complete();
                    if (IsPending)
                        throw new InvalidOperationException(JOB_HANDLE_COMPLETED_BUt_IS_COMPLETE_IS_FALSE);
                    break;
                case Mode.SingleThread:
                    if (IsPending)
                        throw new InvalidOperationException(CAN_NOT_COMPLETE_SINGLE_THREAD);
                    break;
            }
            mode = Mode.Empty;
        }

        /// <inheritdoc cref="IProcessHandleSourceCompletition.EndFromSync"/>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="mode"/> is not <see cref="Mode.SingleThread"/> or <see cref="IsPending"/> is <see langword="false"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndFromSync()
        {
            if (mode != Mode.SingleThread)
                throw new InvalidOperationException(CAN_NOT_END_IF_IS_EMPTY_COMPLETED_OR_IS_NOT_SINGLE_THREAD);
            IsPending = false;
        }

        /// <inheritdoc cref="IProcessHandleSourceCompletition.EndFromJobHandle"/>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="mode"/> is not <see cref="Mode.JobHandle"/> or <see cref="IsPending"/> is <see langword="false"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndFromJobHandle()
        {
            if (mode != Mode.JobHandle)
                throw new InvalidOperationException(CAN_NOT_END_IF_IS_EMPTY_COMPLETED_OR_IS_NOT_JOB_HANDLE);
            IsPending = false;
        }

        /// <inheritdoc cref="IProcessHandleSourceCompletition.StartFromSync"/>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="mode"/> is not <see cref="Mode.Empty"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StartFromSync()
        {
            if (mode != Mode.Empty)
                throw new InvalidOperationException(IS_BEING_USED);
            mode = Mode.SingleThread;
            IsPending = true;
        }

        /// <inheritdoc cref="IProcessHandleSourceCompletition.StartFromJobHandle(JobHandle)"/>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="mode"/> is not <see cref="Mode.Empty"/>.</exception
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StartFromJobHandle(JobHandle jobHandle)
        {
            if (mode != Mode.Empty)
                throw new InvalidOperationException(IS_BEING_USED);
            mode = Mode.JobHandle;
            this.jobHandle = jobHandle;
            IsPending = true;
        }

        private enum Mode
        {
            Empty,
            SingleThread,
            JobHandle,
        }
    }
}