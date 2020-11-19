namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Interface to check if two coordinates has line of sight in a graph.
    /// </summary>
    /// <typeparam name="TCoord">Type of coordinate.</typeparam>
    internal interface IGraphLineOfSight<TCoord>
    {
        /// <summary>
        /// Determines if both coordinates has line of sight.
        /// </summary>
        /// <param name="from">Start coodinate.</param>
        /// <param name="to">End coordinate.</param>
        /// <returns>Whenever they have line of sight or not.</returns>
        bool HasLineOfSight(TCoord from, TCoord to);
    }
}