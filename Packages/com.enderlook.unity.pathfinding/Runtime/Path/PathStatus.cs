namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Determines the state of a <see cref="Path{T}"/>.
    /// </summary>
    public enum PathStatus
    {
        /// <summary>
        /// The path is empty and doesn't contain anything.
        /// </summary>
        EmptyOrNotFound,

        /// <summary>
        /// The path is being processed.
        /// </summary>
        IsPending,

        /// <inheritdoc cref="CalculationResult.PathFound"/>
        PathFound,
    }
}