using System;

using Unity.Jobs;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Feeds a path with already processed information.
    /// </summary>
    /// <typeparam name="TInfo">Information feeded to the path.</typeparam>
    internal interface IPathFeedable<TInfo>
    {
        /// <summary>
        /// Feeds the path with new information.
        /// </summary>
        /// <param name="feeder">Object which has the path information to get.</param>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="Reserve"/> wasn't previously executed before executing this method.</exception>
        void Feed(IPathFeeder<TInfo> feeder);
    }
}