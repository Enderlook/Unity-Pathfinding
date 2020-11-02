namespace Enderlook.Unity.Pathfinding
{
    internal static partial class ConvexHull
    {
        private enum OrientationType
        {
            Clockwise = -1,
            RightTurn = Clockwise,
            Collinear = 0,
            CounterClockwise = 1,
            LeftTurn = CounterClockwise,
        }
    }
}