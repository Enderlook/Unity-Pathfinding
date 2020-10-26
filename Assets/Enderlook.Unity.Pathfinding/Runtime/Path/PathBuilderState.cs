namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Determines the state of a <see cref="IPathBuilder{TNode}"/>.
    /// </summary>
    public enum PathBuilderState
    {
        /// <summary>
        /// The path is empty and doesn't contain anything.
        /// </summary>
        Empty,

        /// <summary>
        /// The path is being calculated.
        /// </summary>
        InProgress,

        /// <inheritdoc cref="CalculationResult.PathFound"/>
        PathFound,

        /// <inheritdoc cref="CalculationResult.PathNotFound"/>
        PathNotFound,

        /// <inheritdoc cref="CalculationResult.Timedout"/>
        Timedout,
    }
}