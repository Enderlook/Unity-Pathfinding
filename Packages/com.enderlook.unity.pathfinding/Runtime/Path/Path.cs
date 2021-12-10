using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Unity.Pathfinding.Utils;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Represents a path.
    /// </summary>
    /// <typeparam name="TInfo">Node or coordinate type.</typeparam>
    public sealed class Path<TInfo> : IPathFeedable<TInfo>, ISetTask, IEnumerable<TInfo>, IDisposable
    {
        private RawPooledList<TInfo> list = RawPooledList<TInfo>.Create();
        private int version;
        private Status status;
        private ValueTask task;

        /// <summary>
        /// Extract the underlying span of this path.
        /// </summary>
        internal Span<TInfo> AsSpan {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => list.AsSpan();
        }

        /// <summary>
        /// Determines if the path calculation has completed
        /// </summary>
        public bool IsCompleted {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => task.IsCompleted;
        }

        private bool IsTrulyCompleted {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (status & Status.IsPending) == 0;
        }

        /// <summary>
        /// Determines if this path contains an actual path.<br/>
        /// This will return <see langword="false"/> if it's empty, hasn't found a path, or <see cref="IsPending"/> is <see langword="true"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="IsPending"/> is <see langword="true"/>.</exception>
        public bool HasPath {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                if (!IsTrulyCompleted) ThrowInvalidOperationException_HasNotCompleted();
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
                if (!IsTrulyCompleted) ThrowInvalidOperationException_HasNotCompleted();
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
                    if (!IsTrulyCompleted) ThrowInvalidOperationException_HasNotCompleted();
                    ThrowInvalidOperationException_PathWasNotFound();
                }
                return list[list.Count - 1];
            }
        }

        /// <summary>
        /// Completes calculation of path.
        /// </summary>
        public void Complete()
        {
            ValueTask task_ = task;
            task = default;
            task_.GetAwaiter().GetResult();
        }

        /// <inheritdoc cref="ISetTask.SetTask(ValueTask)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ISetTask.SetTask(ValueTask task)
        {
            status = Status.IsPending;
            this.task = task;
        }

        /// <inheritdoc cref="IPathFeedable{TInfo}.Feed(IPathFeeder{TInfo})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathFeedable<TInfo>.Feed(IPathFeeder<TInfo> feeder)
        {
            Debug.Assert(!task.IsCompleted);
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
            Debug.Assert(IsTrulyCompleted);
            list.Clear();
            list.AddRange(path);
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

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose() => list.Dispose();

        public struct Enumerator : IEnumerator<TInfo>
        {
            private Path<TInfo> source;
            private int index;
            private int version;

            internal Enumerator(Path<TInfo> source)
            {
                if (!source.IsTrulyCompleted) ThrowInvalidOperationException_HasNotCompleted();
                this.source = source;
                index = -1;
                version = source.version;
            }

            /// <inheritdoc cref="IEnumerator{T}.Current"/>
            public TInfo Current {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get {
                    if (version != source.version) ThrowInvalidOperationException_PathWasModified();
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
                if (version != source.version) ThrowInvalidOperationException_PathWasModified();

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
                if (version != source.version) ThrowInvalidOperationException_PathWasModified();
                index = -1;
            }

            private static void ThrowInvalidOperationException_PathWasModified()
                => throw new InvalidOperationException("Path was modified; enumeration operation may not execute.");
        }

        [Flags]
        private enum Status : byte
        {
            Found = 1 << 0,
            Timedout = 1 << 1,
            IsPending = 1 << 2,
        }

        private static void ThrowInvalidOperationException_HasNotCompleted()
            => throw new InvalidOperationException($"Can't execute if method {nameof(Complete)} was not executed.");

        private static void ThrowInvalidOperationException_PathWasNotFound()
            => throw new InvalidOperationException("Can't execute if path was not found.");
    }
}