using System;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// A watchdog that will stop after reaching a certain time.
    /// </summary>
    public readonly struct TimerWatchdog : IWatchdog
    {
        /// <summary>
        /// Time where it must halt.
        /// </summary>
        public readonly DateTime StopAt;

        /// <inheritdoc cref="IWatchdog.CanContinue"/>
        public bool CanContinue() => DateTime.Now < StopAt;

        /// <summary>
        /// Creates a watchdog that will halt at <paramref name="stopAt"/>.
        /// </summary>
        /// <param name="stopAt">When the watchdog will halt.</param>
        public TimerWatchdog(DateTime stopAt) => StopAt = stopAt;

        /// <summary>
        /// Creates a watchdog that will halt after <paramref name="miliseconds"/> miliseconds from now.
        /// </summary>
        /// <param name="miliseconds">Miliseconds left to halt.</param>
        public TimerWatchdog(double miliseconds) => StopAt = DateTime.Now.AddMilliseconds(miliseconds);
    }
}