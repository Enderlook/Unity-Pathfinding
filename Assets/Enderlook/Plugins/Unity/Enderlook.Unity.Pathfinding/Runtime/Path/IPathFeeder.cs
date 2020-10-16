namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Feeds a <see cref="IPathFeedable{TInfo}"/> with path information.
    /// </summary>
    /// <typeparam name="TInfo">Information feeded to the path.</typeparam>
    public interface IPathFeeder<TInfo>
    {
        /// <summary>
        /// Feeds a path with information.
        /// </summary>
        /// <param name="feedable">Path to feed.</param>
        void FeedPathTo(IPathFeedable<TInfo> feedable);
    }
}