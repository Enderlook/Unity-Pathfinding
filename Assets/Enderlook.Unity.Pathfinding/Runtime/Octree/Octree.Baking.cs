using System.Collections.Generic;

using UnityEditor;

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
        internal void DrawOctantWithHandle(OctantCode code)
        {
            Octant octant = octants[code];
            Handles.DrawWireCube(octant.Center, Vector3.one * octant.GetSize(size));
        }

        /// <summary>
        /// Only use in Editor.
        /// </summary>
        internal int OctantsCount => (octants?.Count) ?? 0;
#endif

        internal void SubdivideFromObstacles(LayerMask filterInclude, bool includeTriggerColliders)
        {
            if (octants is null)
                octants = new Dictionary<OctantCode, Octant>();
            else
                octants.Clear();

            QueryTriggerInteraction query = includeTriggerColliders ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            Collider[] test = new Collider[1];

            octants[OctantCode.Root] = new Octant(OctantCode.Root);

            (LayerMask filterInclude, QueryTriggerInteraction query, Collider[] test) tuple = (filterInclude, query, test);
            if (CheckChild(OctantCode.Root, center, ref tuple))
                octants[OctantCode.Root] = new Octant(OctantCode.Root, center, Octant.StatusFlags.IsIntransitable);

            FilterFloor();
        }

        private void FilterFloor()
        {
            // TODO: this is extremelly innefficient

            List<OctantCode> toChange = new List<OctantCode>();
            foreach (KeyValuePair<OctantCode, Octant> kvp in octants)
            {
                if (kvp.Value.IsIntransitable)
                    continue;

                float epsilon = 1f / size / MaxDepth; // Some random value to find octant below
                OctantCode belowCode = FindClosestNodeTo(kvp.Value.Center + Vector3.down * (kvp.Value.GetSize(size) + epsilon));
                if (belowCode.IsInvalid)
                    continue;

                Octant belowOctant = octants[belowCode];
                if (belowOctant.IsIntransitable)
                    toChange.Add(kvp.Key);
            }

            foreach (OctantCode code in toChange)
            {
                Octant octant = octants[code];
                octant.HasGround = true;
                octants[code] = octant;
            }
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