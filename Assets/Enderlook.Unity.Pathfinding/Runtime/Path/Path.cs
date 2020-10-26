using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Represents a path.
    /// </summary>
    /// <typeparam name="TInfo">Node or coordinate type.</typeparam>
    public sealed class Path<TInfo> : IPathFeedable<TInfo>, IEnumerable<TInfo>
    {
        private readonly List<TInfo> list = new List<TInfo>();
        private PathState state;
#if UNITY_EDITOR || DEBUG
        private int version;
#endif

        /// <summary>
        /// Determines the amount of points this path has.
        /// </summary>
        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => list.Count;
        }

        /// <summary>
        /// Determines the state of this path.
        /// </summary>
        public PathState State {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => state;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set => state = value;
        }

        /// <summary>
        /// Get the destination of this path.<br/>
        /// This has undefined behaviour if <see cref="State"/> is <see cref="PathState.EmptyOrNotFound"/> or <see cref="PathState.InProgress"/>.
        /// </summary>
        public TInfo Destination {
            get {
#if UNITY_EDITOR || DEBUG
                if (State != PathState.PathFound)
                    throw new InvalidOperationException("Can't get destination if path is empty or in progess. This will not be thrown in release build, but undefined behaviuor.");
#endif
                return list[list.Count - 1];
            }
        }

        /// <inheritdoc cref="IPathFeedable{TInfo}"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathFeedable<TInfo>.Feed(IPathFeeder<TInfo> feeder)
        {
#if UNITY_EDITOR || DEBUG
            if (State == PathState.InProgress)
                throw new InvalidOperationException("Can't feed a path which is already in progress.");
#endif
            State = PathState.InProgress;
#if UNITY_EDITOR || DEBUG
        version++;
#endif
            list.Clear();
            // We don't check capacity for optimization because that is already done in AddRange.
            list.AddRange(feeder.GetPathInfo());
            State = feeder.Status.ToPathState();
        }

#if UNITY_EDITOR || DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GuardVersion(int version)
        {
            if (version != this.version)
                throw new InvalidOperationException("Path was modified; enumeration operation may not execute. This doesn't raise on release build.");
        }
#endif

        /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <inheritdoc cref="IEnumerable.GetEnumerator"/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator<TInfo> IEnumerable<TInfo>.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<TInfo>
        {
            private Path<TInfo> source;

            private int index;

#if UNITY_EDITOR || DEBUG
            private int version;
#endif

            public Enumerator(Path<TInfo> source)
            {
#if UNITY_EDITOR || DEBUG
                if (source.State == PathState.InProgress)
                    throw new InvalidOperationException("Can't enumerate a path which is in progress.");
#endif
                this.source = source;
                index = -1;
#if UNITY_EDITOR || DEBUG
                version = source.version;
#endif
            }

            /// <inheritdoc cref="IEnumerator{T}.Current"/>
            public TInfo Current {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get {
#if UNITY_EDITOR || DEBUG
                    source.GuardVersion(version);
                    if (index == -1)
                        throw new InvalidOperationException($"{nameof(MoveNext)} must be called at least once before. This doesn't raise on release build.");
#endif
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
#if UNITY_EDITOR || DEBUG
                source.GuardVersion(version);
#endif
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
#if UNITY_EDITOR || DEBUG
                source.GuardVersion(version);
#endif
                index = -1;
            }
        }
    }
}