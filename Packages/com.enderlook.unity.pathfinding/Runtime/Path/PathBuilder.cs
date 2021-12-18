using Enderlook.Collections;
using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Unity.Threading;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Represents the internal builder of a path.
    /// </summary>
    /// <typeparam name="TNode">Node type.</typeparam>
    /// <typeparam name="TCoord">Coordinate type.</typeparam>
    internal sealed class PathBuilder<TNode, TCoord> : IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>, IDisposable
    {
        private readonly HashSet<TNode> visited = new HashSet<TNode>();
        private readonly BinaryHeapMin<TNode, float> toVisit = new BinaryHeapMin<TNode, float>();
        private readonly Dictionary<TNode, float> costs = new Dictionary<TNode, float>();
        private readonly Dictionary<TNode, TNode> edges = new Dictionary<TNode, TNode>();
        private RawPooledList<TCoord> path = RawPooledList<TCoord>.Create();

        private TCoord startPosition;
        private TCoord endPosition;
        private TNode endNode;
        private TNode startNode;

        private Status status;

        /// <summary>
        /// Determines if this builder has a path.
        /// </summary>
        public bool HasPath {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (status & Status.Found) == Status.Found;
        }

        /// <summary>
        /// Determines if this builder is pending.
        /// </summary>
        public bool IsPending {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (status & Status.Initialized) == Status.Initialized;
        }

        /// <summary>
        /// Determines if the calculation was aborted due to a timedout.<br/>
        /// This method will return <see langword="false"/> if <see cref="HasPath"/> or <see cref="IsPending"/> are <see langword="true"/>.
        /// </summary>
        public bool HasTimedout {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (status & Status.Timedout) == Status.Timedout;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.EnqueueToVisit(TNode, float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode, TCoord>.EnqueueToVisit(TNode node, float priority)
        {
            Debug.Assert(status == Status.Initialized);
            toVisit.Enqueue(node, priority);
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.FinalizeBuilderSession{TGraph, TWatchdog, TAwaitable, TAwaiter}(TGraph, CalculationResult, TWatchdog)"/>
        async ValueTask IPathBuilder<TNode, TCoord>.FinalizeBuilderSession<TGraph, TWatchdog, TAwaitable, TAwaiter>(TGraph graph, CalculationResult result, TWatchdog watchdog)
        {
            if ((status & Status.Initialized) == 0) ThrowInvalidOperationException_IsNotInitialized();

            if (result == CalculationResult.PathFound)
            {
                EqualityComparer<TCoord> coordComparer = typeof(TCoord).IsValueType ? null : EqualityComparer<TCoord>.Default;

                path.Clear();
                if (typeof(IGraphLineOfSight<TCoord>).IsAssignableFrom(typeof(TGraph)))
                {
                    // Optimize path using line of sight.
                    IGraphLineOfSight<TCoord> lineOfSight = typeof(TGraph).IsValueType ? null : (IGraphLineOfSight<TCoord>)graph;

                    path.Add(endPosition);

                    bool requiresSwitch = (typeof(TGraph).IsValueType ? ((IGraphLineOfSight<TCoord>)graph).RequiresUnityThread : lineOfSight.RequiresUnityThread) && !UnityThread.IsMainThread;
                    if (requiresSwitch)
                        await Switch.ToUnity;

                    TCoord previous;
                    TCoord current = endPosition;
                    TCoord lastOptimized = current;

                    TNode to = endNode;
                    TCoord end2 = graph.ToPosition(to);
                    if (!(typeof(TCoord).IsValueType ?
                        EqualityComparer<TCoord>.Default.Equals(endPosition, end2)
                        : coordComparer.Equals(endPosition, end2)))
                    {
                        previous = current;
                        current = end2;
                        if (!(typeof(TGraph).IsValueType ? 
                            ((IGraphLineOfSight<TCoord>)graph).HasLineOfSight(lastOptimized, current)
                            : lineOfSight.HasLineOfSight(lastOptimized, current)))
                        {
                            path.Add(previous);
                            lastOptimized = previous;
                        }
                    }

#if UNITY_ASSERTIONS
                    visited.Clear();
                    visited.Add(to);
#endif

                    while (edges.TryGetValue(to, out TNode from))
                    {
#if UNITY_ASSERTIONS
                        if (!visited.Add(from))
                        {
                            Debug.LogError("Assertion failed. The calculated path has an endless loop. This assertion is only performed when flag UNITY_ASSERTIONS is enabled.");
                            break;
                        }
#endif
                        to = from;

                        previous = current;
                        current = graph.ToPosition(from);
                        if (!(typeof(TGraph).IsValueType ?
                             ((IGraphLineOfSight<TCoord>)graph).HasLineOfSight(lastOptimized, current)
                             : lineOfSight.HasLineOfSight(lastOptimized, current)))
                        {
                            path.Add(previous);
                            lastOptimized = previous;
                        }

                        if (watchdog.CanContinue(out TAwaitable awaitable))
                            await awaitable;
                        else
                            goto timedout;
                    }

                    TCoord start2 = graph.ToPosition(startNode);
                    if (!(typeof(TCoord).IsValueType ?
                        EqualityComparer<TCoord>.Default.Equals(start2, path[path.Count - 1])
                        : coordComparer.Equals(start2, path[path.Count - 1])))
                    {
                        previous = current;
                        current = start2;
                        if (!(typeof(TGraph).IsValueType ?
                            ((IGraphLineOfSight<TCoord>)graph).HasLineOfSight(lastOptimized, current)
                            : lineOfSight.HasLineOfSight(lastOptimized, current)))
                        {
                            path.Add(previous);
                            lastOptimized = previous;
                        }
                    }

                    if (!(typeof(TCoord).IsValueType ?
                        EqualityComparer<TCoord>.Default.Equals(startPosition, start2)
                        : coordComparer.Equals(startPosition, startPosition)))
                    {
                        previous = current;
                        current = startPosition;
                        if (!(typeof(TGraph).IsValueType ?
                            ((IGraphLineOfSight<TCoord>)graph).HasLineOfSight(lastOptimized, current)
                            : lineOfSight.HasLineOfSight(lastOptimized, current)))
                        {
                            path.Add(previous);
                            lastOptimized = previous;
                        }
                    }

                    if (path.Count == 1)
                        path.Add(endPosition);

                    if (requiresSwitch)
                        await Switch.ToBackground;

                    path.Reverse();
                }
                else
                {
                    if (edges.Count == 0)
                    {
                        path.Add(startPosition);

                        TCoord start2 = graph.ToPosition(startNode);

                        if (!(typeof(TCoord).IsValueType ?
                            EqualityComparer<TCoord>.Default.Equals(startPosition, start2)
                            : coordComparer.Equals(startPosition, start2)))
                            path.Add(start2);

                        TCoord end2 = graph.ToPosition(endNode);
                        if (!(typeof(TCoord).IsValueType ?
                            EqualityComparer<TCoord>.Default.Equals(endPosition, end2)
                            : coordComparer.Equals(endPosition, end2)))
                            path.Add(end2);

                        path.Add(endPosition);
                    }
                    else
                    {
                        path.Add(endPosition);

                        TNode to = endNode;
                        TCoord end2 = graph.ToPosition(to);
                        if (!(typeof(TCoord).IsValueType ?
                            EqualityComparer<TCoord>.Default.Equals(endPosition, end2)
                            : coordComparer.Equals(endPosition, end2)))
                            path.Add(end2);

#if UNITY_ASSERTIONS
                        visited.Clear();
                        visited.Add(to);
#endif

                        while (edges.TryGetValue(to, out TNode from))
                        {
#if UNITY_ASSERTIONS
                            if (!visited.Add(from))
                            {
                                Debug.LogError("Assertion failed. The calculated path has an endless loop. This assertion is only performed when flag UNITY_ASSERTIONS is enabled.");
                                break;
                            }
#endif
                            to = from;
                            path.Add(graph.ToPosition(from));

                            if (watchdog.CanContinue(out TAwaitable awaitable))
                                await awaitable;
                            else
                                goto timedout;
                        }

                        TCoord start2 = graph.ToPosition(startNode);
                        if (!(typeof(TCoord).IsValueType ?
                            EqualityComparer<TCoord>.Default.Equals(path[path.Count - 1], start2)
                            : coordComparer.Equals(path[path.Count - 1], start2)))
                            path.Add(start2);

                        if (!(typeof(TCoord).IsValueType ?
                            EqualityComparer<TCoord>.Default.Equals(startPosition, start2)
                            : coordComparer.Equals(startPosition, start2)))
                            path.Add(startPosition);

                        path.Reverse();
                    }
                }

                // Finalize
                status = Status.Found;
            }
            else if (result == CalculationResult.Timedout)
                goto timedout;
            else
                status = Status.Finalized;

            return;
            timedout:
            status = Status.Timedout;
        }

        /// <inheritdoc cref="IPathFeeder{TInfo}.GetPathInfo"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ReadOnlySpan<TCoord> IPathFeeder<TCoord>.GetPathInfo()
        {
            if ((status & Status.Finalized) == 0) ThrowInvalidOperationException_IsNotFinalized();
            return path.AsSpan();
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.InitializeBuilderSession()"/>
        void IPathBuilder<TNode, TCoord>.InitializeBuilderSession()
        {
            if ((status & Status.Initialized) != 0) ThrowInvalidOperationException_IsAlreadyInitialized();

            status = Status.Initialized;

            visited.Clear();
            toVisit.Clear();
            costs.Clear();
            edges.Clear();
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetCost(TNode, float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode, TCoord>.SetCost(TNode to, float cost)
        {
            Debug.Assert((status & Status.Initialized) != 0);
            costs[to] = cost;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetEdge(TNode, TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode, TCoord>.SetEdge(TNode from, TNode to)
        {
            Debug.Assert((status & Status.Initialized) != 0);
            Debug.Assert(!EqualityComparer<TNode>.Default.Equals(from, to));
            edges[to] = from;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetEnd(TCoord, TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode, TCoord>.SetEnd(TCoord endPosition, TNode endNode)
        {
            Debug.Assert((status & Status.Initialized) != 0);
            this.endNode = endNode;
            this.endPosition = endPosition;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetStart(TCoord, TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode, TCoord>.SetStart(TCoord startPosition, TNode startNode)
        {
            Debug.Assert((status & Status.Initialized) != 0);
            this.startNode = startNode;
            this.startPosition = startPosition;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.TryDequeueToVisit(out TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IPathBuilder<TNode, TCoord>.TryDequeueToVisit(out TNode node)
        {
            Debug.Assert((status & Status.Initialized) != 0);
            return toVisit.TryDequeue(out node, out _);
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.TryGetCost(TNode, out float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IPathBuilder<TNode, TCoord>.TryGetCost(TNode to, out float cost)
        {
            Debug.Assert((status & Status.Initialized) != 0);
            return costs.TryGetValue(to, out cost);
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.TryGetEdge(TNode, out TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IPathBuilder<TNode, TCoord>.TryGetEdge(TNode to, out TNode from)
        {
            Debug.Assert((status & Status.Initialized) != 0);
            return edges.TryGetValue(to, out from);
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.VisitIfWasNotVisited(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IPathBuilder<TNode, TCoord>.VisitIfWasNotVisited(TNode node)
        {
            Debug.Assert((status & Status.Initialized) != 0);
            return visited.Add(node);
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose() => path.Dispose();

        [Flags]
        private enum Status : byte
        {
            Initialized = 1 << 0,
            Finalized = 1 << 1,
            Found = Finalized | 1 << 2,
            Timedout = Finalized | 1 << 3,
        }

        private static void ThrowInvalidOperationException_IsNotFinalized()
            => throw new InvalidOperationException("Session has not finalized.");

        private static void ThrowInvalidOperationException_IsAlreadyInitialized()
            => throw new InvalidOperationException("Session is already initialized.");

        private static void ThrowInvalidOperationException_IsNotInitialized()
            => throw new InvalidOperationException("Session has not initialized.");
    }
}
