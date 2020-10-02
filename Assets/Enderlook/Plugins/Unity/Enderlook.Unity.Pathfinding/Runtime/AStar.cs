using Enderlook.Collections;

using System;
using System.Collections.Generic;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// A pathfinder that uses the A* algorithm.<br/>
    /// Instances of this class are not thread safe.
    /// </summary>
    /// <typeparam name="TNode">Type of node.</typeparam>
    public class AStar<TNode>
    {
        private HashSet<TNode> visited = new HashSet<TNode>();
        private BinaryHeapMin<TNode, float> toVisit = new BinaryHeapMin<TNode, float>();

#if UNITY_EDITOR || DEBUG
        private bool isBeingUsed;
#endif
        /// <inheritdoc cref="Dijkstra{TNode}.CalculatePath{TFinder}(TNode, TFinder)"/>
        public void CalculatePath<TFinder>(TNode from, TFinder finder)
            where TFinder : IGraphIntrinsic<TNode>, IPathTargetFinderHeuristic<TNode>, IPathBuilder<TNode>
        {
#if UNITY_EDITOR || DEBUG
            if (isBeingUsed)
                new InvalidOperationException("The instance doesn't support multithreading. Note that this exception will not be raised on the final build, but undefined behaviour.");
#endif

            finder.Clear();
            finder.SetState(PathState.InProgress);

            if (finder.DoesSatisfy(from))
            {
#if UNITY_EDITOR || DEBUG
                isBeingUsed = false;
#endif
                finder.SetState(PathState.PathFound);
                return;
            }

            visited.Clear();
            toVisit.Clear();

            toVisit.Enqueue(from, 0);

            while (toVisit.TryDequeue(out TNode node, out _))
            {
                if (visited.Contains(node))
                    continue;
                visited.Add(node);

                if (!finder.TryGetCost(node, out float costFromSource))
                    costFromSource = float.PositiveInfinity;

                foreach (TNode neighbour in finder.GetNeighbours(node))
                {
                    float cost = finder.GetCost(node, neighbour) + costFromSource;
                    if (!finder.TryGetCost(neighbour, out float oldCost) || cost < oldCost)
                    {
                        finder.SetCost(neighbour, cost);
                        finder.SetEdge(node, neighbour);
                    }

                    toVisit.Enqueue(neighbour, cost + finder.GetHeuristicCost(neighbour));

                    if (finder.DoesSatisfy(neighbour))
                    {
#if UNITY_EDITOR || DEBUG
                        isBeingUsed = false;
#endif
                        finder.SetState(PathState.PathFound);
                        return;
                    }
                }
            }

#if UNITY_EDITOR || DEBUG
            isBeingUsed = false;
#endif
            finder.SetState(PathState.PathNotFound);
        }
    }
}