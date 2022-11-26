using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Steerings
{
    /// <summary>
    /// Defines an object which determines the direction of an agent.
    /// </summary>
    public interface ISteeringBehaviour
    {
        /// <summary>
        /// Determines the current direction to follow.
        /// </summary>
        /// <returns>Direction to follow.</returns>
        Vector3 GetDirection();

#if UNITY_EDITOR
        /// <summary>
        /// Draw gizmos of this steering behaviour.
        /// </summary>
        void DrawGizmos();
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// Note: This interface is only valid in the editor.
    /// </summary>
    internal interface ISteeringBehaviourEditor
    {
        /// <summary>
        /// Behaviour executed prior to <see cref="ISteeringBehaviour.DrawGizmos"/>.
        /// </summary>
        void PrepareForGizmos();
    }
#endif
}
