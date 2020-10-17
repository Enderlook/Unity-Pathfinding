using System.Diagnostics;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Helper methods for <see cref="PathState"/>.
    /// </summary>
    public static class PathStateHelper
    {
        public static PathState ToPathState(this CalculationResult result)
        {
            switch (result)
            {
                case CalculationResult.PathFound:
                    return PathState.PathFound;
                case CalculationResult.PathNotFound:
                    return PathState.PathNotFound;
                case CalculationResult.Timedout:
                    return PathState.Timedout;
                default:
                    Debug.Fail("Impossible State.");
                    return PathState.Empty;
            }
        }
    }
}