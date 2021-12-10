using System.Threading.Tasks;

namespace Enderlook.Unity.Pathfinding.Utils
{
    /// <summary>
    /// Represent an object which can accept a task which represent the underlying work of this object.
    /// </summary>
    internal interface ISetTask
    {
        /// <summary>
        /// Accepts a tasks which represent the underlying work of this object.
        /// </summary>
        /// <param name="task">Task to accept.</param>
        void SetTask(ValueTask task);
    }
}