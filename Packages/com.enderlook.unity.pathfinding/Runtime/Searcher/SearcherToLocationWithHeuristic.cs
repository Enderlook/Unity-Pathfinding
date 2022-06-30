using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Implementation of <see cref="ISearcherSatisfy{TNode}"/> and <see cref="ISearcherHeuristic{TNode}"/> to find an specific location.
    /// </summary>
    /// <typeparam name="TNode">Type of node.</typeparam>
    internal readonly struct SearcherToLocationWithHeuristic<TGraph, TCoord, TNode> : ISearcherSatisfy<TNode>, ISearcherPosition<TCoord>, ISearcherHeuristic<TNode>
        where TGraph : IGraphHeuristic<TNode>
    {
        private readonly TGraph graph;
        private readonly TNode target;
        private readonly IEqualityComparer<TNode> comparer;

        /// <inheritdoc cref="ISearcherPosition{TCoord}.EndPosition"/>
        public TCoord EndPosition { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; }

        private SearcherToLocationWithHeuristic(TGraph graph, TNode target, TCoord destination)
        {
            this.graph = graph;
            this.target = target;
            EndPosition = destination;
            if (!typeof(TNode).IsValueType)
                comparer = EqualityComparer<TNode>.Default;
            else
                comparer = default;
        }

        public static bool TryFrom<T>(T graph, TCoord destination, out SearcherToLocationWithHeuristic<TGraph, TCoord, TNode> searcher)
            where T : TGraph, IGraphLocation<TNode, TCoord>
        {
            if (graph.TryFindNodeTo(destination, out TNode node))
            {
                searcher = new SearcherToLocationWithHeuristic<TGraph, TCoord, TNode>(graph, node, destination);
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

        /// <inheritdoc cref="ISearcherHeuristic{TNode}.GetHeuristicCost(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetHeuristicCost(TNode from) => graph.GetHeuristicCost(from, target);
    }
}