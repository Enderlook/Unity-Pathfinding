using System;
using System.Runtime.CompilerServices;

using Unity.Jobs;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Represents a handle for operations that can be run with multithreading techniques.
    /// </summary>
    internal struct ProcessHandle : IProcessHandle, IProcessHandleSourceCompletition
    {
        private Mode mode;
        private JobHandle? jobHandle;

        /// <inheritdoc cref="IProcessHandle.IsComplete"/>
        public bool IsComplete {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                bool _ = jobHandle is JobHandle handle && handle.IsCompleted; // If we don't add this, it seems that Unity doesn't start the job
                return mode == Mode.ManualComplete || mode == Mode.Default || mode == Mode.TrulyComplete;
            }
        }

        public bool IsCompleteThreadSafe {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => mode == Mode.ManualComplete || mode == Mode.Default || mode == Mode.TrulyComplete;
        }

        /// <inheritdoc cref="IProcessHandle.IsPending"/>
        public bool IsPending {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => !IsComplete;
        }

        /// <inheritdoc cref="IProcessHandle.Complete"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Complete()
        {
            if (IsPending)
                throw new InvalidOperationException("Can't complete if is pending.");

            if (jobHandle is JobHandle handle)
            {
                handle.Complete();
                jobHandle = null;
            }

            mode = Mode.TrulyComplete;
        }

        /// <inheritdoc cref="IProcessHandleSourceCompletition.Start"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Start()
        {
            if (mode != Mode.Default && mode != Mode.TrulyComplete)
                throw new InvalidOperationException("Can't start process handle if is not default nor is truly complete.");
            mode = Mode.Working;
        }

        /// <inheritdoc cref="IProcessHandleSourceCompletition.SetJobHandle(JobHandle)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetJobHandle(JobHandle jobHandle)
        {
            if (this.jobHandle.HasValue)
                throw new InvalidOperationException("Can't set job handle if already has one.");

            this.jobHandle = jobHandle;
        }

        /// <inheritdoc cref="IProcessHandleSourceCompletition.CompleteJobHandle"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CompleteJobHandle()
        {
            if (mode != Mode.Working)
                throw new InvalidOperationException("Can't complete job handle if is not working.");

            if (jobHandle is JobHandle handle)
            {
                handle.Complete();
                jobHandle = null;
            }
            else
                throw new InvalidOperationException("Can't complete job handle if doesn't have one.");
        }

        /// <inheritdoc cref="IProcessHandleSourceCompletition.End"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void End()
        {
            if (mode != Mode.Working)
                throw new InvalidOperationException("Can't complete process handle if is not working.");

            mode = Mode.ManualComplete;
        }

        private enum Mode
        {
            Default = 0,
            Working,
            ManualComplete,
            TrulyComplete,
        }
    }
}