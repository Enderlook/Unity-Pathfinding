using System;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [Serializable]
    public class Graph3D : Graph<Vector3, Vector3Tree<int>>
    {
        /// <inheritdoc cref="Graph{TCoord, TSpatialIndex}.GetCost(NodeId, NodeId)"/>
        public override float GetCost(NodeId from, NodeId to) => Vector3.Distance(GetNode(from).Position, GetNode(to).Position);
    }
}