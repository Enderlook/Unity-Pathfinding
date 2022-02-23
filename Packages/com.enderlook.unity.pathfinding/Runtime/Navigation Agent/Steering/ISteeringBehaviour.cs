using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Steerings
{
    /// <summary>
    /// Defines an object which determines the direction of an agent.
    /// </summary>
    internal interface ISteeringBehaviour
    {
        /// <summary>
        /// Determines the current direction to follow.
        /// </summary>
        /// <returns>Direction to follow.</returns>
        Vector3 GetDirection();
    }
}
