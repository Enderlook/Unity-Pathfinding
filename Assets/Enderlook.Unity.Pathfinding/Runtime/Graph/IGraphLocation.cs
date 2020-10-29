namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Interface to extract closest node of a graph to a certain position.
    /// </summary>
    /// <typeparam name="TNode">Type of node.</typeparam>
    /// <typeparam name="TCoord">Type of coordinate.</typeparam>
    internal interface IGraphLocation<TNode, TCoord>
    {
        /// <summary>
        /// Calculates the closest node to <paramref name="position"/> or the node which has in range <paramref name="position"/>.
        /// </summary>
        /// <param name="position">Position to query.</param>
        /// <returns>Closest node to <paramref name="position"/> or node which has in range <paramref name="position"/>.<returns>
        TNode FindClosestNodeTo(TCoord position);

        /// <summary>
        /// Calculates the position of a given node.
        /// </summary>
        /// <param name="node">Node to query.</param>
        /// <returns>Position of the node <see cref="node"/>.</returns>
        TCoord ToPosition(TNode node);
    }
}
