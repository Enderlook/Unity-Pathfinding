namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Interface to check if two coordinates has line of sight in a graph.
    /// </summary>
    /// <typeparam name="TCoord">Type of coordinate.</typeparam>
    internal interface IGraphLineOfSight<TCoord>
    {
        /// <summary>
        /// <see langword="true"/> if <see cref="HasLineOfSight(TCoord, TCoord)"/> can only be executed from Unity thread.<br/>
        /// Otherwise it can be executed from other threads.<br/>
        /// If this is not respected, undefined behaviour.
        /// </summary>
        bool RequiresUnityThread { get; }

        /// <summary>
        /// Determines if both coordinates has line of sight.
        /// </summary>
        /// <param name="from">Start coodinate.</param>
        /// <param name="to">End coordinate.</param>
        /// <returns>Whenever they have line of sight or not.</returns>
        bool HasLineOfSight(TCoord from, TCoord to);
    }
}