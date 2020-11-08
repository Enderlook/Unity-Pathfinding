using System.Runtime.CompilerServices;

using Unity.Jobs;

namespace Enderlook.Unity.Pathfinding
{
    internal interface IProcessHandleSourceCompletition
    {
        /// <summary>
        /// Starts a process.
        /// </summary>
        void Start();

        /// <summary>
        /// Attach a <see cref="JobHandle"/> to a process.
        /// </summary>
        /// <param name="jobHandle">Job handle to attach.</param>
        void SetJobHandle(JobHandle jobHandle);

        /// <summary>
        /// Forces the completition of an attached job handle.
        /// </summary>
        void CompleteJobHandle();

        /// <summary>
        /// Mark a process as complete.
        /// </summary>
        void End();
    }
}