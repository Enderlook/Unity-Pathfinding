namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Determines the state of a path.
    /// </summary>
    public enum PathState
    {
        /// <summary>
        /// The path is empty and doesn't contains anything.
        /// </summary>
        Empty,

        /// <summary>
        /// The path is being calculated.
        /// </summary>
        InProgress,

        /// <summary>
        /// The path was found successfully.
        /// </summary>
        PathFound,

        /// <summary>
        /// The path was not found.
        /// </summary>
        PathNotFound,
    }
}