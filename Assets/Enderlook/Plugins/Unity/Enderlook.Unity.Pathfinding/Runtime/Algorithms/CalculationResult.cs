namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Determines the result of a path calculation.
    /// </summary>
    public enum CalculationResult
    {
        /// <summary>
        /// The path was found successfully.
        /// </summary>
        PathFound,

        /// <summary>
        /// The path was not found.
        /// </summary>
        PathNotFound,

        /// <summary>
        /// The path was not found because a watch dog finalized the calculation prematurely.
        /// </summary>
        Timedout,
    }
}