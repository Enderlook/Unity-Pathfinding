using Enderlook.Collections;

using System;
using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [Serializable]
    public abstract class Graph<TCoord, TSpatialIndex> : IGraphIntrinsic<NodeId, IReadOnlyList<NodeId>>, ISerializationCallbackReceiver
        where TSpatialIndex : ISpatialIndex<TCoord, int>, new()
    {
#pragma warning disable CS0649
        [SerializeField]
        private NodeInner[] nodes = Array.Empty<NodeInner>();
#pragma warning restore CS0649

        private TSpatialIndex tree;

        /// <inheritdoc cref="IGraphIntrinsic{TNode}.GetCost(TNode, TNode)"/>
        public abstract float GetCost(NodeId from, NodeId to);

        /// <inheritdoc cref="IGraphIntrinsic{TNode, TNodes}.GetNeighbours(TNode)"/>
        public IReadOnlyList<NodeId> GetNeighbours(NodeId node) => nodes[node.id].edges;

        protected Node GetNode(NodeId node) => nodes[node.id].ToNode();

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            tree = new TSpatialIndex();
            for (int i = 0; i < nodes.Length; i++)
            {
                tree.Insert(nodes[i].position, i);
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() =>
#if UNITY_EDITOR
            // If we are in the editor, we can try and optimize nodes by sorting them.
            Optimize();
#endif


        private void Optimize()
        {
            int nodesLength = nodes.Length;
            NodeClass[] nodesClass = new NodeClass[nodesLength];
            for (int i = 0; i < 0; i++)
                nodesClass[i] = new NodeClass() { position = nodes[i].position };

            Dictionary<NodeClass, NodeId[]> oldEdges = new Dictionary<NodeClass, NodeId[]>(nodesLength);

            for (int i = 0; i < nodesLength; i++)
            {
                NodeClass node = nodesClass[i];

                NodeId[] edges_ = nodes[i].edges;
                oldEdges[node] = edges_;

                NodeClass[] edges = new NodeClass[edges_.Length];
                for (int j = 0; j < edges_.Length; j++)
                    edges[j] = nodesClass[edges_[j].id];
                node.edges = edges;
            }

            Array.Sort(nodesClass);

            Dictionary<NodeClass, int> index = new Dictionary<NodeClass, int>(nodesLength);
            for (int i = 0; i < nodesLength; i++)
            {
                NodeClass node = nodesClass[i];
                nodes[i].position = node.position;
                index[node] = i;
            }
            for (int i = 0; i < nodesLength; i++)
            {
                NodeClass node = nodesClass[i];
                NodeClass[] edges_ = node.edges;
                NodeId[] edges = oldEdges[node];
                for (int j = 0; j < edges_.Length; j++)
                    edges[j] = new NodeId(index[edges_[j]]);
                nodes[i].edges = edges;
                Array.Sort(edges); // Not necessary
            }
        }

        /// <inheritdoc cref="IGraphIntrinsic{TNodeId}.GetNeighbours(TNodeId)"/>
        IEnumerable<NodeId> IGraphIntrinsic<NodeId>.GetNeighbours(NodeId node) => GetNeighbours(node);

        [Serializable]
        /// <summary>
        /// Represent an ephemereal node.
        /// </summary>
        private struct NodeInner
        {
            public TCoord position;

            public NodeId[] edges;

            public Node ToNode() =>
                // TODO: Use MemoryMarshal
                new Node(position, edges);
        }

        /// <summary>
        /// Represent an ephemereal node.
        /// </summary>
        protected readonly struct Node
        {
            /// <summary>
            /// Position of the node.
            /// </summary>
            public readonly TCoord Position;

            /// <summary>
            /// Don't store this content outside of the node.
            /// </summary>
            public readonly IReadOnlyList<NodeId> Edges;

            public Node(TCoord position, NodeId[] edges)
            {
                Position = position;
                Edges = edges;
            }
        }

#if UNITY_EDITOR
        [Serializable]
        private class NodeClass
        {
            public TCoord position;
            public NodeClass[] edges;
        }
#endif
    }
}