using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Implementation of <see cref="ISearcherSatisfy{TNode}"/> to find an specific location.
    /// </summary>
    /// <typeparam name="TNode">Type of node.</typeparam>
    internal readonly struct SearcherToLocation<TCoord, TNode> : ISearcherSatisfy<TNode>, ISearcherPosition<TCoord>
    {
        private readonly TNode target;
        private readonly IEqualityComparer<TNode> comparer;

        /// <inheritdoc cref="ISearcherPosition{TCoord}.EndPosition"/>
        public TCoord EndPosition { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; }

        private SearcherToLocation(TNode target, TCoord destination)
        {
            this.target = target;
            EndPosition = destination;
            if (!typeof(TNode).IsValueType)
                comparer = EqualityComparer<TNode>.Default;
            else
                comparer = default;
        }

        public static bool TryFrom<T>(T graph, TCoord destination, out SearcherToLocation<TCoord, TNode> searcher)
            where T : class, IGraphLocation<TNode, TCoord>
        {
            if (graph.TryFindNodeTo(destination, out TNode node))
            {
                searcher = new SearcherToLocation<TCoord, TNode>(node, destination);
                return true;
            }

            searcher = default;
            return false;
        }

        /// <inheritdoc cref="ISearcherSatisfy{TNode}.DoesSatisfy(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DoesSatisfy(TNode node)
        {
            if (typeof(TNode).IsValueType) // Use intrinsic for value types.
                return EqualityComparer<TNode>.Default.Equals(node, target);
            else
                return comparer.Equals(node, target);
        }
    }
}