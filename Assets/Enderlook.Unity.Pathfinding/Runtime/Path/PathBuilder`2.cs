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
    public sealed class PathBuilder<TNode, TCoord> : IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>
    {
        private PathBuilderInner<TNode> inner = new PathBuilderInner<TNode>(false);
        private readonly List<TCoord> path = new List<TCoord>();
        private IGraphLocation<TNode, TCoord> converter;
        private TCoord start;
        private TCoord end;

        /// <inheritdoc cref="IPathFeeder{TInfo}.Status"/>
        public PathBuilderState Status {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => inner.Status;
        }

        /// <inheritdoc cref="IPathBuilder{TNode}.InitializeBuilderSession(TNode)"/>
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
                    path.Add(start);

                    TCoord start2 = converter.ToPosition(inner.Start);
                    if (!EqualityComparer<TCoord>.Default.Equals(start, start2))
                        path.Add(start2);

                    TCoord end2 = converter.ToPosition(inner.End);
                    if (!EqualityComparer<TCoord>.Default.Equals(end, end2))
                        path.Add(end2);

                    path.Add(end);
                }
                else
                {
                    path.Add(end);

                    TNode to = inner.End;
                    TCoord end2 = converter.ToPosition(to);
                    if (!EqualityComparer<TCoord>.Default.Equals(end, end2))
                        path.Add(end2);

                    while (inner.edges.TryGetValue(to, out TNode from))
                    {
                        to = from;
                        path.Add(converter.ToPosition(from));
                    }

                    TCoord start2 = converter.ToPosition(inner.Start);
                    if (!EqualityComparer<TCoord>.Default.Equals(start2, path[path.Count - 1]))
                        path.Add(start2);

                    if (!EqualityComparer<TCoord>.Default.Equals(start2, start))
                        path.Add(start);

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

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetEnd(TCoord)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode, TCoord>.SetEnd(TCoord end) => this.end = end;

        /// <inheritdoc cref="IPathBuilder{TNode}.SetEnd(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode>.SetEnd(TNode end) => inner.SetEnd(end);

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetStart(TCoord))"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode, TCoord>.SetStart(TCoord start) => this.start = start;

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetNodeToPositionConverter(IGraphLocation{TNode, TCoord})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode, TCoord>.SetNodeToPositionConverter(IGraphLocation<TNode, TCoord> converter) => this.converter = converter;

        /// <inheritdoc cref="IPathBuilder{TNode}.SetStart(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode>.SetStart(TNode start) => inner.SetStart(start);

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
        IEnumerable<TCoord> IPathFeeder<TCoord>.GetPathInfo()
        {
#if UNITY_EDITOR || DEBUG
            if (Status == PathBuilderState.InProgress)
                throw new InvalidOperationException(PathBuilder<TNode>.CAN_NOT_GET_PATH_IN_PROGRESS);
#endif
            return path;
        }
    }
}