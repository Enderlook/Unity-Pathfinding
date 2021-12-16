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
        private RawPooledList<TCoord> pathRaw = RawPooledList<TCoord>.Create();
        private RawPooledList<TCoord> pathOptimized = RawPooledList<TCoord>.Create();

        private IGraphLocation<TNode, TCoord> converter;
        private IGraphLineOfSight<TCoord> lineOfsight;

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

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.FinalizeBuilderSession(CalculationResult)"/>
        async ValueTask IPathBuilder<TNode, TCoord>.FinalizeBuilderSession<TWatchdog, TAwaitable, TAwaiter>(CalculationResult result, TWatchdog watchdog)
        {
            if ((status & Status.Initialized) == 0) ThrowInvalidOperationException_IsNotInitialized();

            if (result == CalculationResult.PathFound)
            {
                // Produce path
                pathRaw.Clear();
                if (edges.Count == 0)
                {
                    pathRaw.Add(startPosition);

                    TCoord start2 = converter.ToPosition(startNode);
                    if (!EqualityComparer<TCoord>.Default.Equals(startPosition, start2))
                        pathRaw.Add(start2);

                    TCoord end2 = converter.ToPosition(endNode);
                    if (!EqualityComparer<TCoord>.Default.Equals(endPosition, end2))
                        pathRaw.Add(end2);

                    pathRaw.Add(endPosition);
                }
                else
                {
                    pathRaw.Add(endPosition);

                    TNode to = endNode;
                    TCoord end2 = converter.ToPosition(to);
                    if (!EqualityComparer<TCoord>.Default.Equals(endPosition, end2))
                        pathRaw.Add(end2);

                    while (edges.TryGetValue(to, out TNode from))
                    {
                        to = from;
                        pathRaw.Add(converter.ToPosition(from));

                        if (watchdog.CanContinue(out TAwaitable awaitable))
                            await awaitable;
                        else
                            goto timedout;
                    }

                    TCoord start2 = converter.ToPosition(startNode);
                    if (!EqualityComparer<TCoord>.Default.Equals(start2, pathRaw[pathRaw.Count - 1]))
                        pathRaw.Add(start2);

                    if (!EqualityComparer<TCoord>.Default.Equals(start2, startPosition))
                        pathRaw.Add(startPosition);

                    pathRaw.Reverse();
                }

                // Optimize path
                Debug.Assert(pathRaw.Count >= 2);
                pathOptimized.Clear();

                if (pathRaw.Count == 2)
                {
                    pathOptimized.Add(pathRaw[0]);
                    pathOptimized.Add(pathRaw[1]);
                    return;
                }

                pathOptimized.Add(pathRaw[0]);

                bool requiresSwitch = lineOfsight.RequiresUnityThread && !UnityThread.IsMainThread;
                if (requiresSwitch)
                    await Switch.ToUnity;

                TCoord previous;
                TCoord current = pathRaw[0];
                TCoord lastOptimized = current;
                for (int i = 1; i < pathRaw.Count; i++)
                {
                    previous = current;
                    current = pathRaw[i];
                    //if (lineOfsight.HasLineOfSight(lastOptimized, current))
                    //    goto toContinue;
                    pathOptimized.Add(previous);
                    lastOptimized = previous;

                    toContinue:
                    if (watchdog.CanContinue(out TAwaitable awaitable))
                        await awaitable;
                    else
                        goto timedout;
                }

                TCoord last = pathRaw[pathRaw.Count - 1];
                if (pathOptimized.Count > 1)
                {
                    if (lineOfsight.HasLineOfSight(pathOptimized[pathOptimized.Count - 2], last))
                        pathOptimized[pathOptimized.Count - 1] = last;
                    else
                        pathOptimized.Add(last);
                }
                else
                    pathOptimized.Add(last);

                if (requiresSwitch)
                    await Switch.ToBackground;

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
            return pathOptimized.AsSpan();
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.InitializeBuilderSession(TNode)"/>
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
            edges[to] = from;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetEnd(TNode)(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode, TCoord>.SetEnd(TCoord endPosition, TNode endNode)
        {
            Debug.Assert((status & Status.Initialized) != 0);
            this.endNode = endNode;
            this.endPosition = endPosition;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetGraphLocation(IGraphLocation{TNode, TCoord})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetGraphLocation(IGraphLocation<TNode, TCoord> converter)
        {
            Debug.Assert((status & Status.Initialized) != 0);
            this.converter = converter;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetLineOfSight(IGraphLineOfSight{TCoord})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetLineOfSight(IGraphLineOfSight<TCoord> lineOfSight)
        {
            Debug.Assert((status & Status.Initialized) != 0);
            lineOfsight = lineOfSight;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetStart(TNode)(TNode)(TNode)"/>
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

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.Visit(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode, TCoord>.Visit(TNode node)
        {
            Debug.Assert((status & Status.Initialized) != 0);
            visited.Add(node);
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.WasVisited(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IPathBuilder<TNode, TCoord>.WasVisited(TNode node)
        {
            Debug.Assert((status & Status.Initialized) != 0);
            return visited.Contains(node);
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            pathRaw.Dispose();
            pathOptimized.Dispose();
        }

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