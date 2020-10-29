namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Interface to extract heuristic node information from a graph.
    /// </summary>
    /// <typeparam name="TNode">Type of node.</typeparam>
    internal interface IGraphHeuristic<TNode>
    {
        /// <summary>
        /// Get the heuristic cost of travel from <paramref name="from"/> node to <paramref name="to"/> node.
        /// </summary>
        /// <param name="from">Initial node.</param>
        /// <param name="to">Destination node.</param>
        /// <returns>Heuristic cost of traveling from <paramref name="from"/> to <paramref name="to"/>.</returns>
        float GetHeuristicCost(TNode from, TNode to);
    }
}