using System;
using System.Diagnostics;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Helper methods for <see cref="PathBuilderState"/>.
    /// </summary>
    public static class PathHelper
    {
        /// <summary>
        /// Feed the information from <paramref name="source"/> to <paramref name="to"/>.
        /// If <c><paramref name="source"/>>.Status == <see cref="PathBuilderState.InProgress"/></c>, this has undefined behaviour.
        /// </summary>
        /// <typeparam name="TInfo">Type of information feeded to the path.</typeparam>
        /// <param name="source">Source of information.</param>
        /// <param name="to">Where the information is being feed to.</param>
        public static void FeedPathTo<TInfo>(this IPathFeeder<TInfo> source, IPathFeedable<TInfo> to)
        {
#if UNITY_EDITOR || DEBUG
            if (source.Status == PathBuilderState.InProgress)
                throw new InvalidOperationException(PathBuilder<TInfo>.CAN_NOT_GET_PATH_IN_PROGRESS);
#endif
            to.Feed(source);
        }

        internal static PathBuilderState ToPathBuilderState(this CalculationResult result)
        {
            switch (result)
            {
                case CalculationResult.PathFound:
                    return PathBuilderState.PathFound;
                case CalculationResult.PathNotFound:
                    return PathBuilderState.PathNotFound;
                case CalculationResult.Timedout:
                    return PathBuilderState.Timedout;
                default:
                    Debug.Fail("Impossible State.");
                    return PathBuilderState.Empty;
            }
        }

        internal static PathState ToPathState(this PathBuilderState result)
        {
            switch (result)
            {
                case PathBuilderState.PathFound:
                    return PathState.PathFound;
                case PathBuilderState.InProgress:
                    return PathState.InProgress;
                case PathBuilderState.Empty:
                case PathBuilderState.PathNotFound:
                case PathBuilderState.Timedout:
                    return PathState.EmptyOrNotFound;
                default:
                    Debug.Fail("Impossible State.");
                    return PathState.EmptyOrNotFound;
            }
        }
    }
}