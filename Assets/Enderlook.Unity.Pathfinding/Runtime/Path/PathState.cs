﻿namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Determines the state of a <see cref="Path{T}"/>.
    /// </summary>
    public enum PathState
    {
        /// <summary>
        /// The path is empty and doesn't contain anything.
        /// </summary>
        EmptyOrNotFound,

        /// <summary>
        /// The path is being stored.
        /// </summary>
        InProgress,

        /// <inheritdoc cref="CalculationResult.PathFound"/>
        PathFound,
    }
}