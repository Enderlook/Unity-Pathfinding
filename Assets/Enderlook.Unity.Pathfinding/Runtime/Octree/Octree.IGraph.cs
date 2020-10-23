using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree :
        IGraphIntrinsic<Octree.OctantCode, IReadOnlyCollection<Octree.OctantCode>>
        //IGraphLocation<Octree.OctantCode, Vector3>
    {
        /*/// <inheritdoc cref="IGraphLocation{TNode, TCoord}.FindClosestNodeTo(TCoord)"/>
        public OctantCode FindClosestNodeTo(Vector3 position)
        {
            Octant octant = octants[OctantCode.Root];

            if (octant.Contains(position, size))
            {

            }
        }*/

        /// <inheritdoc cref="IGraphIntrinsic{TNodeId}.GetCost(TNodeId, TNodeId)"/>
        public float GetCost(OctantCode from, OctantCode to) => Vector3.Distance(octants[from].Center, octants[to].Center);

        /// <inheritdoc cref="IGraphIntrinsic{TNode, TNodes}.GetNeighbours(TNode)"/>
        public IReadOnlyCollection<OctantCode> GetNeighbours(OctantCode node) => connections[node];

        /// <inheritdoc cref="IGraphIntrinsic{TNodeId}.GetNeighbours(TNodeId)"/>
        IEnumerable<OctantCode> IGraphIntrinsic<OctantCode>.GetNeighbours(OctantCode node) => connections[node];
    }
}
