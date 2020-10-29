using Enderlook.Collections;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Represents the internal builder of a path.
    /// </summary>
    /// <typeparam name="TNode">Node type.</typeparam>
    /// <typeparam name="TCoord">Coordinate type.</typeparam>
    internal sealed class PathBuilder<TNode, TCoord> : IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>
    {
        private const string CAN_NOT_INITIALIZE_ALREADY_INITIALIZED = "Can't initialize a builder session because it's already initialized.";
        private const string CAN_NOT_EXECUTE_IF_IS_NOT_FINALIZED = "Can't execute if the session hasn't finalized";
        private const string CAN_NOT_EXECUTE_IT_IS_NOT_INITIALIZED_DEBUG_ONLY = "It's not initialized. This doesn't happens in export release.";
        private const string CAN_NOT_FINALIZE_IF_WAS_NOT_INITIALIZED = "Can't finalize session because it was never initialized.";

        private readonly HashSet<TNode> visited = new HashSet<TNode>();
        private readonly BinaryHeapMin<TNode, float> toVisit = new BinaryHeapMin<TNode, float>();
        private readonly Dictionary<TNode, float> costs = new Dictionary<TNode, float>();
        private readonly Dictionary<TNode, TNode> edges = new Dictionary<TNode, TNode>();
        private readonly List<TCoord> path = new List<TCoord>();

        private IGraphLocation<TNode, TCoord> converter;
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
#if UNITY_EDITOR || DEBUG
            if (status != Status.Initialized)
                throw new InvalidOperationException(CAN_NOT_EXECUTE_IT_IS_NOT_INITIALIZED_DEBUG_ONLY);
#endif

            toVisit.Enqueue(node, priority);
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.FinalizeBuilderSession(CalculationResult)"/>
        void IPathBuilder<TNode, TCoord>.FinalizeBuilderSession(CalculationResult result)
        {
            if ((status & Status.Initialized) == 0)
                throw new InvalidOperationException(CAN_NOT_FINALIZE_IF_WAS_NOT_INITIALIZED);

            if (result == CalculationResult.PathFound)
            {
                path.Clear();
                if (edges.Count == 0)
                {
                    path.Add(startPosition);

                    TCoord start2 = converter.ToPosition(startNode);
                    if (!EqualityComparer<TCoord>.Default.Equals(startPosition, start2))
                        path.Add(start2);

                    TCoord end2 = converter.ToPosition(endNode);
                    if (!EqualityComparer<TCoord>.Default.Equals(endPosition, end2))
                        path.Add(end2);

                    path.Add(endPosition);
                }
                else
                {
                    path.Add(endPosition);

                    TNode to = endNode;
                    TCoord end2 = converter.ToPosition(to);
                    if (!EqualityComparer<TCoord>.Default.Equals(endPosition, end2))
                        path.Add(end2);

                    while (edges.TryGetValue(to, out TNode from))
                    {
                        to = from;
                        path.Add(converter.ToPosition(from));
                    }

                    TCoord start2 = converter.ToPosition(startNode);
                    if (!EqualityComparer<TCoord>.Default.Equals(start2, path[path.Count - 1]))
                        path.Add(start2);

                    if (!EqualityComparer<TCoord>.Default.Equals(start2, startPosition))
                        path.Add(startPosition);

                    path.Reverse();
                }
                status = Status.Found;
            }
            else if (result == CalculationResult.Timedout)
                status = Status.Timedout;
            else
                status = Status.Finalized;
        }

        /// <inheritdoc cref="IPathFeeder{TInfo}.GetPathInfo"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerable<TCoord> IPathFeeder<TCoord>.GetPathInfo()
        {
            if ((status & Status.Finalized) == 0)
                throw new InvalidOperationException(CAN_NOT_EXECUTE_IF_IS_NOT_FINALIZED);

            return path;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.InitializeBuilderSession(TNode)"/>
        void IPathBuilder<TNode, TCoord>.InitializeBuilderSession()
        {
            if ((status & Status.Initialized) != 0)
                throw new InvalidOperationException(CAN_NOT_INITIALIZE_ALREADY_INITIALIZED);

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
#if UNITY_EDITOR || DEBUG
            if ((status & Status.Initialized) == 0)
                throw new InvalidOperationException(CAN_NOT_EXECUTE_IT_IS_NOT_INITIALIZED_DEBUG_ONLY);
#endif

            costs[to] = cost;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetEdge(TNode, TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode, TCoord>.SetEdge(TNode from, TNode to)
        {
#if UNITY_EDITOR || DEBUG
            if ((status & Status.Initialized) == 0)
                throw new InvalidOperationException(CAN_NOT_EXECUTE_IT_IS_NOT_INITIALIZED_DEBUG_ONLY);
#endif

            edges[to] = from;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetEnd(TNode)(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode, TCoord>.SetEnd(TCoord endPosition, TNode endNode)
        {
#if UNITY_EDITOR || DEBUG
            if ((status & Status.Initialized) == 0)
                throw new InvalidOperationException(CAN_NOT_EXECUTE_IT_IS_NOT_INITIALIZED_DEBUG_ONLY);
#endif

            this.endNode = endNode;
            this.endPosition = endPosition;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetNodeToPositionConverter(IGraphLocation{TNode, TCoord})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetNodeToPositionConverter(IGraphLocation<TNode, TCoord> converter)
        {
#if UNITY_EDITOR || DEBUG
            if ((status & Status.Initialized) == 0)
                throw new InvalidOperationException(CAN_NOT_EXECUTE_IT_IS_NOT_INITIALIZED_DEBUG_ONLY);
#endif

            this.converter = converter;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetStart(TNode)(TNode)(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode, TCoord>.SetStart(TCoord startPosition, TNode startNode)
        {
#if UNITY_EDITOR || DEBUG
            if ((status & Status.Initialized) == 0)
                throw new InvalidOperationException(CAN_NOT_EXECUTE_IT_IS_NOT_INITIALIZED_DEBUG_ONLY);
#endif

            this.startNode = startNode;
            this.startPosition = startPosition;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.TryDequeueToVisit(out TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IPathBuilder<TNode, TCoord>.TryDequeueToVisit(out TNode node)
        {
#if UNITY_EDITOR || DEBUG
            if ((status & Status.Initialized) == 0)
                throw new InvalidOperationException(CAN_NOT_EXECUTE_IT_IS_NOT_INITIALIZED_DEBUG_ONLY);
#endif

            return toVisit.TryDequeue(out node, out _);
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.TryGetCost(TNode, out float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IPathBuilder<TNode, TCoord>.TryGetCost(TNode to, out float cost)
        {
#if UNITY_EDITOR || DEBUG
            if ((status & Status.Initialized) == 0)
                throw new InvalidOperationException(CAN_NOT_EXECUTE_IT_IS_NOT_INITIALIZED_DEBUG_ONLY);
#endif

            return costs.TryGetValue(to, out cost);
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.Visit(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode, TCoord>.Visit(TNode node)
        {
#if UNITY_EDITOR || DEBUG
            if ((status & Status.Initialized) == 0)
                throw new InvalidOperationException(CAN_NOT_EXECUTE_IT_IS_NOT_INITIALIZED_DEBUG_ONLY);
#endif

            visited.Add(node);
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.WasVisited(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IPathBuilder<TNode, TCoord>.WasVisited(TNode node)
        {
#if UNITY_EDITOR || DEBUG
            if ((status & Status.Initialized) == 0)
                throw new InvalidOperationException(CAN_NOT_EXECUTE_IT_IS_NOT_INITIALIZED_DEBUG_ONLY);
#endif

            return visited.Contains(node);
        }

        [Flags]
        private enum Status : byte
        {
            Initialized = 1 << 0,
            Finalized = 1 << 1,
            Found = Finalized | 1 << 2,
            Timedout = Finalized | 1 << 3,
        }
    }
}