using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree
    {
        private Dictionary<OctantCode, Octant> octants;

#if UNITY_EDITOR
        /// <summary>
        /// Only use in Editor.
        /// </summary>
        internal int OctantsCount => (octants?.Count) ?? 0;
#endif

        internal void SubdivideFromObstacles(LayerMask filterInclude, bool includeTriggerColliders)
        {
            Clear();

            QueryTriggerInteraction query = includeTriggerColliders ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            Collider[] test = new Collider[1];

            octants[new OctantCode(1)] = new Octant(new OctantCode(1));

            (LayerMask filterInclude, QueryTriggerInteraction query, Collider[] test) tuple = (filterInclude, query, test);
            if (CheckChild(new OctantCode(1), center, ref tuple))
                octants[new OctantCode(1)] = new Octant(new OctantCode(1), center, Octant.StatusFlags.IsIntransitable);

            CalculateConnectionsBruteForce();

            isSerializationUpdated = false;
        }

        private bool CheckChild(
            OctantCode code,
            Vector3 center,
            ref (LayerMask filterInclude, QueryTriggerInteraction query, Collider[] test) tuple)
        {
            Octant octant = new Octant(code, center)
            {
                Center = center
            };

            float currentSize = octant.GetSize(size) * .5f;

            int count = Physics.OverlapBoxNonAlloc(center, Vector3.one * currentSize, tuple.test, Quaternion.identity, tuple.filterInclude, tuple.query);
            if (count == 0)
            {
                octants[code] = octant;
                return false;
            }

            // At this point there are obstacles

            if (code.Depth > subdivisions)
            {
                // There are obstacles but we can't subdivide more
                // So make the octant intransitable
                octant.Flags |= Octant.StatusFlags.IsIntransitable | Octant.StatusFlags.IsLeaf;
                octants[code] = octant;
                return true;
            }

            // At this point we can subdivide
            currentSize *= .5f;
            uint firstChild = code.GetChildTh(0).Code;
            OctantCode code0 = new OctantCode(firstChild++);
            OctantCode code1 = new OctantCode(firstChild++);
            OctantCode code2 = new OctantCode(firstChild++);
            OctantCode code3 = new OctantCode(firstChild++);
            OctantCode code4 = new OctantCode(firstChild++);
            OctantCode code5 = new OctantCode(firstChild++);
            OctantCode code6 = new OctantCode(firstChild++);
            OctantCode code7 = new OctantCode(firstChild);
            if (
                CheckChild(code0, center + (ChildrenPositions.Child0 * currentSize), ref tuple) &
                CheckChild(code1, center + (ChildrenPositions.Child1 * currentSize), ref tuple) &
                CheckChild(code2, center + (ChildrenPositions.Child2 * currentSize), ref tuple) &
                CheckChild(code3, center + (ChildrenPositions.Child3 * currentSize), ref tuple) &
                CheckChild(code4, center + (ChildrenPositions.Child4 * currentSize), ref tuple) &
                CheckChild(code5, center + (ChildrenPositions.Child5 * currentSize), ref tuple) &
                CheckChild(code6, center + (ChildrenPositions.Child6 * currentSize), ref tuple) &
                CheckChild(code7, center + (ChildrenPositions.Child7 * currentSize), ref tuple)
                )
            {
                // If all children are intransitable, we can kill them and just mark this node as intransitable to save space

                octants.Remove(code0);
                octants.Remove(code1);
                octants.Remove(code2);
                octants.Remove(code3);
                octants.Remove(code4);
                octants.Remove(code5);
                octants.Remove(code6);
                octants.Remove(code7);

                octant.Flags |= Octant.StatusFlags.IsIntransitable | Octant.StatusFlags.IsLeaf;
                octants[code] = octant;
                return true;
            }

            octants[code] = octant;
            return false;
        }
    }
}