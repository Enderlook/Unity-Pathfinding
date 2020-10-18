using System;
using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [Serializable]
    internal sealed partial class Octree : ISerializationCallbackReceiver
    {
        [SerializeField]
        private SerializableNode[] serializedNodes;

#if UNITY_EDITOR
        /// <summary>
        /// Only use in Editor.
        /// </summary>
        internal int SerializedNodesCount => serializedNodes.Length;
#endif

        [Serializable]
        private struct SerializableNode
        {
            //  0 -> Completely transitable but has no children
            // -1 -> Intransitable Leaf
            // -2 -> Intransitable Non-Leaf (all its children are intransitable)
            // ChildrenStartAtIndex
            [SerializeField]
            private int i; // Use a short name to reduce serialization size

            public int ChildrenStartAtIndex {
                get => i;
                set => i = value;
            }

            public SerializableNode(int childrenStartAtIndex) => i = childrenStartAtIndex;
        }
        
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            if (serializedNodes is null)
                serializedNodes = new SerializableNode[nodesCount];
            else if (serializedNodes.Length < nodesCount)
                Array.Resize(ref serializedNodes, nodesCount);

            int freeCount;
            if (free is null || free.Count == 0)
            {
                if (nodesCount == 0)
                    return;

                for (int i = 0; i < nodesCount; i++)
                {
                    Debug.Assert(nodes[i].ChildrenStartAtIndex != 3);
                    serializedNodes[i].ChildrenStartAtIndex = nodes[i].ChildrenStartAtIndex;
                }

                Array.Resize(ref serializedNodes, nodesCount);
            }
            else
            {
                freeCount = free.Count * 8;
                foreach (int index in free)
                {
                    int to = index + 8;
                    for (int i = 0; i < to; i++)
                        nodes[i].ChildrenStartAtIndex = -3;
                }

                int capacity = nodesCount - freeCount;
                Dictionary<int, int> map = new Dictionary<int, int>(capacity < 0 ? 0 : capacity);
                int j = 0;
                for (int i = 0; i < nodesCount; i++)
                {
                    if (nodes[i].ChildrenStartAtIndex != -3)
                        j++;
                    map.Add(i, j);
                }

                j = 0;
                for (int i = 0; i < nodesCount; i++)
                {
                    int oldStart = nodes[i].ChildrenStartAtIndex;
                    int start;
                    if (oldStart == -3)
                        continue;
                    if (oldStart < 0)
                        start = oldStart;
                    else
                        start = map[oldStart];

                    serializedNodes[j++].ChildrenStartAtIndex = start;
                }

                Array.Resize(ref serializedNodes, j);
            }
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (serializedNodes is null)
            {
                nodes = Array.Empty<InnerNode>();
                nodesCount = 0;
            }
            else
            {
                nodesCount = serializedNodes.Length;
                nodes = new InnerNode[nodesCount];
                for (int i = 0; i < nodesCount; i++)
                    nodes[i] = new InnerNode(serializedNodes[i].ChildrenStartAtIndex);
            }
        }
    }
}