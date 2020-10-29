namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Represents a handle for operations that can be run with multithreading.
    /// </summary>
    internal interface IProcessHandle
    {
        /// <summary>
        /// Determines is the current process has completed.
        /// </summary>
        /// <remarks>It returns <see langword="true"/> if the handle is empty or completed.</remarks>
        bool IsComplete { get; }

        /// <summary>
        /// Determines is the current process has pending.
        /// </summary>
        /// <remarks>It returns <see langword="false"/> if the handle is empty or completed.<br/>
        /// This property is the opposite of <see cref="IsComplete"/>.</remarks>
        bool IsPending { get; }

        /// <summary>
        /// Marks as completed or forces the completition of the current process.
        /// </summary>
        /// <remarks>It does nothing if the handle is empty.<br/>
        /// This method must be executed even if <see cref="IsComplete"/> returns <see langword="true"/></remarks>
        void Complete();
    }
}