using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree
    {
        private static class ChildrenPositions
        {
            public static readonly Vector3 LeftUpFoward = new Vector3(-1, 1, 1);
            public static readonly Vector3 LeftUpBack = new Vector3(-1, 1, -1);
            public static readonly Vector3 LeftDownFoward = new Vector3(-1, -1, 1);
            public static readonly Vector3 LeftDownBack = new Vector3(-1, -1, -1);
            public static readonly Vector3 RightUpFoward = new Vector3(1, 1, 1);
            public static readonly Vector3 RightUpBack = new Vector3(1, 1, -1);
            public static readonly Vector3 RightDownFoward = new Vector3(1, -1, 1);
            public static readonly Vector3 RightDownBack = new Vector3(1, -1, -1);

            public static readonly Vector3 Child0 = new Vector3(-1, 1, 1);    // LeftUpFoward
            public static readonly Vector3 Child1 = new Vector3(-1, 1, -1);   // LeftUpBack
            public static readonly Vector3 Child2 = new Vector3(-1, -1, 1);   // LeftDownFoward
            public static readonly Vector3 Child3 = new Vector3(-1, -1, -1);  // LeftDownBack
            public static readonly Vector3 Child4 = new Vector3(1, 1, 1);     // RightUpFoward
            public static readonly Vector3 Child5 = new Vector3(1, 1, -1);    // RightUpBack
            public static readonly Vector3 Child6 = new Vector3(1, -1, 1);    // RightDownFoward
            public static readonly Vector3 Child7 = new Vector3(1, -1, -1);   // RightDownBack

            public static readonly Vector3Int ChildZ0 = new Vector3Int(0, 1, 1);  // LeftUpFoward
            public static readonly Vector3Int ChildZ1 = new Vector3Int(0, 1, 0);  // LeftUpBack
            public static readonly Vector3Int ChildZ2 = new Vector3Int(0, 0, 1);  // LeftDownFoward
            public static readonly Vector3Int ChildZ3 = new Vector3Int(0, 0, 0);  // LeftDownBack
            public static readonly Vector3Int ChildZ4 = new Vector3Int(1, 1, 1);  // RightUpFoward
            public static readonly Vector3Int ChildZ5 = new Vector3Int(1, 1, 0);  // RightUpBack
            public static readonly Vector3Int ChildZ6 = new Vector3Int(1, 0, 1);  // RightDownFoward
            public static readonly Vector3Int ChildZ7 = new Vector3Int(1, 0, 0);  // RightDownBack

        }
    }
}