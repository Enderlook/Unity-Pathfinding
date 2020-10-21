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
            if (CheckChild(0, center, size, 0, new LocationCode(1), ref tuple))
                octants.SetIndexAndKey(new InnerOctant(-1, -2, new LocationCode(1), center), 0);
        }

        private bool CheckChild(int index, Vector3 center, float size, int depth, LocationCode code, ref (LayerMask filterInclude, QueryTriggerInteraction query, Collider[] test) tuple)
        {
            ref InnerOctant octant = ref octants[index];
            octant.Center = center;
            octants.MapIndexWithKey(index, code);

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

            if (CheckChild(childrenStartAtIndex++, center + (ChildrenPositions.Child0 * size * .5f), size, depth, code.GetChildTh(0), ref tuple) &
                CheckChild(childrenStartAtIndex++, center + (ChildrenPositions.Child1 * size * .5f), size, depth, code.GetChildTh(1), ref tuple) &
                CheckChild(childrenStartAtIndex++, center + (ChildrenPositions.Child2 * size * .5f), size, depth, code.GetChildTh(2), ref tuple) &
                CheckChild(childrenStartAtIndex++, center + (ChildrenPositions.Child3 * size * .5f), size, depth, code.GetChildTh(3), ref tuple) &
                CheckChild(childrenStartAtIndex++, center + (ChildrenPositions.Child4 * size * .5f), size, depth, code.GetChildTh(4), ref tuple) &
                CheckChild(childrenStartAtIndex++, center + (ChildrenPositions.Child5 * size * .5f), size, depth, code.GetChildTh(5), ref tuple) &
                CheckChild(childrenStartAtIndex++, center + (ChildrenPositions.Child6 * size * .5f), size, depth, code.GetChildTh(6), ref tuple) &
                CheckChild(childrenStartAtIndex  , center + (ChildrenPositions.Child7 * size * .5f), size, depth, code.GetChildTh(7), ref tuple))
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