using Enderlook.Unity.Pathfinding.Utils;
using Enderlook.Unity.Threading;

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
            where TGraph : IGraphIntrinsic<TNode, TNodes>, IGraphLocation<TNode, TCoord>/*, IGraphLineOfSight<TCoord>*/
            where TBuilder : IPathBuilder<TNode, TCoord>
            where TSearcher : ISearcherSatisfy<TNode>/*, ISearcherHeuristic<TNode>, ISearcherPosition<TCoord> */
            where TWatchdog : IWatchdog<TAwaitable, TAwaiter>
            where TAwaitable : IAwaitable<TAwaiter>
            where TAwaiter : IAwaiter
        {
            TNode endNode = default;
            TCoord endPosition = default;
            CalculationResult result = CalculationResult.Timedout;
            bool @switch = false;

            if (!graph.TryFindNodeTo(from, out TNode from_))
            {
                result = CalculationResult.PathNotFound;
                goto end;
            }
            builder.SetStart(from, from_);

            if (searcher.DoesSatisfy(from_))
            {
                endNode = from_;
                endPosition = from;
                goto found;
            }

            builder.SetCost(from_, 0);
            builder.EnqueueToVisit(from_, 0);

            bool graphImplementsLineOfSight = typeof(IGraphLineOfSight<TCoord>).IsAssignableFrom(typeof(TGraph));
            IGraphLineOfSight<TCoord> lineOfSight = typeof(TGraph).IsValueType ? null : (IGraphLineOfSight<TCoord>)graph;
            bool searcherImplementsHeuristic = typeof(ISearcherHeuristic<TNode>).IsAssignableFrom(typeof(TSearcher));
            ISearcherHeuristic<TNode> searcherHeuristicReferenceType = typeof(TSearcher).IsValueType ? null : (ISearcherHeuristic<TNode>)searcher;

            @switch = graphImplementsLineOfSight && ((IGraphLineOfSight<TCoord>)graph).RequiresUnityThread;
            if (@switch)
                await Switch.ToUnity;

            while (builder.TryDequeueToVisit(out TNode node))
            {
                if (builder.WasVisited(node))
                    continue;
                builder.Visit(node);

                if (searcher.DoesSatisfy(node))
                {
                    if (typeof(ISearcherPosition<TCoord>).IsAssignableFrom(typeof(TSearcher)))
                        endPosition = ((ISearcherPosition<TCoord>)searcher).EndPosition;
                    else
                        endPosition = graph.ToPosition(node);
                    endNode = node;
                    goto found;
                }

                float costFromSource = default;
                if (graphImplementsLineOfSight && !builder.TryGetCost(node, out costFromSource))
                    costFromSource = float.PositiveInfinity;

                TNodes neighbours = graph.GetNeighbours(node);
                try
                {
                    TNode neighbour;
                    while (neighbours.MoveNext())
                    {
                        neighbour = neighbours.Current;

                        TNode currentParent;
                        if (graphImplementsLineOfSight)
                        {
                            // builder.TryGetEdge(neighbour, out TNode parent)
                            if (builder.TryGetEdge(neighbour, out TNode parent) && (typeof(TGraph).IsValueType ? (IGraphLineOfSight<TCoord>)graph : lineOfSight).HasLineOfSight(graph.ToPosition(parent), graph.ToPosition(neighbour)))
                                currentParent = parent;
                            else
                                currentParent = node;

                            if (!builder.TryGetCost(currentParent, out costFromSource))
                                costFromSource = float.PositiveInfinity;
                        }
                        else
                            currentParent = node;

                        float cost = graph.GetCost(currentParent, neighbour) + costFromSource;

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
            if (@switch)
                await Switch.ToBackground;
            builder.SetEnd(endPosition, endNode);
            await builder.FinalizeBuilderSession<TWatchdog, TAwaitable, TAwaiter>(result, watchdog);
        }
    }
}