namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Constructor of a path between two nodes.
    /// </summary>
    /// <typeparam name="TNode">Type of node.</typeparam>
    public interface IPathBuilder<TNode>
    {
        /// <summary>
        /// Clear the path.<br/>
        /// Used to reuse this instance.
        /// </summary>
        void Clear();

        /// <summary>
        /// Stores the starting and target node of the path.
        /// </summary>
        /// <param name="from">Starting node.</param>
        /// <param name="to">Destination node.</param>
        void SetFromTo(TNode from, TNode to);

        /// <summary>
        /// Stores the cost require to travel from the starting node to <paramref name="to"/>.
        /// </summary>
        /// <param name="to">Node to set cost.</param>
        /// <param name="cost">Cost required to travel from the starting node to <paramref name="to"/>.</param>
        void SetCost(TNode to, float cost);

        /// <summary>
        /// Get the cost required to travel from the starting node to <paramref name="to"/>.<br/>
        /// This value was previously setted by <seealso cref="SetCost(TNode, float)"/>.
        /// </summary>
        /// <param name="to">Destination node.</param>
        /// <param name="cost">Cost required to travel from the starting node to <paramref name="to"/>, if any.</param>
        /// <returns>Whenever a cost was found or not.</returns>
        bool TryGetCost(TNode to, out float cost);

        /// <summary>
        /// Set an edge between two nodes.
        /// </summary>
        /// <param name="from">Starting edge's node.</param>
        /// <param name="to">End edge's node.</param>
        void SetEdge(TNode from, TNode to);

        /// <summary>
        /// Set the state of the current path generator.
        /// </summary>
        /// <param name="state">Current state of the path.</param>
        void SetState(PathState state);
    }
}