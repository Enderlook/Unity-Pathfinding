using System;

namespace Enderlook.Unity.Pathfinding.Utils
{
    /// <summary>
    /// Task used for navigation purposes.
    /// </summary>
    internal readonly struct NavigationTask
    {
        private readonly TimeSlicer timeSlicer;

        /// <summary>
        /// Constructs a tasks from a <see cref="TimeSlicer"/>.
        /// </summary>
        /// <param name="timeSlicer"> <see cref="TimeSlicer"/> of the task.</param>
        public NavigationTask(TimeSlicer timeSlicer) => this.timeSlicer = timeSlicer;

        /// <summary>
        /// Whenever the <see cref="NavigationTask"/> is completed.
        /// </summary>
        public bool IsCompleted => timeSlicer.IsCompleted;

        /// <summary>
        /// Gets an awaiter for this <see cref="NavigationTask"/>.
        /// </summary>
        /// <returns>Awaiter of this <see cref="NavigationTask"/>.</returns>
        public Awaiter GetAwaiter() => new Awaiter(this);

        /// <summary>
        /// Awaiter of <see cref="NavigationTask"/>.
        /// </summary>
        public readonly struct Awaiter
        {
            private readonly NavigationTask task;

            /// <summary>
            /// Constructs an awaiter for <see cref="NavigationTask"/>.
            /// </summary>
            /// <param name="task">Task to <see langword="await"/>.</param>
            public Awaiter(NavigationTask task) => this.task = task;

            /// <summary>
            /// Gets whenever the <see cref="NavigationTask"/> has completed.
            /// </summary>
            public bool IsCompleted => task.timeSlicer.IsCompleted;

            /// <summary>
            /// Gets the result of the <see cref="NavigationTask"/>.
            /// </summary>
            public void GetResult() => task.timeSlicer.CompleteNow();

            /// <summary>
            /// Schedules a continuation action for this <see cref="NavigationTask"/>.
            /// </summary>
            /// <param name="continuation">Continuation to schedule.</param>
            public void OnCompleted(Action continuation) => task.timeSlicer.OnCompleted(continuation);
        }
    }
}