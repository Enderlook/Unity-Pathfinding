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
    internal struct PathBuilderInner<TNode> : IPathBuilder<TNode>
    {
        private readonly HashSet<TNode> visited;
        private readonly BinaryHeapMin<TNode, float> toVisit;
        private readonly Dictionary<TNode, float> costs;
        public readonly Dictionary<TNode, TNode> edges;
        private PathState status;
        private TNode end;
        private TNode start;

        /// <inheritdoc cref="IPathBuilder{TNode}.Status"/>
        public PathState Status {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => status;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set => status = value;
        }

        public TNode Start {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => start;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set => start = value;
        }

        public TNode End {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => end;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set => end = value;
        }

        public PathBuilderInner(bool _)
        {
            visited = new HashSet<TNode>();
            toVisit = new BinaryHeapMin<TNode, float>();
            costs = new Dictionary<TNode, float>();
            edges = new Dictionary<TNode, TNode>();
            status = PathState.Empty;
            end = default;
            start = default;
        }

        /// <inheritdoc cref="IPathBuilder{TNode}.InitializeBuilderSession(TNode)"/>
        public void InitializeBuilderSession()
        {
            Status = PathState.InProgress;
            visited.Clear();
            toVisit.Clear();
            costs.Clear();
            edges.Clear();
        }

        /// <inheritdoc cref="IPathBuilder{TNode}.EnqueueToVisit(TNode, float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnqueueToVisit(TNode node, float priority) => toVisit.Enqueue(node, priority);

        /// <inheritdoc cref="IPathBuilder{TNode}.FinalizeBuilderSession(CalculationResult)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FinalizeBuilderSession(CalculationResult result) => Status = result.ToPathState();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void FeedToPathGuard()
        {
            switch (Status)
            {
                case PathState.Empty:
                    throw new InvalidOperationException("Can't create path if path builder is empty.");
                case PathState.InProgress:
                    throw new InvalidOperationException("Can't create path while path builder is in progress.");
                case PathState.PathNotFound:
                    throw new InvalidOperationException("Can't create path because no path was found.");
                case PathState.Timedout:
                    throw new InvalidOperationException("Can't create path because path building was aborted prematurely.");
            }
        }

        /// <inheritdoc cref="IPathBuilder{TNode}.SetCost(TNode, float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetCost(TNode to, float cost) => costs[to] = cost;

        /// <inheritdoc cref="IPathBuilder{TNode}.SetEdge(TNode, TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetEdge(TNode from, TNode to) => edges[to] = from;

        /// <inheritdoc cref="IPathBuilder{TNode}.SetEnd(TNode)(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetEnd(TNode end) => End = end;

        /// <inheritdoc cref="IPathBuilder{TNode}.SetStart(TNode)(TNode)(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStart(TNode start) => Start = start;

        /// <inheritdoc cref="IPathBuilder{TNode}.TryDequeueToVisit(out TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeueToVisit(out TNode node) => toVisit.TryDequeue(out node, out _);

        /// <inheritdoc cref="IPathBuilder{TNode}.TryGetCost(TNode, out float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetCost(TNode to, out float cost) => costs.TryGetValue(to, out cost);

        /// <inheritdoc cref="IPathBuilder{TNode}.Visit(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Visit(TNode node) => visited.Add(node);

        /// <inheritdoc cref="IPathBuilder{TNode}.WasVisited(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WasVisited(TNode node) => visited.Contains(node);
    }
}