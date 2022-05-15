namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Interface to check if two points has line of sight in a graph.
    /// </summary>
    /// <typeparam name="T">Type of point.</typeparam>
    internal interface IGraphLineOfSight<T>
    {
        /// <summary>
        /// <see langword="true"/> if <see cref="HasLineOfSight(T, T)"/> can only be executed from Unity thread.<br/>
        /// Otherwise it can be executed from other threads.<br/>
        /// If this is not respected, undefined behaviour.
        /// </summary>
        bool RequiresUnityThread { get; }

        /// <summary>
        /// Determines if both points has line of sight.
        /// </summary>
        /// <param name="from">Start points.</param>
        /// <param name="to">End points.</param>
        /// <returns>Whenever they have line of sight or not.</returns>
        bool HasLineOfSight(T from, T to);
    }
}