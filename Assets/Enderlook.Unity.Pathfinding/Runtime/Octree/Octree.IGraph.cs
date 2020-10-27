using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    public sealed partial class Octree : IGraphIntrinsic<Octree.OctantCode, IReadOnlyCollection<Octree.OctantCode>>, IGraphLocation<Octree.OctantCode, Vector3>
    {
        /// <inheritdoc cref="IGraphLocation{TNode, TCoord}.ToPosition(TNode)"/>
        public Vector3 ToPosition(OctantCode code) => octants[code].Center;

        /// <inheritdoc cref="IGraphLocation{TNode, TCoord}.FindClosestNodeTo(TCoord)"/>
        public OctantCode FindClosestNodeTo(Vector3 position)
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

                        return octant.Code;
                    }
                }
                return octant.Code;
            }

            return OctantCode.Invalid;

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

        private Dictionary<(OctantCode, OctantCode), float> distances;

        /// <inheritdoc cref="IGraphIntrinsic{TNode}.GetCost(TNode, TNode)"/>
        public float GetCost(OctantCode from, OctantCode to)
        {
            if (distances.TryGetValue((from, to), out float value))
                return value;

            if (from.IsInvalid || to.IsInvalid)
                value = float.NaN;
            else
                value = Vector3.Distance(octants[from].Center, octants[to].Center);

            distances[(from, to)] = value;
            return value;
        }

        /// <inheritdoc cref="IGraphIntrinsic{TNode, TNodes}.GetNeighbours(TNode)"/>
        public IReadOnlyCollection<OctantCode> GetNeighbours(OctantCode node) => connections[node];

        /// <inheritdoc cref="IGraphIntrinsic{TNode}.GetNeighbours(TNode)"/>
        IEnumerable<OctantCode> IGraphIntrinsic<OctantCode>.GetNeighbours(OctantCode node) => GetNeighbours(node);

        public void CalculatePath<TBuilder, TWatchdog>(Vector3 from, Vector3 to, TBuilder builder, TWatchdog watchdog)
            where TBuilder : IPathBuilder<OctantCode, Vector3>
            where TWatchdog : IWatchdog
            => AStar.CalculatePath<OctantCode, Vector3, Octree, TBuilder, TWatchdog, IReadOnlyCollection<OctantCode>>(from, to, this, builder, watchdog);

        public void CalculatePath<TBuilder>(Vector3 from, Vector3 to, TBuilder builder)
            where TBuilder : IPathBuilder<OctantCode, Vector3>
            => AStar.CalculatePath<OctantCode, Vector3, Octree, TBuilder, IReadOnlyCollection<OctantCode>>(from, to, this, builder);
    }
}
