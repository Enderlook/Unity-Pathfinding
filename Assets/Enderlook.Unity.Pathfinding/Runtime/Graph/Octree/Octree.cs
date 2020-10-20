using System;

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

        private OctantCollection octants;

        public Octree(Vector3 center, float size, byte subdivisions)
        {
            this.center = center;
            this.size = size;
            this.subdivisions = subdivisions;
            if (subdivisions > 10 || subdivisions < 1)
                throw new ArgumentOutOfRangeException(nameof(subdivisions), "Must be a value from 1 to 10.", subdivisions.ToString());
            serializedOctantsRaw = Array.Empty<int>();
        }

        private struct InnerOctant
        {
            //  0 -> Completely transitable but has no children
            // -1 -> Intransitable Leaf
            // -2 -> Intransitable Non-Leaf (all its children are intransitable)
            // -3 -> Not serialize
            public int ChildrenStartAtIndex;

            public int ParentIndex;

            public MortonCode3D Code;

            public Vector3 Center;

            public bool HasChildren => ChildrenStartAtIndex > 0;

            public bool IsLeaf => ChildrenStartAtIndex == 0 || ChildrenStartAtIndex == -1;

            public bool IsIntransitable => ChildrenStartAtIndex == -1 || ChildrenStartAtIndex == -2;

            public InnerOctant(int parentIndex, int childrenStartAtIndex, MortonCode3D code, Vector3 center)
            {
                ParentIndex = parentIndex;
                ChildrenStartAtIndex = childrenStartAtIndex;
                Center = center;
                Code = code;
            }

            public InnerOctant(int parentIndex, int childrenStartAtIndex)
            {
                ParentIndex = parentIndex;
                ChildrenStartAtIndex = childrenStartAtIndex;
                Center = default;
                Code = default;
            }

            public void SetTraversableLeaf() => ChildrenStartAtIndex = 0;

            public void SetIntransitableLeaf() => ChildrenStartAtIndex = -1;

            public void SetIntransitableParent() => ChildrenStartAtIndex = -2;
        }

        internal void Reset(Vector3 center, float size, byte subdivisions)
        {
            if (subdivisions > 10 || subdivisions < 1)
                throw new ArgumentOutOfRangeException(nameof(subdivisions), "Must be a value from 1 to 10.", subdivisions.ToString());

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

            Clear();
        }

        private void Clear() => octants.Clear();

        internal void SubdivideFromObstacles(LayerMask filterInclude, bool includeTriggerColliders)
        {
            Clear();

            QueryTriggerInteraction query = includeTriggerColliders ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            Collider[] test = new Collider[1];

            octants.SetRoot(default);

            (LayerMask filterInclude, QueryTriggerInteraction query, Collider[] test) tuple = (filterInclude, query, test);
            if (CheckChild(0, center, size, 0, Vector3Int.zero, ref tuple))
                octants[0] = new InnerOctant(-1, -2, new MortonCode3D(Vector3Int.zero), center);
        }

        private bool CheckChild(int index, Vector3 center, float size, int depth, Vector3Int position, ref (LayerMask filterInclude, QueryTriggerInteraction query, Collider[] test) tuple)
        {
            ref InnerOctant octant = ref octants[index];
            octant.Center = center;

            size /= 2;

            int count = Physics.OverlapBoxNonAlloc(center, Vector3.one * size, tuple.test, Quaternion.identity, tuple.filterInclude, tuple.query);
            if (count == 0)
            {
                // If the node isn't a leaf we make it a leaf because there are no obstacles
                if (!octant.IsLeaf)
                {
                    octants.Free8ConsecutiveOctants(octant.ChildrenStartAtIndex);
                    octant.SetTraversableLeaf();
                }
                return false;
            }

            // At this point there are obstacles

            if (depth > subdivisions)
            {
                // There are obstacles but we can't subdivide more
                // So make the node an intransitable leaf 
                octant.SetIntransitableLeaf();
                return true;
            }

            // At this point we can subdivide

            int childrenStartAtIndex = octant.ChildrenStartAtIndex;
            if (childrenStartAtIndex <= 0)
            {
                // The node doesn't have any children, so we add room for them
                childrenStartAtIndex = octants.Allocate8ConsecutiveOctans();
                octant.ChildrenStartAtIndex = childrenStartAtIndex;
            }

            depth++;

            int old = childrenStartAtIndex;

            if (CheckChild(childrenStartAtIndex++, center + (ChildrenPositions.Child0 * size * .5f), size, depth, position + ChildrenPositions.ChildZ0, ref tuple) &
                CheckChild(childrenStartAtIndex++, center + (ChildrenPositions.Child1 * size * .5f), size, depth, position + ChildrenPositions.ChildZ0, ref tuple) &
                CheckChild(childrenStartAtIndex++, center + (ChildrenPositions.Child2 * size * .5f), size, depth, position + ChildrenPositions.ChildZ0, ref tuple) &
                CheckChild(childrenStartAtIndex++, center + (ChildrenPositions.Child3 * size * .5f), size, depth, position + ChildrenPositions.ChildZ0, ref tuple) &
                CheckChild(childrenStartAtIndex++, center + (ChildrenPositions.Child4 * size * .5f), size, depth, position + ChildrenPositions.ChildZ0, ref tuple) &
                CheckChild(childrenStartAtIndex++, center + (ChildrenPositions.Child5 * size * .5f), size, depth, position + ChildrenPositions.ChildZ0, ref tuple) &
                CheckChild(childrenStartAtIndex++, center + (ChildrenPositions.Child6 * size * .5f), size, depth, position + ChildrenPositions.ChildZ0, ref tuple) &
                CheckChild(childrenStartAtIndex  , center + (ChildrenPositions.Child7 * size * .5f), size, depth, position + ChildrenPositions.ChildZ0, ref tuple))
            {
                // If all children are intransitable, we can kill them and just mark this node as intransitable to save space

                octant.SetIntransitableParent();
                octants.Free8ConsecutiveOctants(old);
                return true;
            }
            return false;
        }
    }
}