using Enderlook.Pools;
using Enderlook.Unity.Pathfinding.Utils;

using System.Collections.Generic;
using System.Threading.Tasks;

using Enderlook.Threading;
using System;
using Enderlook.Unity.Threading;
using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Helper class to calculate paths.
    /// </summary>
    internal static class PathCalculator
    {
        public static ValueTask CalculatePath<TCoord, TNode, TNodes, TGraph, TBuilder, TPath, TSearcher, TWatchdog, TWatchdogAwaitable, TWatchdogAwaiter, TThreadingAwaitable, TThreadingAwaiter>(
            TGraph graph, TCoord from, TPath path, TSearcher searcher, TWatchdog watchdog)
            where TNodes : IEnumerator<TNode>
            where TGraph : IGraphIntrinsic<TNode, TNodes>, IGraphLocation<TNode, TCoord>/*, IGraphLineOfSight<TNode>, IGraphLineOfSight<TCoord>*/
            where TBuilder : class, IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>, new()
            where TPath : IPathFeedable<TCoord>, IEnumerable<TCoord>
            where TSearcher : ISearcherSatisfy<TNode>/*, ISearcherHeuristic<TNode>, ISearcherPosition<TCoord> */
            where TWatchdog : IWatchdog<TWatchdogAwaitable, TWatchdogAwaiter>, IThreadingPreference<TThreadingAwaitable, TThreadingAwaiter>
            where TWatchdogAwaitable : IAwaitable<TWatchdogAwaiter>
            where TWatchdogAwaiter : IAwaiter
            where TThreadingAwaitable : IAwaitable<TThreadingAwaiter>
            where TThreadingAwaiter : IAwaiter
        {
            // https://github.com/yellowisher/ThetaStar/blob/master/Assets/1_Scripts/PathfindingManager.cs
            // http://idm-lab.org/bib/abstracts/papers/aaai10b.pdf
            // https://github.com/syonkers/Theta-star-Pathfinding/blob/master/Assets/Controller.cs

            if (typeof(ISearcherHeuristic<TNode>).IsAssignableFrom(typeof(TSearcher)))
            {
                if (typeof(IGraphLineOfSight<TNode>).IsAssignableFrom(typeof(TGraph)))
                {
                    if (typeof(IGraphLineOfSight<TCoord>).IsAssignableFrom(typeof(TGraph)))
                        return Local<Toggle.Yes, Toggle.Yes, Toggle.Yes>();
                    else
                        return Local<Toggle.Yes, Toggle.Yes, Toggle.No>();
                }
                else
                {
                    if (typeof(IGraphLineOfSight<TCoord>).IsAssignableFrom(typeof(TGraph)))
                        return Local<Toggle.Yes, Toggle.No, Toggle.Yes>();
                    else
                        return Local<Toggle.Yes, Toggle.No, Toggle.No>();
                }
            }
            else if (typeof(IGraphLineOfSight<TNode>).IsAssignableFrom(typeof(TGraph)))
            {
                if (typeof(IGraphLineOfSight<TCoord>).IsAssignableFrom(typeof(TGraph)))
                    return Local<Toggle.No, Toggle.Yes, Toggle.Yes>();
                else
                    return Local<Toggle.No, Toggle.Yes, Toggle.No>();
            }
            else if (typeof(IGraphLineOfSight<TCoord>).IsAssignableFrom(typeof(TGraph)))
                return Local<Toggle.No, Toggle.No, Toggle.Yes>();
            else
                return Local<Toggle.No, Toggle.No, Toggle.No>();

            async ValueTask Local<THasHeuristic, THasLineOfSightNode, THasLineOfSightCoord>()
            {
                /* Types of algorithms:
                 * | Algorithm            | THasHeuristic | THasLineOfSightNode | THasLineOfSightCoord |
                 * | Dijkstra             | No            | No                  | No                   |
                 * | A*                   | Yes           | No                  | No                   |
                 * | Dijkstra (any angle) | No            | Yes                 | No                   |
                 * | Dijkstra (any angle) | No            | No                  | Yes                  |
                 * | Dijkstra (any angle) | No            | Yes                 | Yes                  |
                 * | Theta*               | Yes           | Yes                 | No                   |
                 * | Theta*               | Yes           | No                  | Yes                  |
                 * | Theta*               | Yes           | Yes                 | Yes                  |
                 */

                // TODO: Pathfinding which uses line of sight with coordinates could be improved to take into account
                // the first and last positions which aren't taken into account for any-angle since they are not
                // bound to any node.

                TBuilder builder = ObjectPool<TBuilder>.Shared.Rent();
                builder.InitializeBuilderSession();

                TNode endNode;
                TCoord endPosition;
                CalculationResult result;
                bool @switch = false;

                if (!graph.TryFindNodeTo(from, out TNode from_))
                    goto notfound;

                builder.SetStart(from, from_);

                // This check is not required since it's already done inside the loop below.
                // However, by adding this, we avoid a possible switch to the Unity thread.
                if (searcher.DoesSatisfy(from_))
                {
                    endNode = from_;
                    endPosition = from;
                    goto found;
                }

                if (Toggle.IsToggled<THasHeuristic>() != typeof(ISearcherHeuristic<TNode>).IsAssignableFrom(typeof(TSearcher))
                    || Toggle.IsToggled<THasLineOfSightNode>() != typeof(IGraphLineOfSight<TNode>).IsAssignableFrom(typeof(TGraph))
                    || Toggle.IsToggled<THasLineOfSightCoord>() != typeof(IGraphLineOfSight<TCoord>).IsAssignableFrom(typeof(TGraph)))
                {
                    Debug.Assert(false);
                    goto notfound;
                }

                ISearcherHeuristic<TNode> searcherHeuristicReferenceType = !Toggle.IsToggled<THasHeuristic>() || typeof(TSearcher).IsValueType ? null : (ISearcherHeuristic<TNode>)searcher;
                IGraphLineOfSight<TNode> lineOfSightNodeReferenceType = !Toggle.IsToggled<THasLineOfSightNode>() || typeof(TGraph).IsValueType ? null : (IGraphLineOfSight<TNode>)graph;
                IGraphLineOfSight<TCoord> lineOfSightCoordReferenceType = !Toggle.IsToggled<THasLineOfSightCoord>() || typeof(TGraph).IsValueType ? null : (IGraphLineOfSight<TCoord>)graph;

                builder.SetCost(from_, 0);
                builder.EnqueueToVisit(from_, 0);

                @switch = Toggle.IsToggled<THasLineOfSightNode>() && (typeof(TGraph).IsValueType ? (IGraphLineOfSight<TNode>)graph : lineOfSightNodeReferenceType).RequiresUnityThread;
                if (@switch && !UnityThread.IsMainThread)
                    await watchdog.ToUnity();

                while (builder.TryDequeueToVisit(out TNode node))
                {
                    if (!builder.VisitIfWasNotVisited(node)) // TODO: Maybe this could be put inside the neighbours loop.
                        goto endOfLoop;

                    if (searcher.DoesSatisfy(node))
                    {
                        if (typeof(ISearcherPosition<TCoord>).IsAssignableFrom(typeof(TSearcher)))
                            endPosition = ((ISearcherPosition<TCoord>)searcher).EndPosition;
                        else
                            endPosition = graph.ToPosition(node);
                        endNode = node;
                        goto found;
                    }

                    // Calculate cost eagerly since nodes usually have more than one neighbour, so we cache the lookup.
                    if (!builder.TryGetCost(node, out float costFromNode))
                        costFromNode = float.PositiveInfinity;
                    float costFromParent;
                    bool hasNodeParent;
                    TNode nodeParent;
                    TCoord coordParent;
                    if (Toggle.IsToggled<THasLineOfSightNode>() || Toggle.IsToggled<THasLineOfSightCoord>())
                    {
                        hasNodeParent = builder.TryGetEdge(node, out nodeParent);
                        if (hasNodeParent)
                        {
                            if (Toggle.IsToggled<THasLineOfSightCoord>())
                                coordParent = graph.ToPosition(nodeParent);
                            else
                                coordParent = default;
                            if (builder.TryGetCost(nodeParent, out costFromParent))
                                goto getNeighbours;
                        }
                        else
                            coordParent = default;
                    }
                    else
                    {
                        hasNodeParent = false;
                        nodeParent = default;
                        coordParent = default;
                    }
                    costFromParent = float.PositiveInfinity;

                getNeighbours:
                    using (TNodes neighbours = graph.GetNeighbours(node))
                    {
                        TNode neighbour;
                        while (neighbours.MoveNext())
                        {
                            neighbour = neighbours.Current;

                            TNode fromNode;
                            float fromCost;
                            if (hasNodeParent)
                            {
                                if (Toggle.IsToggled<THasLineOfSightNode>())
                                {
                                    if ((typeof(TGraph).IsValueType ? (IGraphLineOfSight<TNode>)graph : lineOfSightNodeReferenceType).HasLineOfSight(nodeParent, neighbour))
                                    {
                                        fromCost = costFromParent;
                                        fromNode = nodeParent;
                                        goto getNewCost;
                                    }
                                }
                                else if (Toggle.IsToggled<THasLineOfSightCoord>()
                                    && (typeof(TGraph).IsValueType ? (IGraphLineOfSight<TCoord>)graph : lineOfSightCoordReferenceType)
                                    .HasLineOfSight(coordParent, graph.ToPosition(neighbour)))
                                {
                                    fromCost = costFromParent;
                                    fromNode = nodeParent;
                                    goto getNewCost;
                                }
                            }
                            fromNode = node;
                            fromCost = costFromNode;

                        getNewCost:
                            float newCost = graph.GetCost(fromNode, neighbour) + fromCost;

                            if (!builder.TryGetCost(neighbour, out float oldCost) || newCost < oldCost)
                            {
                                builder.SetCost(neighbour, newCost);
                                builder.SetEdge(fromNode, neighbour);
                            }

                            if (Toggle.IsToggled<THasHeuristic>())
                                newCost += (typeof(TSearcher).IsValueType ? ((ISearcherHeuristic<TNode>)searcher) : searcherHeuristicReferenceType).GetHeuristicCost(neighbour);

                            builder.EnqueueToVisit(neighbour, newCost);

                            if (!watchdog.CanContinue(out TWatchdogAwaitable awaitable_))
                                goto timedout;
                            await awaitable_;
                        }
                    }

                    endOfLoop:
                    if (!watchdog.CanContinue(out TWatchdogAwaitable awaitable))
                        goto timedout;
                    await awaitable;
                }

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
                await builder.FinalizeBuilderSession<TGraph, TWatchdog, TWatchdogAwaitable, TWatchdogAwaiter, TThreadingAwaitable, TThreadingAwaiter>(graph, result, watchdog);
                builder.FeedPathTo<TBuilder, TPath, TCoord>(path);
                ObjectPool<TBuilder>.Shared.Return(builder);
            }
        }
    }
}