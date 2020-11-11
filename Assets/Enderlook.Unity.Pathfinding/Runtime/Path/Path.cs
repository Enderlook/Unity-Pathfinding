using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Unity.Jobs;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Represents a path.
    /// </summary>
    /// <typeparam name="TInfo">Node or coordinate type.</typeparam>
    public sealed class Path<TInfo> : IPathFeedable<TInfo>, IProcessHandle, IProcessHandleSourceCompletition, IEnumerable<TInfo>
    {
        private const string CAN_NOT_FEED_IF_IS_NOT_PENDING = "Can't feed path if it's not pending.";
        private const string CAN_NOT_GET_DESTINATION_IF_PATH_WAS_NOT_FOUND = "Can't get destination if path is empty or not found.";
        private const string CAN_NOT_GET_DESTINATION_IF_PATH_IS_PENDING = "Can't get destination if path is pending.";
        private const string PATH_WAS_MODIFIED_OUTDATED_ENUMERATOR = "Path was modified; enumeration operation may not execute.";
        private const string CAN_NOT_EXECUTE_IF_IS_PENDING = "Can't execute if it's pending.";

        private DynamicArray<TInfo> list = DynamicArray<TInfo>.Create();
        private int version;
        private Status status;
        private ProcessHandle processHandle;

        /// <summary>
        /// Extract the underlying span of this path.
        /// </summary>
        internal Span<TInfo> AsSpan {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => list.AsSpan;
        }

        /// <summary>
        /// Determines if this path is being calculated.
        /// </summary>
        public bool IsPending {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => processHandle.IsPending;
        }

        /// <inheritdoc cref="IProcessHandle.IsComplete"/>
        public bool IsComplete {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => processHandle.IsComplete;
        }

        /// <inheritdoc cref="IProcessHandle.Complete"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Complete() => processHandle.Complete();

        /// <summary>
        /// Determines if this path contains an actual path.<br/>
        /// This will return <see langword="false"/> if it's empty, hasn't found a path, or <see cref="IsPending"/> is <see langword="true"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="IsPending"/> is <see langword="true"/>.</exception>
        public bool HasPath {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                if (IsPending)
                    throw new InvalidOperationException(CAN_NOT_EXECUTE_IF_IS_PENDING);
                return (status & Status.Found) != 0;
            }
        }

        /// <summary>
        /// Determines the amount of points this path has.<br/>
        /// This returns <c>0</c> if <see cref="HasPath"/> and <see cref="IsPending"/> are both <see langword="false"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="IsPending"/> is <see langword="true"/>.</exception>
        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                if (IsPending)
                    throw new InvalidOperationException(CAN_NOT_EXECUTE_IF_IS_PENDING);
                return list.Count;
            }
        }

        /// <summary>
        /// Get the destination of this path.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="HasPath"/> is <see langword="false"/> or <see cref="IsPending"/> is <see langword="true"/>.</exception>
        internal TInfo Destination {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                if (!HasPath)
                {
                    if (IsPending)
                        throw new InvalidOperationException(CAN_NOT_GET_DESTINATION_IF_PATH_IS_PENDING);
                    throw new InvalidOperationException(CAN_NOT_GET_DESTINATION_IF_PATH_WAS_NOT_FOUND);
                }
                return list[list.Count - 1];
            }
        }

        /// <inheritdoc cref="IPathFeedable{TInfo}"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathFeedable<TInfo>.Feed(IPathFeeder<TInfo> feeder)
        {
            if (processHandle.IsCompleteThreadSafe)
                throw new InvalidOperationException(CAN_NOT_FEED_IF_IS_NOT_PENDING);

            version++;
            list.Clear();
            // We don't check capacity for optimization because that is already done in AddRange.
            list.AddRange(feeder.GetPathInfo());
            if (feeder.HasPath)
                status = Status.Found;
            else if (feeder.HasTimedout)
                status = Status.Timedout;
        }

        /// <summary>
        /// Feeds a manual path.
        /// </summary>
        /// <param name="path">New path.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetManualPath(IEnumerable<TInfo> path)
        {
            if (!processHandle.IsCompleteThreadSafe)
                throw new InvalidOperationException(CAN_NOT_EXECUTE_IF_IS_PENDING);

            list.Clear();
            list.AddRange(path);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GuardVersion(int version)
        {
            if (version != this.version)
                throw new InvalidOperationException(PATH_WAS_MODIFIED_OUTDATED_ENUMERATOR);
        }

        /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <inheritdoc cref="IEnumerable.GetEnumerator"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        IEnumerator<TInfo> IEnumerable<TInfo>.GetEnumerator() => GetEnumerator();

        /// <inheritdoc cref="IProcessHandleSourceCompletition.Start"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        void IProcessHandleSourceCompletition.Start() => processHandle.Start();

        /// <inheritdoc cref="IProcessHandleSourceCompletition.SetJobHandle(JobHandle)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        void IProcessHandleSourceCompletition.SetJobHandle(JobHandle jobHandle) => processHandle.SetJobHandle(jobHandle);

        /// <inheritdoc cref="IProcessHandleSourceCompletition.CompleteJobHandle"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        void IProcessHandleSourceCompletition.CompleteJobHandle() => processHandle.CompleteJobHandle();

        /// <inheritdoc cref="IProcessHandleSourceCompletition.End"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IProcessHandleSourceCompletition.End() => processHandle.End();

        public struct Enumerator : IEnumerator<TInfo>
        {
            private const string CAN_NOT_ENUMERATE_PATH_WHICH_IS_PENDING = "Can't enumerate a path which is pending.";
            private const string MOVE_NEXT_MUST_BE_CALLED_AT_LEAST_ONCE_BEFORE = nameof(MoveNext) + " must be called at least once before.";

            private Path<TInfo> source;
            private int index;
            private int version;

            internal Enumerator(Path<TInfo> source)
            {
                if (source.IsPending)
                    throw new InvalidOperationException(CAN_NOT_ENUMERATE_PATH_WHICH_IS_PENDING);
                this.source = source;
                index = -1;
                version = source.version;
            }

            /// <inheritdoc cref="IEnumerator{T}.Current"/>
            public TInfo Current {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get {
                    source.GuardVersion(version);
                    if (index == -1)
                        throw new InvalidOperationException(MOVE_NEXT_MUST_BE_CALLED_AT_LEAST_ONCE_BEFORE);
                    return source.list[index];
                }
            }

            /// <inheritdoc cref="IEnumerator.Current"/>
            object IEnumerator.Current {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Current;
            }

            /// <inheritdoc cref="IDisposable.Dispose"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }

            /// <inheritdoc cref="IEnumerator.MoveNext"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                source.GuardVersion(version);

                if (index < source.list.Count - 1)
                {
                    index++;
                    return true;
                }
                return false;
            }

            /// <inheritdoc cref="IEnumerator.Reset"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset()
            {
                source.GuardVersion(version);
                index = -1;
            }
        }

        [Flags]
        private enum Status : byte
        {
            Found = 1 << 0,
            Timedout = 1 << 1,
            Reserved = 1 << 2,
        }
    }
}