using System.Collections.Generic;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Feeds a <see cref="IPathFeedable{TInfo}"/> with path information.
    /// </summary>
    /// <typeparam name="TInfo">Type of information feeded to the path.</typeparam>
    public interface IPathFeeder<TInfo>
    {
        /// <summary>
        /// Determines the state of the path.
        /// </summary>
        PathBuilderState Status { get; }

        /// <summary>
        /// Retrieves the path info of this path.<br/>
        /// This can be empty if <see cref="Status"/> is <see cref="PathBuilderState.Empty"/> , <see cref="PathBuilderState.PathNotFound"/> or <see cref="PathBuilderState.Timedout"/>.<br/>
        /// If <see cref="Status"/> is <see cref="PathBuilderState.InProgress"/>, this has undefined behaviour.
        /// </summary>
        /// <returns>The information of this path.</returns>
        IEnumerable<TInfo> GetPathInfo();
    }
}