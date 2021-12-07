using Enderlook.Pools;
using Enderlook.Unity.Pathfinding.Utils;

using System;
using System.Runtime.CompilerServices;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// A single threaded watchdog that uses a time slicer.
    /// </summary>
    internal struct TimeSliceWatchdog : IWatchdog<TimeSlicer.Yielder, TimeSlicer.Yielder>
    {
        private TimeSlicer timeSlicer;

        /// <inheritdoc cref="IWatchdog{TAwaitable, TAwaiter}.UseMultithreading"/>
        public bool UseMultithreading { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => false; }

        public TimeSliceWatchdog(int executionTime)
        {
            timeSlicer = ObjectPool<TimeSlicer>.Shared.Rent();
            timeSlicer.ExecutionTimeSlice = executionTime;
        }

        /// <inheritdoc cref="IWatchdog{TAwaitable, TAwaiter}.CanContinue(out TAwaitable)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanContinue(out TimeSlicer.Yielder awaitable)
        {
            awaitable = timeSlicer.Yield();
            return true;
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            ObjectPool<TimeSlicer>.Shared.Return(timeSlicer);
            timeSlicer = default;
        }
    }
}