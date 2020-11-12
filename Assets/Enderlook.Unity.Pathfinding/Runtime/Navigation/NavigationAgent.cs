using Assets.Enderlook.Unity.Pathfinding;
using Assets.Enderlook.Unity.Pathfinding.Steerings;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [AddComponentMenu("Enderlook/Pathfinding/Navigation Agent"), RequireComponent(typeof(Rigidbody)), DisallowMultipleComponent, DefaultExecutionOrder(ExecutionOrder.NavigationAgent)]
    public sealed class NavigationAgent : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Tooltip("Configuration of the agent movement.")]
        public Movement movement;

        [Header("Path Follower")]
        [SerializeField, Tooltip("Configuration of the path following behaviour.")]
        public PathFollower PathFollower;

        [SerializeField, Tooltip("Navigation volume used to calculate path when requested. You don't need to assing this if you plan to manually set the path.")]
        public NavigationVolume NavigationVolume;

        [Header("Obstacle Avoidance")]
        [SerializeField, Tooltip("Configuration of the obstacle avoidance.")]
        public ObstacleAvoidance ObstacleAvoidance;

        private Path<Vector3> path;
        private bool canSetPath;
        private new Rigidbody rigidbody;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void Awake()
        {
            rigidbody = GetComponent<Rigidbody>();
            movement.Initialize(rigidbody);
            path = new Path<Vector3>();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void FixedUpdate()
        {
            if (path.IsComplete && canSetPath)
            {
                canSetPath = false;
                path.Complete();
                PathFollower.SetPath(path);
            }

            if (movement.IsStopped)
                return;

            movement.MoveAndRotate(rigidbody, GetDirection());
        }

        /// <summary>
        /// Set the target destination of this agent.
        /// </summary>
        /// <param name="destination">Destiantion to follow.</param>
        public void SetDestinationSync(Vector3 destination)
        {
            NavigationVolume.CalculatePathSync(path, rigidbody.position, destination);
            PathFollower.SetPath(path);
        }

        /// <summary>
        /// Set the target destination of this agent.
        /// </summary>
        /// <param name="destination">Destiantion to follow.</param>
        public void SetDestination(Vector3 destination)
        {
            NavigationVolume.CalculatePath(path, rigidbody.position, destination);
            canSetPath = true;
        }

        private Vector3 GetDirection() => PathFollower.GetDirection(rigidbody) + ObstacleAvoidance.GetDirection(rigidbody);
    }
}