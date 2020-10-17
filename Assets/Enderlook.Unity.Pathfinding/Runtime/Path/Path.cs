using System.Collections.Generic;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Represents a path.
    /// </summary>
    /// <typeparam name="TNode">Node type.</typeparam>
    public sealed class Path<TNode> : IPathFeedable<IEnumerable<TNode>>, IPathFeedable<IReadOnlyList<TNode>>, IPathFeedable<IEnumerator<TNode>>
    {
        private readonly List<TNode> nodes = new List<TNode>();

        /// <inheritdoc cref="IPathFeedable{TInfo}"/>
        void IPathFeedable<IEnumerable<TNode>>.Feed(IEnumerable<TNode> information)
        {
            nodes.Clear();
            // We don't check capacity for optimization because that is already done in AddRange.
            nodes.AddRange(information);
        }

        /// <inheritdoc cref="IPathFeedable{TInfo}"/>
        void IPathFeedable<IReadOnlyList<TNode>>.Feed(IReadOnlyList<TNode> information)
        {
            nodes.Clear();
            // We don't check capacity for optimization because that is already done in AddRange.
            nodes.AddRange(information);
        }

        /// <inheritdoc cref="IPathFeedable{TInfo}"/>
        public void Feed(IEnumerator<TNode> information)
        {
            nodes.Clear();
            while (information.MoveNext())
                nodes.Add(information.Current);
        }
    }
}