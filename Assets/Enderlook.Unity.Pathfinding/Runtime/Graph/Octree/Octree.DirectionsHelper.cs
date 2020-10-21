using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree
    {
        private static class ChildrenPositions
        {
            /*          .________._________.
             *         /        /         / |          Y      X     Z           Location   Vector3
             *        /   110  /   111   /  |   0 000  Bottom Left  Front       Back     = Back
             *       /________/_________/   |   1 001  Bottom Right Back        Bottom   = Down
             *      |         |         |   |   2 010  Bottom Left  Back        Front    = Forward
             *      |   100   |   101   |  /|   3 011  Bottom Right Front       Left     = Left
             *      |         |         | / |   4 100  Top    Left  Front       Right    = Right
             *      |_________|_________|/  |   5 101  Top    Right Front       Top      = Up
             *      |         |         |001|   6 110  Top    Left  Back
             * 000  |   010   |   011   |  /    7 111  Top    Right Back
             *      |         |         | /
             *      |_________|_________|/
             */

            public static readonly Vector3 Child0 = Vector3.down + Vector3.left  + Vector3.forward;
            public static readonly Vector3 Child1 = Vector3.down + Vector3.right + Vector3.back;
            public static readonly Vector3 Child2 = Vector3.down + Vector3.left  + Vector3.back;
            public static readonly Vector3 Child3 = Vector3.down + Vector3.right + Vector3.forward;
            public static readonly Vector3 Child4 = Vector3.up   + Vector3.left  + Vector3.forward;
            public static readonly Vector3 Child5 = Vector3.up   + Vector3.right + Vector3.forward;
            public static readonly Vector3 Child6 = Vector3.up   + Vector3.left  + Vector3.back;
            public static readonly Vector3 Child7 = Vector3.up   + Vector3.right + Vector3.back;
        }
    }
}