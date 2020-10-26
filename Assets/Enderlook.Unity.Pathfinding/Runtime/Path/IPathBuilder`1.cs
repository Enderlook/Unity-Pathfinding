﻿namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Constructor of a path between two nodes.
    /// </summary>
    /// <typeparam name="TNode">Type of node.</typeparam>
    public interface IPathBuilder<TNode>
    {
        /// <summary>
        /// Clear the current path and initializes a new builder session.<br/>
        /// If a builder session was already enabled, undefined behaviour.
        /// </summary>
        void InitializeBuilderSession();

        /// <summary>
        /// Finalize the building session.<br/>
        /// If no builder session was already enabled, undefined behaviour.<br/>
        /// If the initial node and the target node are the same, no other method is necessary to be executed.
        /// </summary>
        /// <param name="result">Result status.</param>
        void FinalizeBuilderSession(CalculationResult result);

        /// <summary>
        /// Enques a node to visit it later.
        /// </summary>
        /// <param name="node">Node to enqueue.</param>
        /// <param name="priority">Priority of the node.</param>
        void EnqueueToVisit(TNode node, float priority);

        /// <summary>
        /// Try to dequeue a node (if any) to visit.<br/>
        /// It doesn't need to check if the node was already visited. For that <see cref="WasVisited(TNode)"/> is used.
        /// </summary>
        /// <param name="node">Dequeued node, if any.</param>
        /// <returns>Whenever a node was deuqued of not.</returns>
        bool TryDequeueToVisit(out TNode node);

        /// <summary>
        /// Get the cost required to travel from the starting node to <paramref name="to"/>.<br/>
        /// This value was previously setted by <seealso cref="SetCost(TNode, float)"/>.
        /// </summary>
        /// <param name="to">Destination node.</param>
        /// <param name="cost">Cost required to travel from the starting node to <paramref name="to"/>, if any.</param>
        /// <returns>Whenever a cost was found or not.</returns>
        bool TryGetCost(TNode to, out float cost);

        /// <summary>
        /// Stores the cost required to travel from the starting node to <paramref name="to"/>.
        /// </summary>
        /// <param name="to">Node to set cost.</param>
        /// <param name="cost">Cost required to travel from the starting node to <paramref name="to"/>.</param>
        void SetCost(TNode to, float cost);

        /// <summary>
        /// Set an edge between two nodes.
        /// </summary>
        /// <param name="from">Starting edge's node.</param>
        /// <param name="to">End edge's node.</param>
        void SetEdge(TNode from, TNode to);

        /// <summary>
        /// Set the end node of this path.
        /// </summary>
        /// <param name="end">End node of the path.</param>
        /// <remarks>The argument of this method may be the same as the argument of <see cref="SetStart(TCoord)"/> or be <see cref="default"/>.</remarks>
        void SetEnd(TNode end);

        /// <summary>
        /// Set the start node of this path.
        /// </summary>
        /// <param name="start">Start node of the path.</param>
        void SetStart(TNode start);

        /// <summary>
        /// Mark the node as visited.
        /// </summary>
        /// <param name="node">Node to mask as visited.</param>
        void Visit(TNode node);

        /// <summary>
        /// Check if the node was already visited.
        /// </summary>
        /// <param name="node">Node to check.</param>
        /// <returns>Whenever <paramref name="node"/> was already visited or not.</returns>
        bool WasVisited(TNode node);
    }
}