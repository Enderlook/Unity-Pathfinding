using System;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Feeds a <see cref="IPathFeedable{TInfo}"/> with path information.
    /// </summary>
    /// <typeparam name="TInfo">Type of information feeded to the path.</typeparam>
    internal interface IPathFeeder<TInfo>
    {
        /// <summary>
        /// Determines if this path contains an actual path.<br/>
        /// This will return <see langword="false"/> if it's empty or hasn't found a path.
        /// </summary>
        bool HasPath { get; }

        /// <summary>
        /// Determines if the calculation was aborted due to a timedout.<br/>
        /// This method will return <see langword="false"/> if <see cref="HasPath"/> is <see langword="true"/>.
        /// </summary>
        bool HasTimedout { get; }

        /// <summary>
        /// Retrieves the path info of this path.
        /// </summary>
        /// <returns>The information of this path.</returns>
        /// <remarks>This can be empty if <see cref="Status"/> is <see cref="PathBuilderStatus.Empty"/>, <see cref="PathBuilderStatus.PathNotFound"/> or <see cref="PathBuilderStatus.Timedout"/>.</remarks>
        ReadOnlySpan<TInfo> GetPathInfo();
    }
}