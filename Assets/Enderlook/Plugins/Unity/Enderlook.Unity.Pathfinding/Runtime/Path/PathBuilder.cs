using Enderlook.Collections;

using System.Collections.Generic;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Represents the internal builder of a path.
    /// </summary>
    public sealed class PathBuilder<TNode> : IPathBuilder<TNode>
    {
        private readonly HashSet<TNode> visited = new HashSet<TNode>();
        private readonly BinaryHeapMin<TNode, float> toVisit = new BinaryHeapMin<TNode, float>();
        private readonly Dictionary<TNode, float> costs = new Dictionary<TNode, float>();
        private readonly Dictionary<TNode, TNode> edges = new Dictionary<TNode, TNode>();
        private TNode initialNode;
        private TNode targetNode;

        /// <inheritdoc cref="IPathBuilder{TNode}.Status"/>
        public PathState Status { get; private set; }

        /// <inheritdoc cref="IPathBuilder{TNode}.InitializeBuilderSession(TNode)"/>
        void IPathBuilder<TNode>.InitializeBuilderSession(TNode initialNode)
        {
            Status = PathState.InProgress;
            this.initialNode = initialNode;
            visited.Clear();
            toVisit.Clear();
            costs.Clear();
            edges.Clear();
        }

        /// <inheritdoc cref="IPathBuilder{TNode}.EnqueueToVisit(TNode, float)"/>
        void IPathBuilder<TNode>.EnqueueToVisit(TNode node, float priority) => toVisit.Enqueue(node, priority);

        /// <inheritdoc cref="IPathBuilder{TNode}.FinalizeBuilderSession(CalculationResult, TNode)"/>
        void IPathBuilder<TNode>.FinalizeBuilderSession(CalculationResult result, TNode targetNode)
        {
            Status = result.ToPathState();
            this.targetNode = targetNode;
        }

        /// <inheritdoc cref="IPathBuilder{TNode}.SetCost(TNode, float)"/>
        void IPathBuilder<TNode>.SetCost(TNode to, float cost) => costs[to] = cost;

        /// <inheritdoc cref="IPathBuilder{TNode}.SetEdge(TNode, TNode)"/>
        void IPathBuilder<TNode>.SetEdge(TNode from, TNode to) => edges[to] = from;

        /// <inheritdoc cref="IPathBuilder{TNode}.TryDequeueToVisit(out TNode)"/>
        bool IPathBuilder<TNode>.TryDequeueToVisit(out TNode node) => toVisit.TryDequeue(out node, out _);

        /// <inheritdoc cref="IPathBuilder{TNode}.TryGetCost(TNode, out float)"/>
        bool IPathBuilder<TNode>.TryGetCost(TNode to, out float cost) => costs.TryGetValue(to, out cost);

        /// <inheritdoc cref="IPathBuilder{TNode}.Visit(TNode)"/>
        void IPathBuilder<TNode>.Visit(TNode node) => visited.Add(node);

        /// <inheritdoc cref="IPathBuilder{TNode}.WasVisited(TNode)"/>
        bool IPathBuilder<TNode>.WasVisited(TNode node) => visited.Contains(node);
    }
}