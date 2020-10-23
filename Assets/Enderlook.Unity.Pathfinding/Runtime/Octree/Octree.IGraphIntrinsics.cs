using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree : IGraphIntrinsic<Octree.OctantCode, ICollection<Octree.OctantCode>>
    {
        /// <inheritdoc cref="IGraphIntrinsic{TNodeId}.GetCost(TNodeId, TNodeId)"/>
        public float GetCost(OctantCode from, OctantCode to) => Vector3.Distance(octants[from].Center, octants[to].Center);

        /// <inheritdoc cref="IGraphIntrinsic{TNode, TNodes}.GetNeighbours(TNode)"/>
        public ICollection<OctantCode> GetNeighbours(OctantCode node) => throw new System.NotImplementedException();

        /// <inheritdoc cref="IGraphIntrinsic{TNodeId}.GetNeighbours(TNodeId)"/>
        IEnumerable<OctantCode> IGraphIntrinsic<OctantCode>.GetNeighbours(OctantCode node) => throw new System.NotImplementedException();
    }
}
