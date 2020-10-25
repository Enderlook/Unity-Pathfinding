using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Represents a path.
    /// </summary>
    /// <typeparam name="T">Node or coordinate type.</typeparam>
    public sealed class Path<T> : IPathFeedable<IEnumerable<T>>, IPathFeedable<IReadOnlyList<T>>, IPathFeedable<IEnumerator<T>>, IEnumerable<T>
    {
        private readonly List<T> list = new List<T>();
#if UNITY_EDITOR || DEBUG
        private int version;
#endif

        /// <inheritdoc cref="IPathFeedable{TInfo}"/>
        void IPathFeedable<IEnumerable<T>>.Feed(IEnumerable<T> information)
        {
#if UNITY_EDITOR || DEBUG
        version++;
#endif
            list.Clear();
            // We don't check capacity for optimization because that is already done in AddRange.
            list.AddRange(information);
        }

        /// <inheritdoc cref="IPathFeedable{TInfo}"/>
        void IPathFeedable<IReadOnlyList<T>>.Feed(IReadOnlyList<T> information)
        {
#if UNITY_EDITOR || DEBUG
            version++;
#endif
            list.Clear();
            // We don't check capacity for optimization because that is already done in AddRange.
            list.AddRange(information);
        }

        /// <inheritdoc cref="IPathFeedable{TInfo}"/>
        public void Feed(IEnumerator<T> information)
        {
#if UNITY_EDITOR || DEBUG
            version++;
#endif
            list.Clear();
            while (information.MoveNext())
                list.Add(information.Current);
        }

#if UNITY_EDITOR || DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GuardVersion(int version)
        {
            if (version != this.version)
                throw new InvalidOperationException("Path was modified; enumeration operation may not execute. This doesn't raise on release build.");
        }

        /// <inheritdoc cref="IEnumerator{T}"/>
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <inheritdoc cref="IEnumerator{T}"/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc cref="IEnumerator{T}"/>
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
#endif

        public struct Enumerator : IEnumerator<T>
        {
            private Path<T> source;

            private int index;

#if UNITY_EDITOR || DEBUG
            private int version;
#endif

            public Enumerator(Path<T> source)
            {
                this.source = source;
                index = -1;
#if UNITY_EDITOR || DEBUG
                version = source.version;
#endif
            }

            /// <inheritdoc cref="IEnumerator{T}.Current"/>
            public T Current {
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