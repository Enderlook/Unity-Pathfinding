using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree : IGraphIntrinsic<Octree.OctantCode, HashSet<Octree.OctantCode>.Enumerator>, IGraphLocation<Octree.OctantCode, Vector3>, IGraphHeuristic<Octree.OctantCode>, IGraphLineOfSight<Vector3>
    {
        private static readonly HashSet<OctantCode> EmptyHashset = new HashSet<OctantCode>();

        /// <inheritdoc cref="IGraphLocation{TNode, TCoord}.ToPosition(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 ToPosition(OctantCode code) => octants[code].Center;

        private DynamicArray<(OctantCode, Vector3)> positions;

        /// <inheritdoc cref="IGraphLocation{TNode, TCoord}.FindClosestNodeTo(TCoord)"/>
        public OctantCode FindClosestNodeTo(Vector3 position)
        {
            Octant octant = FindBoundingOctant(position);
            if (!octant.HasInvalidCode && !octant.IsIntransitable && octant.HasGround)
                return octant.Code;

            if (positions.IsDefaultOrEmpty)
                return octant.Code;

            OctantCode closest = positions[0].Item1;
            float distance = Vector3.Distance(position, positions[0].Item2);
            for (int i = 0; i < positions.Count; i++)
            {
                float v = Vector3.Distance(position, positions[i].Item2);
                if (v < distance)
                {
                    distance = v;
                    closest = positions[i].Item1;
                }
            }
            return closest;
        }

        private Octant FindBoundingOctant(Vector3 position)
        {
            Octant octant = octants[OctantCode.Root];

            int depth = 0;
            if (octant.Contains(position, size))
            {
                loop:
                uint firstChild = octant.Code.GetChildTh(0).Code;
                for (uint i = 0; i < 8; i++)
                {
                    if (!octants.TryGetValue(new OctantCode(firstChild + i), out Octant child))
                        continue;

                    if (child.Contains(position, size))
                    {
                        if (depth++ < MaxDepth)
                        {
                            octant = child;
                            goto loop;
                        }

                        return octant;
                    }
                }
                return octant;
            }

            return default;

            /*  This could be useful later. It allows to calculate the center of a leaf octant which contains this position
               int maxDepth = MaxDepth + 1;
               float smallestSize = size / maxDepth * .5f;

               position -= center;
               position /= smallestSize;

               int x = (int)Mathf.Floor(position.x);
               int y = (int)Mathf.Floor(position.y);
               int z = (int)Mathf.Floor(position.z);

               // If we wanted to get the center position of the deepest octant which has this point we could do
               Vector3 center = center + smallestSize * new Vector3(x, y, z) + Vector3.one * smallestSize * .5f;
           */
        }

        public int CachedDistances => distances?.Count ?? 0;
        private ConcurrentDictionary<(OctantCode, OctantCode), float> distances;

        /// <inheritdoc cref="IGraphIntrinsic{TNode}.GetCost(TNode, TNode)"/>
        public float GetCost(OctantCode from, OctantCode to)
        {
            if (distances.TryGetValue((from, to), out float value))
                return value;

            if (from.IsInvalid || to.IsInvalid)
                value = float.NaN;
            else
                value = Vector3.Distance(octants[from].Center, octants[to].Center);

            distances.TryAdd((from, to), value);
            return value;
        }

        /// <inheritdoc cref="IGraphIntrinsic{TNode, TNodes}.GetNeighbours(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HashSet<OctantCode>.Enumerator GetNeighbours(OctantCode node)
        {
            if (connections.TryGetValue(node, out HashSet<OctantCode> neighbours))
                return neighbours.GetEnumerator();
            return EmptyHashset.GetEnumerator();
        }

        /// <inheritdoc cref="IGraphHeuristic{TNode}.GetHeuristicCost(TNode, TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetHeuristicCost(OctantCode from, OctantCode to) => GetCost(from, to);

        public int CachedLineOfSights => lineOfSigths?.Count ?? 0;
        private Dictionary<(Vector3, Vector3), bool> lineOfSigths;

        /// <inheritdoc cref="IGraphLineOfSight{TCoord}.HasLineOfSight(TCoord, TCoord)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            (Vector3, Vector3) tuple = (from, to);
            if (!lineOfSigths.TryGetValue(tuple, out bool hasLineOfSight))
            {
                hasLineOfSight = !Physics.Linecast(from, to, filterInclude, query);
                lineOfSigths.Add(tuple, hasLineOfSight);
            }
            return hasLineOfSight;
        }
    }
}
