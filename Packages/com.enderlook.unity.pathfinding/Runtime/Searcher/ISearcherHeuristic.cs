namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Determines heuristic cost to the looked up node.
    /// </summary>
    /// <typeparam name="TNode">Type of node.</typeparam>
    public interface ISearcherHeuristic<TNode>
    {
        /// <summary>
        /// Get the heuristic cost required to travel from <paramref name="from"/> to the destination in order to satisfy <see cref="ISearcherSatisfy{TNode}.DoesSatisfy(TNode)"/>.
        /// </summary>
        /// <param name="from">Node to calculate heuristic cost.</param>
        /// <returns>Heuristic cost required to travel from <paramref name="from"/> to the destination in order to satisfy <see cref="ISearcherSatisfy{TNode}.DoesSatisfy(TNode)"/>.</returns>
        float GetHeuristicCost(TNode from);
    }
}