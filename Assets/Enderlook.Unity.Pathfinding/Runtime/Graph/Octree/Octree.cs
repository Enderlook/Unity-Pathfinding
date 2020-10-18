using System;
using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree
    {
        private const int GROW_ARRAY_FACTOR_MULTIPLICATIVE = 2;
        private const int GROW_ARRAY_FACTOR_ADDITIVE = 8;

        [SerializeField]
        private Vector3 center;

        [SerializeField]
        private float size;

        [SerializeField]
        private int subdivisions;

        private InnerNode[] nodes;
        private int nodesCount;
        private List<int> free;

        public Octree(Vector3 center, float size, int subdivisions)
        {
            this.center = center;
            this.size = size;
            this.subdivisions = subdivisions;
            serializedNodes = Array.Empty<SerializableNode>();
        }

        private struct InnerNode
        {
            //  0 -> Completely transitable but has no children
            // -1 -> Intransitable Leaf
            // -2 -> Intransitable Non-Leaf (all its children are intransitable)
            // -3 -> Not serialize
            public int ChildrenStartAtIndex;

            public bool HasChildren => ChildrenStartAtIndex > 0;

            public bool IsLeaf => ChildrenStartAtIndex == 0 || ChildrenStartAtIndex == -1;

            public bool IsIntransitable => ChildrenStartAtIndex == -1 || ChildrenStartAtIndex == -2;

            public InnerNode(int childrenStartAtIndex) => ChildrenStartAtIndex = childrenStartAtIndex;

            public void SetTraversableLeaf() => ChildrenStartAtIndex = 0;

            public void SetIntransitableLeaf() => ChildrenStartAtIndex = -1;

            public void SetIntransitableParent() => ChildrenStartAtIndex = -2;
        }

        internal void Reset(Vector3 center, float size, int subdivisions)
        {
#if !UNITY_EDITOR
            if (serializedNodes is null)
                serializedNodes = Array.Empty<SerializableNode>();
            else
                Array.Clear(serializedNodes, 0, serializedNodes.Length);
#else
            serializedNodes = Array.Empty<SerializableNode>();
#endif
            this.center = center;
            this.size = size;
            this.subdivisions = subdivisions;

            if (nodes is null)
                nodes = Array.Empty<InnerNode>();
            else
                Array.Clear(nodes, 0, nodes.Length);
            nodesCount = 0;
        }

        internal void SubdivideFromObstacles(LayerMask filterInclude, bool includeTriggerColliders)
        {
            if (free is null)
                free = new List<int>();

            QueryTriggerInteraction query = includeTriggerColliders ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            Collider[] test = new Collider[1];

            if (nodesCount == 0)
            {
                EnsureAdditionalCapacity(1);
                nodesCount++;
            }

            (LayerMask filterInclude, QueryTriggerInteraction query, Collider[] test) tuple = (filterInclude, query, test);
            if (CheckChild(0, center, size, 0, ref tuple))
                nodes[0] = new InnerNode(-2);
        }

        private bool CheckChild(int index, Vector3 center, float size, int depth, ref (LayerMask filterInclude, QueryTriggerInteraction query, Collider[] test) tuple)
        {
            size /= 2;

            int count = Physics.OverlapBoxNonAlloc(center, Vector3.one * size, tuple.test, Quaternion.identity, tuple.filterInclude, tuple.query);
            if (count == 0)
            {
                // If the node isn't a leaf we make it a leaf because there are no obstacles
                if (!nodes[index].IsLeaf)
                {
                    Reclaim(nodes[index].ChildrenStartAtIndex);
                    nodes[index].SetTraversableLeaf();
                }
                return false;
            }

            // At this point there are obstacles

            if (depth > subdivisions)
            {
                // There are obstacles but we can't subdivide more
                // So make the node an intransitable leaf 
                nodes[index].SetIntransitableLeaf();
                return true;
            }

            // At this point we can subdivide

            int childrenStartAtIndex = nodes[index].ChildrenStartAtIndex;
            if (childrenStartAtIndex <= 0)
            {
                // The node doesn't have any children, so we add room for them
                childrenStartAtIndex = GetChildrenSpace();
                nodes[index].ChildrenStartAtIndex = childrenStartAtIndex;
            }

            depth++;

            int old = childrenStartAtIndex;

            if (CheckChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir0 * size * .5f), size, depth, ref tuple) &
                CheckChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir1 * size * .5f), size, depth, ref tuple) &
                CheckChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir2 * size * .5f), size, depth, ref tuple) &
                CheckChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir3 * size * .5f), size, depth, ref tuple) &
                CheckChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir4 * size * .5f), size, depth, ref tuple) &
                CheckChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir5 * size * .5f), size, depth, ref tuple) &
                CheckChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir6 * size * .5f), size, depth, ref tuple) &
                CheckChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir7 * size * .5f), size, depth, ref tuple))
            {
                // If all children are intransitable, we can kill them and just mark this node as intransitable to save space

                nodes[index].SetIntransitableParent();
                Reclaim(old);
                return true;
            }
            return false;
        }

        private void Reclaim(int childrenStartAtIndex)
        {
            free.Add(childrenStartAtIndex);
            int to = childrenStartAtIndex + 8;
            for (int i = childrenStartAtIndex; i < to; i++)
                nodes[i] = default;
        }

        private int GetChildrenSpace()
        {
            int index;
            if (free.Count > 0)
            {
                int last = free.Count - 1;
                index = free[last];
                free.RemoveAt(last);
                return index;
            }

            EnsureAdditionalCapacity(8);
            index = nodesCount;
            nodesCount += 8;
            return index;
        }

        private void EnsureAdditionalCapacity(int additional)
        {
            int required = nodesCount + additional;
            if (required <= nodes.Length)
                return;

            int newSize = (nodes.Length * GROW_ARRAY_FACTOR_MULTIPLICATIVE) + GROW_ARRAY_FACTOR_ADDITIVE;
            if (newSize < required)
                newSize = required;

            Array.Resize(ref nodes, newSize);
        }
    }
}