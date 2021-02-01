using System.Runtime.CompilerServices;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Helper methods for <see cref="PathBuilderStatus"/>.
    /// </summary>
    internal static class PathHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FeedPathTo<TInfo>(this IPathFeeder<TInfo> source, IPathFeedable<TInfo> to) => to.Feed(source);
    }
}