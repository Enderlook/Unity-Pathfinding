using Enderlook.Unity.Pathfinding.Utils;

using System;
using System.Runtime.CompilerServices;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// A simple watchdog that never awaits.
    /// </summary>
    internal struct SimpleWatchdog : IWatchdog<DummyAwaitable, DummyAwaitable.Awaiter>
    {
        /// <inheritdoc cref="IWatchdog{TAwaitable, TAwaiter}.UseMultithreading"/>
        public bool UseMultithreading { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; }

        /// <summary>
        /// Creates an instance of the watchdog.
        /// </summary>
        /// <param name="useMultithreading">Whenever it should run in the current thread or run in a thread pool.</param>
        public SimpleWatchdog(bool useMultithreading) => UseMultithreading = useMultithreading;

        /// <inheritdoc cref="IWatchdog{TAwaitable, TAwaiter}.CanContinue(out TAwaitable)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanContinue(out DummyAwaitable awaitable)
        {
            awaitable = new DummyAwaitable();
            return true;
        }

        /// <inheritdoc cref="IDisposable.Dispoe"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() { }
    }
}