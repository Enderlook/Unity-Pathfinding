using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree
    {
        private struct InnerOctant
        {
            //  0 -> Completely transitable but has no children
            // -1 -> Intransitable Leaf
            // -2 -> Intransitable Non-Leaf (all its children are intransitable)
            // -3 -> Not serialize
            public int ChildrenStartAtIndex;

            public int ParentIndex;

            public LocationCode Code;

            public Vector3 Center;

            public bool HasChildren => ChildrenStartAtIndex > 0;

            public bool IsLeaf => ChildrenStartAtIndex == 0 || ChildrenStartAtIndex == -1;

            public bool IsIntransitable => ChildrenStartAtIndex == -1 || ChildrenStartAtIndex == -2;

            public InnerOctant(int parentIndex, int childrenStartAtIndex, LocationCode code, Vector3 center)
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
    }
}