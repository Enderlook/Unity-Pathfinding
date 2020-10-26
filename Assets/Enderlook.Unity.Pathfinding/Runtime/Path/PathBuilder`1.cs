using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Represents the internal builder of a path.
    /// </summary>
    /// <typeparam name="TNode">Node type.</typeparam>
    public sealed class PathBuilder<TNode> : IPathBuilder<TNode>, IPathFeeder<TNode>
    {
        internal const string CAN_NOT_GET_PATH_IN_PROGRESS = "Can't get a path while the path builder is in progress. This exception will not be throw in release build, but undefined behaviour.";

        private PathBuilderInner<TNode> inner = new PathBuilderInner<TNode>(false);
        private readonly List<TNode> path = new List<TNode>();

        /// <inheritdoc cref="IPathFeeder{TInfo}.Status"/>
        public PathBuilderState Status {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => inner.Status;
        }

        PathBuilderState IPathFeeder<TNode>.Status => throw new System.NotImplementedException();

        /// <inheritdoc cref="IPathBuilder{TNode}.InitializeBuilderSession()"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode>.InitializeBuilderSession()
            => inner.InitializeBuilderSession();

        /// <inheritdoc cref="IPathBuilder{TNode}.EnqueueToVisit(TNode, float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode>.EnqueueToVisit(TNode node, float priority)
            => inner.EnqueueToVisit(node, priority);

        /// <inheritdoc cref="IPathBuilder{TNode}.FinalizeBuilderSession(CalculationResult)"/>
        void IPathBuilder<TNode>.FinalizeBuilderSession(CalculationResult result)
        {
            inner.FinalizeBuilderSession(result);

            if (Status == PathBuilderState.PathFound)
            {
                if (inner.edges.Count == 0)
                {
                    path.Add(inner.Start);
                    if (!EqualityComparer<TNode>.Default.Equals(inner.Start, inner.End))
                        path.Add(inner.End);
                }
                else
                {
                    TNode to = inner.End;
                    path.Add(to);
                    while (inner.edges.TryGetValue(to, out TNode from))
                    {
                        to = from;
                        path.Add(from);
                    }
                    if (!EqualityComparer<TNode>.Default.Equals(path[path.Count - 1], inner.Start))
                        path.Add(inner.Start);
                    path.Reverse();
                }
            }
        }

        /// <inheritdoc cref="IPathBuilder{TNode}.SetCost(TNode, float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode>.SetCost(TNode to, float cost) => inner.SetCost(to, cost);

        /// <inheritdoc cref="IPathBuilder{TNode}.SetEdge(TNode, TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode>.SetEdge(TNode from, TNode to) => inner.SetEdge(from, to);

        /// <inheritdoc cref="IPathBuilderByNode{TNode}.SetEnd(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetEnd(TNode end) => inner.SetEnd(end);

        /// <inheritdoc cref="IPathBuilderByNode{TNode}.SetStart(TNode)(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStart(TNode start) => inner.SetStart(start);

        /// <inheritdoc cref="IPathBuilder{TNode}.TryDequeueToVisit(out TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IPathBuilder<TNode>.TryDequeueToVisit(out TNode node) => inner.TryDequeueToVisit(out node);

        /// <inheritdoc cref="IPathBuilder{TNode}.TryGetCost(TNode, out float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IPathBuilder<TNode>.TryGetCost(TNode to, out float cost) => inner.TryGetCost(to, out cost);

        /// <inheritdoc cref="IPathBuilder{TNode}.Visit(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode>.Visit(TNode node) => inner.Visit(node);

        /// <inheritdoc cref="IPathBuilder{TNode}.WasVisited(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IPathBuilder<TNode>.WasVisited(TNode node) => inner.WasVisited(node);

        /// <inheritdoc cref="IPathFeeder{TInfo}.GetPathInfo"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerable<TNode> IPathFeeder<TNode>.GetPathInfo()
        {
#if UNITY_EDITOR || DEBUG
            if (Status == PathBuilderState.InProgress)
                throw new InvalidOperationException(CAN_NOT_GET_PATH_IN_PROGRESS);
#endif
            return path;
        }
    }
}