﻿using Enderlook.Collections;

using System;
using System.Collections.Generic;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// A pathfinder that uses the Dijkstra algorithm.<br/>
    /// Instances of this class are not thread safe.
    /// </summary>
    /// <typeparam name="TNode">Type of node.</typeparam>
    public class Dijkstra<TNode>
    {
        private const string MULTITHREADING_NOT_SUPPORTED = "The instance doesn't support multithreading. Note that this exception will not be raised on the final build, but undefined behaviour.";

        private HashSet<TNode> visited = new HashSet<TNode>();
        private BinaryHeapMin<TNode, float> toVisit = new BinaryHeapMin<TNode, float>();

#if UNITY_EDITOR || DEBUG
        private bool isBeingUsed;
#endif

        /// <summary>
        /// Calculates a path from <paramref name="from"/> node to some node which satisfy <paramref name="finder"/>.
        /// </summary>
        /// <typeparam name="TFinder">Type of finder.</typeparam>
        /// <param name="from">Node where path start.</param>
        /// <param name="finder">Path builder.</param>
        public void CalculatePath<TFinder>(TNode from, TFinder finder)
            where TFinder : IGraphIntrinsic<TNode>, IPathTargetFinder<TNode>, IPathBuilder<TNode>
        {
#if UNITY_EDITOR || DEBUG
            if (isBeingUsed)
                new InvalidOperationException(MULTITHREADING_NOT_SUPPORTED);
#endif
            finder.Clear();
            finder.SetState(PathState.InProgress);

            if (finder.DoesSatisfy(from))
            {
                finder.SetState(PathState.PathFound);
                return;
            }

            visited.Clear();
            toVisit.Clear();

            finder.SetCost(from, 0);

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
                    if (!finder.TryGetCost(node, out float oldCost) || cost < oldCost)
                    {
                        finder.SetCost(neighbour, cost);
                        finder.SetEdge(node, neighbour);
                    }

                    toVisit.Enqueue(neighbour, cost);

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