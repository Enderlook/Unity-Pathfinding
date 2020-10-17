namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Feeds a path with already processed information.
    /// </summary>
    /// <typeparam name="TInfo">Information feeded to the path.</typeparam>
    public interface IPathFeedable<TInfo>
    {
        /// <summary>
        /// Feeds the path with new information;
        /// </summary>
        /// <param name="information">Information of the path.</param>
        void Feed(TInfo information);
    }
}