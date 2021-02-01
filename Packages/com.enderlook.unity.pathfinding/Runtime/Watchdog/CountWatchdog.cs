namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// A watchdog that will stop after a number of iterations.
    /// </summary>
    public struct CountWatchdog : IWatchdog
    {
        /// <summary>
        /// Remaining number of iterations before halt.<br/>
        /// Each time <see cref="CanContinue"/> is executed this value is decremented by one.
        /// </summary>
        public int RemainingIterations { get; private set; }

        /// <inheritdoc cref="IWatchdog.CanContinue"/>
        public bool CanContinue() => --RemainingIterations >= 0;

        /// <summary>
        /// Creates a watchdog that will halt after the specified number of iterations.
        /// </summary>
        /// <param name="iterations">Number of iterations before halt.</param>
        public CountWatchdog(int iterations) => RemainingIterations = iterations;
    }
}