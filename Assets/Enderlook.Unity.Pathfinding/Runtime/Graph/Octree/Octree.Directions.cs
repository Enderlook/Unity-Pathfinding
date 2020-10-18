namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree
    {
        /// <summary>
        /// Determines the direction of each <see cref="Octree"/> child.
        /// </summary>
        private enum Directions
        {
            //  X,  Y,  Z
            LeftUpFoward,       // -1,  1,  1
            LeftUpBack,         // -1,  1, -1
            LeftDownFoward,     // -1, -1,  1
            LeftDownBack,       // -1, -1,  -1
            RightUpFoward,      //  1,  1,  1
            RightUpBack,        //  1,  1, -1
            RightDownFoward,    //  1, -1,  1
            RightDownBack,      //  1, -1, -1
        }
    }
}