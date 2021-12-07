using System.Runtime.CompilerServices;

namespace Enderlook.Unity.Pathfinding.Utils
{
    /// <summary>
    /// Interface used to define an awaiter.
    /// </summary>
    internal interface IAwaiter : INotifyCompletion
    {
        /// <summary>
        /// Determines if the awaitable is completed.
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// Completes the awaiter.
        /// </summary>
        void GetResult();
    }
}