using Enderlook.Unity.Pathfinding.Utils;
using Enderlook.Unity.Threading;

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Enderlook.Unity.Pathfinding.Algorithms
{
    /// <summary>
    /// A pathfinder that uses the A* algorithm.
    /// </summary>
    internal static class Algorithm
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
            // https://github.com/yellowisher/ThetaStar/blob/master/Assets/1_Scripts/PathfindingManager.cs
            // http://idm-lab.org/bib/abstracts/papers/aaai10b.pdf

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

            // Check if we use A* or Dijkstra
            bool searcherImplementsHeuristic = typeof(ISearcherHeuristic<TNode>).IsAssignableFrom(typeof(TSearcher));
            ISearcherHeuristic<TNode> searcherHeuristicReferenceType = typeof(TSearcher).IsValueType ? null : (ISearcherHeuristic<TNode>)searcher;

            // Check if we use Theta* (if A* is true) or Dijkstra with any-angle.
            bool graphImplementsLineOfSight = typeof(IGraphLineOfSight<TCoord>).IsAssignableFrom(typeof(TGraph));
            IGraphLineOfSight<TCoord> lineOfSight = typeof(TGraph).IsValueType ? null : (IGraphLineOfSight<TCoord>)graph;

            @switch = graphImplementsLineOfSight && ((IGraphLineOfSight<TCoord>)graph).RequiresUnityThread;
            if (@switch)
                await Switch.ToUnity;

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

                // Calculate cost from source eagerly since nodes usually have more than 1 neighbour, so we cache it.
                if (builder.TryGetCost(node, out float costFromSourceNode))
                    costFromSourceNode = float.PositiveInfinity;

                float costFromSourceParent;
                bool hasNodeParent;
                TNode nodeParent;
                if (graphImplementsLineOfSight)
                {
                    hasNodeParent = builder.TryGetEdge(node, out nodeParent);
                    if (hasNodeParent && !builder.TryGetCost(nodeParent, out costFromSourceParent))
                        goto getNeighbours;
                }
                else
                {
                    hasNodeParent = false;
                    nodeParent = default;
                    goto hasNotParentCost;
                }
                hasNotParentCost:
                costFromSourceParent = float.PositiveInfinity;

                getNeighbours:
                TNodes neighbours = graph.GetNeighbours(node);
                try
                {
                    TNode neighbour;
                    while (neighbours.MoveNext())
                    {
                        neighbour = neighbours.Current;
                        TNode fromNode;
                        float costFromSource;
                        if (graphImplementsLineOfSight && hasNodeParent
                            && (typeof(TGraph).IsValueType ? (IGraphLineOfSight<TCoord>)graph : lineOfSight)
                                .HasLineOfSight(graph.ToPosition(nodeParent), graph.ToPosition(neighbour)))
                        {
                            costFromSource = costFromSourceParent;
                            fromNode = nodeParent;
                            goto process;
                        }
                        fromNode = node;
                        costFromSource = costFromSourceNode;

                        process:
                        float newCost = graph.GetCost(fromNode, neighbour) + costFromSource;

                        if (!builder.TryGetCost(neighbour, out float oldCost) || newCost < oldCost)
                        {
                            builder.SetCost(neighbour, newCost);
                            builder.SetEdge(fromNode, neighbour);
                        }

                        float costWithHeuristic = newCost;
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
            if (@switch)
                await Switch.ToBackground;
            await builder.FinalizeBuilderSession<TWatchdog, TAwaitable, TAwaiter>(result, watchdog);
        }
    }
}