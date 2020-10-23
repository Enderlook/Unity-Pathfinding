using Enderlook.Collections;

using System;
using System.Collections.Generic;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Represents the internal builder of a path.
    /// </summary>
    /// <typeparam name="TNode">Node type.</typeparam>
    public sealed class PathBuilder<TNode> : IPathBuilder<TNode>, IPathFeeder<IReadOnlyList<TNode>>, IPathFeeder<IEnumerable<TNode>>
    {
        private readonly HashSet<TNode> visited = new HashSet<TNode>();
        private readonly BinaryHeapMin<TNode, float> toVisit = new BinaryHeapMin<TNode, float>();
        private readonly Dictionary<TNode, float> costs = new Dictionary<TNode, float>();
        private readonly Dictionary<TNode, TNode> edges = new Dictionary<TNode, TNode>();
        private readonly List<TNode> path = new List<TNode>();
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
            path.Clear();
        }

        /// <inheritdoc cref="IPathBuilder{TNode}.EnqueueToVisit(TNode, float)"/>
        void IPathBuilder<TNode>.EnqueueToVisit(TNode node, float priority) => toVisit.Enqueue(node, priority);

        /// <inheritdoc cref="IPathBuilder{TNode}.FinalizeBuilderSession(CalculationResult, TNode)"/>
        void IPathBuilder<TNode>.FinalizeBuilderSession(CalculationResult result, TNode targetNode)
        {
            Status = result.ToPathState();
            this.targetNode = targetNode;

            if (Status == PathState.PathFound)
            {
                if (edges.Count == 0)
                    path.Add(targetNode);
                else
                {
                    TNode to = targetNode;
                    path.Add(to);
                    while (edges.TryGetValue(to, out TNode from))
                    {
                        to = from;
                        path.Add(from);
                    }
                    path.Reverse();
                }
            }
        }

        /// <inheritdoc cref="IPathFeeder{TInfo}.FeedPathTo(IPathFeedable{TInfo})"/>
        public void FeedPathTo(IPathFeedable<IReadOnlyList<TNode>> path)
        {
            FeedToPathGuard();
            path.Feed(this.path);
        }

        /// <inheritdoc cref="IPathFeeder{TInfo}.FeedPathTo(IPathFeedable{TInfo})"/>
        public void FeedPathTo(IPathFeedable<IEnumerable<TNode>> path)
        {
            FeedToPathGuard();
            path.Feed(this.path);
        }

        /// <inheritdoc cref="IPathFeeder{TInfo}.FeedPathTo(IPathFeedable{TInfo})"/>
        public void FeedPathTo<T>(T path)
            where T : IPathFeedable<IEnumerable<TNode>>
        {
            FeedToPathGuard();
            path.Feed(this.path);
        }

        private void FeedToPathGuard()
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