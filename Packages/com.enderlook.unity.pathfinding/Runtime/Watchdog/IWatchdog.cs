using Enderlook.Unity.Pathfinding.Utils;

using System;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Interface used to define a time slicer and or prematurely halt the execution of an algorithm.
    /// </summary>
    /// <typeparam name="TAwaitable">Type of awaitable.</typeparam>
    /// <typeparam name="TAwaiter">Type of awaiter.</typeparam>
    internal interface IWatchdog<TAwaitable, TAwaiter> : IDisposable
        where TAwaitable : IAwaitable<TAwaiter>
        where TAwaiter : IAwaiter
    {
        /// <summary>
        /// Check if it should continue.
        /// </summary>
        /// <param name="awaitable">If returns <see langword="true"/>, this is the awaitable to await.</param>
        /// <returns>Whenever it can continue to calculate of it should halt.</returns>
        bool CanContinue(out TAwaitable awaitable);

        /// <summary>
        /// Whenever it should use multithreading for the operation.
        /// </summary>
        bool UseMultithreading { get; }
    }
}