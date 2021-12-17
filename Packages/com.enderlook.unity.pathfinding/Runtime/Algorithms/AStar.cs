using Enderlook.Unity.Pathfinding.Utils;

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Enderlook.Unity.Pathfinding.Algorithms
{
    /// <summary>
    /// A pathfinder that uses the A* algorithm.
    /// </summary>
    internal static class AStar
    {
        public static async ValueTask CalculatePath<TCoord, TNode, TNodes, TGraph, TBuilder, TSearcher, TWatchdog, TAwaitable, TAwaiter>(
            TGraph graph, TBuilder builder, TCoord from, TSearcher searcher, TWatchdog watchdog)
            where TNodes : IEnumerator<TNode>
            where TGraph : IGraphIntrinsic<TNode, TNodes>, IGraphLocation<TNode, TCoord>
            where TBuilder : IPathBuilder<TNode, TCoord>
            where TSearcher : ISearcherSatisfy<TNode>/*, ISearcherHeuristic<TNode>, ISearcherPosition<TCoord> */
            where TWatchdog : IWatchdog<TAwaitable, TAwaiter>
            where TAwaitable : IAwaitable<TAwaiter>
            where TAwaiter : IAwaiter
        {
            TNode endNode;
            TCoord endPosition;
            CalculationResult result;
            bool @switch = false;

            if (!graph.TryFindNodeTo(from, out TNode from_))
                goto notfound;

            builder.SetStart(from, from_);

            // This check if not required since it's already done inside the loop below.
            // However, by adding this, we avoid a possible switch to the Unity thread.
            if (searcher.DoesSatisfy(from_))
            {
                endNode = from_;
                endPosition = from;
                goto found;
            }

            builder.SetCost(from_, 0);
            builder.EnqueueToVisit(from_, 0);

            bool searcherImplementsHeuristic = typeof(ISearcherHeuristic<TNode>).IsAssignableFrom(typeof(TSearcher));
            ISearcherHeuristic<TNode> searcherHeuristicReferenceType = typeof(TSearcher).IsValueType ? null : (ISearcherHeuristic<TNode>)searcher;

            while (builder.TryDequeueToVisit(out TNode node))
            {
                if (!builder.VisitIfWasNotVisited(node))
                    continue;

                if (searcher.DoesSatisfy(node))
                {
                    if (typeof(ISearcherPosition<TCoord>).IsAssignableFrom(typeof(TSearcher)))
                        endPosition = ((ISearcherPosition<TCoord>)searcher).EndPosition;
                    else
                        endPosition = graph.ToPosition(node);
                    endNode = node;
                    goto found;
                }

                if (!builder.TryGetCost(node, out float costFromSource))
                    costFromSource = float.PositiveInfinity;

                TNodes neighbours = graph.GetNeighbours(node);
                try
                {
                    TNode neighbour;
                    while (neighbours.MoveNext())
                    {
                        neighbour = neighbours.Current;

                        float cost = graph.GetCost(node, neighbour) + costFromSource;

                        if (!builder.TryGetCost(neighbour, out float oldCost) || cost < oldCost)
                        {
                            builder.SetCost(neighbour, cost);
                            builder.SetEdge(node, neighbour);
                        }

                        float costWithHeuristic = cost;
                        if (searcherImplementsHeuristic)
                            costWithHeuristic += (typeof(TSearcher).IsValueType ? ((ISearcherHeuristic<TNode>)searcher) : searcherHeuristicReferenceType).GetHeuristicCost(neighbour);

                        builder.EnqueueToVisit(neighbour, costWithHeuristic);

                        if (!watchdog.CanContinue(out TAwaitable awaitable_))
                            goto timedout;
                        await awaitable_;
                    }
                }
                finally
                {
                    neighbours.Dispose();
                }

                if (!watchdog.CanContinue(out TAwaitable awaitable))
                    goto timedout;
                await awaitable;
            }
            goto notfound;

            notfound:
            result = CalculationResult.PathNotFound;
            goto finalize;

            timedout:
            result = CalculationResult.Timedout;
            goto finalize;

            found:
            result = CalculationResult.PathFound;
            builder.SetEnd(endPosition, endNode);
            goto finalize;

            finalize:
            await builder.FinalizeBuilderSession<TWatchdog, TAwaitable, TAwaiter>(result, watchdog);
        }
    }
}