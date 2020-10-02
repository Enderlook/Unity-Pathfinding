namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Determines the looked up node with heuristic capabilities.
    /// </summary>
    /// <typeparam name="TNode">Type of node.</typeparam>
    public interface IPathTargetFinderHeuristic<TNode> : IPathTargetFinder<TNode>
    {
        /// <summary>
        /// Get the heuristic cost required to travel from <paramref name="from"/> to the destination in order to satisfy <see cref="IPathTargetFinder{TNode}.DoesSatisfy(TNode)"/>.
        /// </summary>
        /// <param name="from">Node to calculate heuristic cost.</param>
        /// <returns>Heuristic cost required to travel from <paramref name="from"/> to the destination in order to satisfy <see cref="IPathTargetFinder{TNode}.DoesSatisfy(TNode)"/>.</returns>
        float GetHeuristicCost(TNode from);
    }
}