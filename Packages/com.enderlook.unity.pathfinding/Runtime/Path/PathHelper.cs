using System.Runtime.CompilerServices;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Helper methods for <see cref="PathBuilderStatus"/>.
    /// </summary>
    internal static class PathHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FeedPathTo<TFeeder, TFeedable, TInfo>(this TFeeder source, TFeedable to)
            where TFeeder : IPathFeeder<TInfo>
            where TFeedable : IPathFeedable<TInfo>
            => to.Feed(source);
    }
}