using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Defines which game object are used to determine the path area.
    /// </summary>
    public enum CollectionType
    {
        /// <summary>
        /// Use all game objects which's <see cref="Transform.position"/> is inside the scene.
        /// </summary>
        Volume,

        /// <summary>
        /// Use all game object which are children of this component.
        /// </summary>
        Children,
    }
}