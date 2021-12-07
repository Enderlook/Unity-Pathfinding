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
            where TGraph : IGraphIntrinsic<TNode, TNodes>, IGraphLocation<TNode, TCoord>//, IGraphLineOfSight<TCoord>
            where TBuilder : IPathBuilder<TNode, TCoord>
            where TSearcher : struct, ISearcherSatisfy<TNode>
            where TWatchdog : IWatchdog<TAwaitable, TAwaiter>
            where TAwaitable : IAwaitable<TAwaiter>
            where TAwaiter : IAwaiter
        {
            TNode from_ = graph.FindClosestNodeTo(from);
            builder.SetStart(from, from_);

            TNode endNode = default;
            TCoord endPosition = default;
            CalculationResult result = CalculationResult.Timedout;

            if (searcher.DoesSatisfy(from_))
            {
                endNode = from_;
                endPosition = from;
                goto found;
            }

            builder.SetCost(from_, 0);
            builder.EnqueueToVisit(from_, 0);

            while (builder.TryDequeueToVisit(out TNode node))
            {
                if (builder.WasVisited(node))
                    continue;
                builder.Visit(node);

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
                        if (typeof(ISearcherHeuristic<TNode>).IsAssignableFrom(typeof(TSearcher)))
                            costWithHeuristic += ((ISearcherHeuristic<TNode>)searcher).GetHeuristicCost(neighbour);

                        builder.EnqueueToVisit(neighbour, costWithHeuristic);

                        if (searcher.DoesSatisfy(neighbour))
                        {
                            if (typeof(ISearcherPosition<TCoord>).IsAssignableFrom(typeof(TSearcher)))
                                endPosition = ((ISearcherPosition<TCoord>)searcher).EndPosition;
                            else
                                endPosition = graph.ToPosition(neighbour);
                            endNode = neighbour;
                            goto found;
                        }

                        if (!watchdog.CanContinue(out TAwaitable awaitable_))
                            goto end;
                        await awaitable_;
                    }
                }
                finally
                {
                    neighbours.Dispose();
                }

                if (!watchdog.CanContinue(out TAwaitable awaitable))
                    goto end;
                await awaitable;
            }
            goto end;

            found:
            result = CalculationResult.PathFound;

            end:
            builder.SetEnd(endPosition, endNode);
            builder.FinalizeBuilderSession(result);
        }
    }
}