namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Determines the looked up node.
    /// </summary>
    /// <typeparam name="TNode">Type of node.</typeparam>
    public interface IPathTargetFinder<TNode>
    {
        /// <summary>
        /// Check is the node <paramref name="node"/> satisfy this query.
        /// </summary>
        /// <param name="node">Node to check if it does satisfty the query.</param>
        /// <returns>Whenever the node <paramref name="node"/> satisfy the query.</returns>
        bool DoesSatisfy(TNode node);
    }
}