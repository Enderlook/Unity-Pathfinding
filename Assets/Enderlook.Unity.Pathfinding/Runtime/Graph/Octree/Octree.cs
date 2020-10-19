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
        private byte subdivisions;

        private InnerOctant[] octants;
        private int octantsCount;
        /// <summary>
        /// Each index in this list represent 8 contiguos free octans in the <see cref="octants"/> array.
        /// </summary>
        private Stack<int> freeOctanRegions;

        public Octree(Vector3 center, float size, byte subdivisions)
        {
            this.center = center;
            this.size = size;
            this.subdivisions = subdivisions;
            serializedOctantsRaw = Array.Empty<int>();
        }

        private struct InnerOctant
        {
            //  0 -> Completely transitable but has no children
            // -1 -> Intransitable Leaf
            // -2 -> Intransitable Non-Leaf (all its children are intransitable)
            // -3 -> Not serialize
            public int ChildrenStartAtIndex;

            public Vector3 Center;

            public bool HasChildren => ChildrenStartAtIndex > 0;

            public bool IsLeaf => ChildrenStartAtIndex == 0 || ChildrenStartAtIndex == -1;

            public bool IsIntransitable => ChildrenStartAtIndex == -1 || ChildrenStartAtIndex == -2;

            public InnerOctant(int childrenStartAtIndex, Vector3 center)
            {
                ChildrenStartAtIndex = childrenStartAtIndex;
                Center = center;
            }

            public InnerOctant(int childrenStartAtIndex)
            {
                ChildrenStartAtIndex = childrenStartAtIndex;
                Center = default;
            }

            public void SetTraversableLeaf() => ChildrenStartAtIndex = 0;

            public void SetIntransitableLeaf() => ChildrenStartAtIndex = -1;

            public void SetIntransitableParent() => ChildrenStartAtIndex = -2;
        }

        internal void Reset(Vector3 center, float size, byte subdivisions)
        {
#if !UNITY_EDITOR
            if (serializedOctantsRaw is null)
                serializedOctantsRaw = Array.Empty<int>();
            else
                Array.Clear(serializedOctantsRaw, 0, serializedOctantsRaw.Length);
#else
            serializedOctantsRaw = Array.Empty<int>();
#endif
            this.center = center;
            this.size = size;
            this.subdivisions = subdivisions;

            if (octants is null)
                octants = Array.Empty<InnerOctant>();
            else
                Array.Clear(octants, 0, octants.Length);
            octantsCount = 0;
        }

        internal void SubdivideFromObstacles(LayerMask filterInclude, bool includeTriggerColliders)
        {
            QueryTriggerInteraction query = includeTriggerColliders ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            Collider[] test = new Collider[1];

            if (octantsCount == 0)
            {
                EnsureAdditionalCapacity(1);
                octantsCount++;
            }

            (LayerMask filterInclude, QueryTriggerInteraction query, Collider[] test) tuple = (filterInclude, query, test);
            if (CheckChild(0, center, size, 0, ref tuple))
                octants[0] = new InnerOctant(-2, center);
        }

        private bool CheckChild(int index, Vector3 center, float size, int depth, ref (LayerMask filterInclude, QueryTriggerInteraction query, Collider[] test) tuple)
        {
            octants[index].Center = center;

            size /= 2;

            int count = Physics.OverlapBoxNonAlloc(center, Vector3.one * size, tuple.test, Quaternion.identity, tuple.filterInclude, tuple.query);
            if (count == 0)
            {
                // If the node isn't a leaf we make it a leaf because there are no obstacles
                if (!octants[index].IsLeaf)
                {
                    Reclaim(octants[index].ChildrenStartAtIndex);
                    octants[index].SetTraversableLeaf();
                }
                return false;
            }

            // At this point there are obstacles

            if (depth > subdivisions)
            {
                // There are obstacles but we can't subdivide more
                // So make the node an intransitable leaf 
                octants[index].SetIntransitableLeaf();
                return true;
            }

            // At this point we can subdivide

            int childrenStartAtIndex = octants[index].ChildrenStartAtIndex;
            if (childrenStartAtIndex <= 0)
            {
                // The node doesn't have any children, so we add room for them
                childrenStartAtIndex = GetChildrenSpace();
                octants[index].ChildrenStartAtIndex = childrenStartAtIndex;
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
                CheckChild(childrenStartAtIndex, center + (DirectionsHelper.Dir7 * size * .5f), size, depth, ref tuple))
            {
                // If all children are intransitable, we can kill them and just mark this node as intransitable to save space

                octants[index].SetIntransitableParent();
                Reclaim(old);
                return true;
            }
            return false;
        }

        private void Reclaim(int childrenStartAtIndex)
        {
            freeOctanRegions.Push(childrenStartAtIndex);
            int to = childrenStartAtIndex + 8;
            for (int i = childrenStartAtIndex; i < to; i++)
                octants[i] = default;
        }

        private int GetChildrenSpace()
        {
            if (freeOctanRegions.TryPop(out int index))
                return index;

            EnsureAdditionalCapacity(8);
            index = octantsCount;
            octantsCount += 8;
            return index;
        }

        private void EnsureAdditionalCapacity(int additional)
        {
            int required = octantsCount + additional;
            if (required <= octants.Length)
                return;

            int newSize = (octants.Length * GROW_ARRAY_FACTOR_MULTIPLICATIVE) + GROW_ARRAY_FACTOR_ADDITIVE;
            if (newSize < required)
                newSize = required;

            Array.Resize(ref octants, newSize);
        }
    }
}