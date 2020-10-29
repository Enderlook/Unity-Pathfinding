using System;

using Unity.Jobs;

namespace Enderlook.Unity.Pathfinding
{
    internal interface IProcessHandleSourceCompletition
    {
        /// <summary>
        /// Mark a job handle task as completed.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if this handler is empty, is already completed, or is not from a job handle.</exception>
        void EndFromJobHandle();

        /// <summary>
        /// Mark a sync task as completed.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if this handler is empty, is already completed, or is not sync.</exception>
        void EndFromSync();

        /// <summary>
        /// Initializes the handle from a <see cref="JobHandle"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if this handler is not empty or is in progress.</exception>
        void StartFromJobHandle(JobHandle jobHandle);

        /// <summary>
        /// Initializes the handle as sync.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if this handler is not empty or is in progress.</exception>
        void StartFromSync();
    }
}