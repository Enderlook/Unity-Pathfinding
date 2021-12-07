using Enderlook.Unity.Pathfinding.Utils;

using System;
using System.Runtime.CompilerServices;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Dummy awaitable that is always completed.
    /// </summary>
    internal struct DummyAwaitable : IAwaitable<DummyAwaitable.Awaiter>
    {
        /// <inheritdoc cref="IAwaitable{T}.GetAwaiter"/>
        public Awaiter GetAwaiter() => default;

        /// <summary>
        /// Dummy awaiter that is always completed.
        /// </summary>
        internal struct Awaiter : IAwaiter
        {
            /// <inheritdoc cref="IAwaiter.IsCompleted"/>
            public bool IsCompleted
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => true;
            }

            /// <inheritdoc cref="IAwaiter.GetResult"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void GetResult() { }

            /// <inheritdoc cref="INotifyCompletion.OnCompleted(Action)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnCompleted(Action continuation) => continuation();
        }
    }
}