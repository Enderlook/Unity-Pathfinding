using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Steerings
{
    /// <summary>
    /// Defines an object which determines the direction of an agent
    /// </summary>
    internal interface ISteering
    {
        /// <summary>
        /// Determines the current direction to follow.
        /// </summary>
        /// <param name="agent">Rigidbody of the agent which has this steering.</param>
        /// <returns>Direction to follow.</returns>
        Vector3 GetDirection(Rigidbody agent);
    }
}
