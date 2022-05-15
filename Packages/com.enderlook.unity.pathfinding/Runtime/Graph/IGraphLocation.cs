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
        /// Calculates the closest valid node to <paramref name="position"/>.
        /// </summary>
        /// <param name="position">Position to query.</param>
        /// <param name="node">Closest valid node to <paramref name="position"/> if return <see langword="true"/>.</param>
        /// <returns>Whenever a node was found for the specified <paramref name="position"/>.<returns>
        bool TryFindNodeTo(TCoord position, out TNode node);

        /// <summary>
        /// Calculates the position of a given node.
        /// </summary>
        /// <param name="node">Node to query.</param>
        /// <returns>Position of the node <see cref="node"/>.</returns>
        TCoord ToPosition(TNode node);
    }
}
