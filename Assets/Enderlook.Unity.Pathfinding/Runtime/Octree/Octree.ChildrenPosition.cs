namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree
    {
        internal enum ChildrenPosition
        {
            BottomLeftFront = 0,
            BottomRightBack = 1,
            BottomLeftBack = 2,
            BottomRightFront = 3,
            TopLeftFront = 4,
            TopRightFront = 5,
            TopLeftBack = 6,
            TopRightBack = 7,
        }
    }
}