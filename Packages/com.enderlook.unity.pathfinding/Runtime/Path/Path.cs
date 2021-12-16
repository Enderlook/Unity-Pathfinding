using Enderlook.Collections.LowLevel;
using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Pools;
using Enderlook.Unity.Pathfinding.Utils;
using Enderlook.Unity.Threading;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Represents a path.
    /// </summary>
    /// <typeparam name="TInfo">Node or coordinate type.</typeparam>
    public sealed class Path<TInfo> : IPathFeedable<TInfo>, IEnumerable<TInfo>, IDisposable
    {
        private RawPooledList<TInfo> list = RawPooledList<TInfo>.Create();
        private int version;
        private Status status;
        private readonly TimeSlicer timeSlicer = new TimeSlicer();

        private static RawList<Path<TInfo>> paths = RawList<Path<TInfo>>.Create();
        private static int pathsLock;

        static Path()
        {
            UnityThread.OnUpdate += () =>
            {
                Lock(ref pathsLock);
                {
                    int j = 0;
                    for (int i = 0; i < paths.Count; i++)
                    {
                        Path<TInfo> path = paths[i];
                        if (path.IsCompleted)
                        {
                            path.ResetAndReturnToPool();
                            continue;
                        }
                        paths[j++] = path;
                    }
                    paths = RawList<Path<TInfo>>.From(paths.UnderlyingArray, j);
                }
                Unlock(ref pathsLock);
            };
        }

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
                if (!IsCompleted) ThrowInvalidOperationException_HasNotCompleted();
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
                if (!IsCompleted) ThrowInvalidOperationException_HasNotCompleted();
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
                    if (!IsCompleted) ThrowInvalidOperationException_HasNotCompleted();
                    ThrowInvalidOperationException_PathWasNotFound();
                }
                return list[list.Count - 1];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TimeSlicer Start()
        {
            status = Status.IsPending;
            timeSlicer.Reset();
            return timeSlicer;
        }

        /// <inheritdoc cref="IPathFeedable{TInfo}.Feed(IPathFeeder{TInfo})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathFeedable<TInfo>.Feed<TFeeder>(TFeeder feeder)
        {
            Debug.Assert(!timeSlicer.IsCompleted);
            version++;
            list.Clear();
            // We don't check capacity for optimization because that is already done in AddRange.
            list.AddRange(feeder.GetPathInfo());
            if (feeder.HasPath)
                status = Status.Found;
            else if (feeder.HasTimedout)
                status = Status.Timedout;
            else
                status = Status.None;
        }

        /// <summary>
        /// Manually set that path was not found.
        /// </summary>
        internal void ManualSetNotFound()
        {
            version++;
            list.Clear();
            status = Status.None;
        }

        /// <summary>
        /// Feeds a manual path.
        /// </summary>
        /// <param name="path">New path.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetManualPath(IEnumerable<TInfo> path)
        {
            Debug.Assert(IsCompleted);
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
            private readonly Path<TInfo> source;
            private readonly int version;
            private int index;

            internal Enumerator(Path<TInfo> source)
            {
                if (!source.IsCompleted) ThrowInvalidOperationException_HasNotCompleted();
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

        internal void SendToPool()
        {
            if (IsCompleted)
                ResetAndReturnToPool();
            else
            {
                timeSlicer.RequestCancellation();
                Lock(ref pathsLock);
                {
                    paths.Add(this);
                }
                Unlock(ref pathsLock);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetAndReturnToPool()
        {
            version++;
            list.Clear();
            status = Status.None;
            ObjectPool<Path<TInfo>>.Shared.Return(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Path<TInfo> Rent() => ObjectPool<Path<TInfo>>.Shared.Rent();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Lock(ref int @lock)
        {
            while (Interlocked.Exchange(ref @lock, 1) == 1) ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Unlock(ref int @lock) => @lock = 0;

        [Flags]
        private enum Status : byte
        {
            None = 0,
            Found = 1 << 0,
            Timedout = 1 << 1,
            IsPending = 1 << 2,
        }

        private static void ThrowInvalidOperationException_HasNotCompleted()
            => throw new InvalidOperationException($"Can't execute if property {nameof(IsCompleted)} returns false.");

        private static void ThrowInvalidOperationException_PathWasNotFound()
            => throw new InvalidOperationException("Can't execute if path was not found.");
    }
}