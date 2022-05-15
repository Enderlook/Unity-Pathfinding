using Enderlook.Unity.Pathfinding.Utils;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Interface used to define threading preferences.
    /// </summary>
    /// <typeparam name="TAwaitable">Type of awaitable.</typeparam>
    /// <typeparam name="TAwaiter">Type of awaiter.</typeparam>
    internal interface IThreadingPreference<TAwaitable, TAwaiter>
        where TAwaitable : IAwaitable<TAwaiter>
        where TAwaiter : IAwaiter
    {
        /// <summary>
        /// Continues execution in the Unity thread.
        /// </summary>
        /// <returns>Awaiter to continue execution in the Unity thread.</returns>
        TAwaitable ToUnity();
    }
}