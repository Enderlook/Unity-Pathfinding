namespace Enderlook.Unity.Pathfinding.Utils
{
    /// <summary>
    /// Interface used to define an object that can be awaited.
    /// </summary>
    /// <typeparam name="TAwaiter">Type of awaiter.</typeparam>
    internal interface IAwaitable<TAwaiter> where TAwaiter : IAwaiter
    {
        /// <summary>
        /// Awaiter of this awaitable.
        /// </summary>
        /// <returns>Awaiter</returns>
        TAwaiter GetAwaiter();
    }
}