using System;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [Serializable]
    internal sealed partial class Octree : ISerializationCallbackReceiver
    {
        private const int GROW_ARRAY_FACTOR_MULTIPLICATIVE = 2;
        private const int GROW_ARRAY_FACTOR_ADDITIVE = 8;

        [SerializeField]
        private NodeInner[] nodes;

        private int nodesCount;

        [SerializeField]
        private Vector3 center;

        [SerializeField]
        private float size;

        [SerializeField]
        private int subdivisions;

        public Octree(Vector3 center, float size, int subdivisions)
        {
            this.center = center;
            this.size = size;
            this.subdivisions = subdivisions;
            nodes = Array.Empty<NodeInner>();
        }

        private struct NodeInner
        {
            public int childrenStartAtIndex;
        }

        internal void Reset(Vector3 center, float size, int subdivisions)
        {
            if (nodes is null)
                nodes = Array.Empty<NodeInner>();
            else
            {
                nodesCount = 0;
                Array.Clear(nodes, 0, nodes.Length);
            }
            this.center = center;
            this.size = size;
            this.subdivisions = subdivisions;
        }

        internal void SubdivideFromObstacles(LayerMask filterInclude, bool includeTriggerColliders)
        {
            QueryTriggerInteraction query = includeTriggerColliders ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            Collider[] test = new Collider[1];

            if (nodesCount == 0)
            {
                EnsureAdditionalCapacity(1);
                nodesCount++;
            }

            CheckChild(0, center, size, 0);

            void CheckChild(int index, Vector3 center, float size, int depth)
            {
                if (depth > subdivisions)
                    return;

                int count = Physics.OverlapBoxNonAlloc(center, Vector3.one * size / 2, test, Quaternion.identity, filterInclude, query);
                if (count == 0)
                    return;

                int childrenStartAtIndex = nodes[index].childrenStartAtIndex;
                if (childrenStartAtIndex == 0)
                {
                    nodes[index].childrenStartAtIndex = childrenStartAtIndex = nodesCount;
                    EnsureAdditionalCapacity(8);
                    nodesCount += 8;
                }

                depth++;
                size /= 2;

                CheckChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir0 * size * .5f), size, depth);
                CheckChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir1 * size * .5f), size, depth);
                CheckChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir2 * size * .5f), size, depth);
                CheckChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir3 * size * .5f), size, depth);
                CheckChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir4 * size * .5f), size, depth);
                CheckChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir5 * size * .5f), size, depth);
                CheckChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir6 * size * .5f), size, depth);
                CheckChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir7 * size * .5f), size, depth);
            }
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

        internal void DrawGizmos()
        {
            if (nodes is null || nodesCount == 0)
                return;

            DrawChild(0, center, size, 0);
            void DrawChild(int index, Vector3 center, float size, int depth)
            {
                Gizmos.color = Color.Lerp(Color.blue, Color.red, (float)depth / subdivisions);
                Gizmos.DrawWireCube(center, Vector3.one * size);

                NodeInner node = nodes[index];
                int childrenStartAtIndex = node.childrenStartAtIndex;
                if (childrenStartAtIndex == 0)
                    return;

                depth++;
                size /= 2;

                DrawChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir0 * size * .5f), size, depth);
                DrawChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir1 * size * .5f), size, depth);
                DrawChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir2 * size * .5f), size, depth);
                DrawChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir3 * size * .5f), size, depth);
                DrawChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir4 * size * .5f), size, depth);
                DrawChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir5 * size * .5f), size, depth);
                DrawChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir6 * size * .5f), size, depth);
                DrawChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir7 * size * .5f), size, depth);
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() => Array.Resize(ref nodes, nodesCount);

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (nodes is null)
                nodes = Array.Empty<NodeInner>();
            else
                nodesCount = nodes.Length;
        }
    }
}