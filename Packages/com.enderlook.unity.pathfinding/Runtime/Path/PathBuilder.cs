using Enderlook.Collections;
using Enderlook.Collections.LowLevel;
using Enderlook.Unity.Threading;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Represents the internal builder of a path.
    /// </summary>
    /// <typeparam name="TNode">Node type.</typeparam>
    /// <typeparam name="TCoord">Coordinate type.</typeparam>
    internal sealed class PathBuilder<TNode, TCoord> : IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>, IDisposable
    {
        private const string CAN_NOT_INITIALIZE_ALREADY_INITIALIZED = "Can't initialize a builder session because it's already initialized.";
        private const string CAN_NOT_EXECUTE_IF_IS_NOT_FINALIZED = "Can't execute if the session hasn't finalized";
        private const string CAN_NOT_EXECUTE_IT_IS_NOT_INITIALIZED_DEBUG_ONLY = "It's not initialized. This doesn't happens in export release.";
        private const string CAN_NOT_FINALIZE_IF_WAS_NOT_INITIALIZED = "Can't finalize session because it was never initialized.";

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
                ProducePath();
                OptimizePath().GetAwaiter().GetResult();
                status = Status.Found;
            }
            else if (result == CalculationResult.Timedout)
                status = Status.Timedout;
            else
                status = Status.Finalized;
        }

        private void ProducePath()
        {
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
                }

                TCoord start2 = converter.ToPosition(startNode);
                if (!EqualityComparer<TCoord>.Default.Equals(start2, pathRaw[pathRaw.Count - 1]))
                    pathRaw.Add(start2);

                if (!EqualityComparer<TCoord>.Default.Equals(start2, startPosition))
                    pathRaw.Add(startPosition);

                pathRaw.Reverse();
            }
        }

        private async ValueTask OptimizePath()
        {
            System.Diagnostics.Debug.Assert(pathRaw.Count >= 2);

            pathOptimized.Clear();

            if (pathRaw.Count == 2)
            {
                pathOptimized.Add(pathRaw[0]);
                pathOptimized.Add(pathRaw[1]);
                return;
            }

            pathOptimized.Add(pathRaw[0]);

            bool isTask = !Switch.IsInMainThread;
            if (isTask)
                await Switch.ToUnity;

            TCoord previous;
            TCoord current = pathRaw[0];
            TCoord lastOptimized = current;
            for (int i = 1; i < pathRaw.Count; i++)
            {
                previous = current;
                current = pathRaw[i];
                if (lineOfsight.HasLineOfSight(lastOptimized, current))
                    continue;
                pathOptimized.Add(previous);
                lastOptimized = previous;
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

            if (isTask)
                await Switch.ToBackground;
        }

        /// <inheritdoc cref="IPathFeeder{TInfo}.GetPathInfo"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ReadOnlySpan<TCoord> IPathFeeder<TCoord>.GetPathInfo()
        {
            if ((status & Status.Finalized) == 0)
                throw new InvalidOperationException(CAN_NOT_EXECUTE_IF_IS_NOT_FINALIZED);

            return pathOptimized.AsSpan();
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

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetGraphLocation(IGraphLocation{TNode, TCoord})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetGraphLocation(IGraphLocation<TNode, TCoord> converter)
        {
#if UNITY_EDITOR || DEBUG
            if ((status & Status.Initialized) == 0)
                throw new InvalidOperationException(CAN_NOT_EXECUTE_IT_IS_NOT_INITIALIZED_DEBUG_ONLY);
#endif

            this.converter = converter;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetLineOfSight(IGraphLineOfSight{TCoord})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetLineOfSight(IGraphLineOfSight<TCoord> lineOfSight)
        {
#if UNITY_EDITOR || DEBUG
            if ((status & Status.Initialized) == 0)
                throw new InvalidOperationException(CAN_NOT_EXECUTE_IT_IS_NOT_INITIALIZED_DEBUG_ONLY);
#endif

            lineOfsight = lineOfSight;
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
    }
}