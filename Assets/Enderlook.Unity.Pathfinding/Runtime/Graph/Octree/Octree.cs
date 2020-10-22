using System;
using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree
    {
        [SerializeField]
        private Vector3 center;

        [SerializeField]
        private float size;

        [SerializeField]
        private byte subdivisions;

        private Dictionary<LocationCode, InnerOctant> octants;

        internal int OctantsCount => octants.Count;

        public Octree(Vector3 center, float size, byte subdivisions)
        {
            this.center = center;
            this.size = size;
            this.subdivisions = subdivisions;
            if (subdivisions > 9)
                throw new ArgumentOutOfRangeException(nameof(subdivisions), "Must be a value from 1 to 10.", subdivisions.ToString());
        }

        internal void Reset(Vector3 center, float size, byte subdivisions)
        {
            if (subdivisions > 9)
                throw new ArgumentOutOfRangeException(nameof(subdivisions), "Must be a value from 1 to 10.", subdivisions.ToString());

            octantsBytes = null;

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

            octants[new LocationCode(1)] = new InnerOctant(new LocationCode(1));

            (LayerMask filterInclude, QueryTriggerInteraction query, Collider[] test) tuple = (filterInclude, query, test);
            if (CheckChild(new LocationCode(1), center, ref tuple))
                octants[new LocationCode(1)] = new InnerOctant(new LocationCode(1), center, InnerOctantFlags.IsIntransitable);
        }

        private bool CheckChild(
            LocationCode code,
            Vector3 center,
            ref (LayerMask filterInclude, QueryTriggerInteraction query, Collider[] test) tuple)
        {
            InnerOctant octant = new InnerOctant(code, center);
            octant.Center = center;

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
                octant.IsIntransitable = true;
                octants[code] = octant;
                return true;
            }

            // At this point we can subdivide
            currentSize *= .5f;
            LocationCode code0 = code.GetChildTh(0);
            LocationCode code1 = code.GetChildTh(1);
            LocationCode code2 = code.GetChildTh(2);
            LocationCode code3 = code.GetChildTh(3);
            LocationCode code4 = code.GetChildTh(4);
            LocationCode code5 = code.GetChildTh(5);
            LocationCode code6 = code.GetChildTh(6);
            LocationCode code7 = code.GetChildTh(7);
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

                octant.IsIntransitable = true;
                octants[code] = octant;
                return true;
            }

            octants[code] = octant;
            return false;
        }
    }
}