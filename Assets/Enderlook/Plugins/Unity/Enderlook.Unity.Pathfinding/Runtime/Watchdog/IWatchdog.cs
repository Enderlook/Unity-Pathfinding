namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Determines a watchdog to prematurely halt the execution of an algorithm.
    /// </summary>
    public interface IWatchdog
    {
        /// <summary>
        /// Check if it should continue.
        /// </summary>
        /// <returns>Whenever it can continue to calculate of it should halt.</returns>
        bool CanContinue();
    }
}