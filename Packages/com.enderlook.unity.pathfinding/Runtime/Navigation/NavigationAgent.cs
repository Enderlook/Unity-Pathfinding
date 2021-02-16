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
        public Movement Movement;

        [Header("Path Follower")]
        [SerializeField, Tooltip("Configuration of the path following behaviour.")]
        public PathFollower PathFollower;

        [SerializeField, Tooltip("Navigation volume used to calculate path when requested. You don't need to assing this if you plan to manually set the path.")]
        public NavigationVolume NavigationVolume;

        [Header("Obstacle Avoidance")]
        [SerializeField, Tooltip("Configuration of the obstacle avoidance.")]
        public ObstacleAvoidance ObstacleAvoidance;

        /// <summary>
        /// Whenever it's calculating a new path.
        /// </summary>
        public bool IsPending { get; private set; }

        private Path<Vector3> path;

        internal Rigidbody Rigidbody { get; private set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void Awake()
        {
            Rigidbody = GetComponent<Rigidbody>();
            Movement.Initialize(Rigidbody);
            path = new Path<Vector3>();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void FixedUpdate()
        {
            if (path.IsComplete && IsPending)
            {
                IsPending = false;
                path.Complete();
                PathFollower.SetPath(path);
            }

            if (Movement.IsStopped)
                return;

            Movement.MoveAndRotate(Rigidbody, GetDirection());
        }

        /// <summary>
        /// Set the target destination of this agent.
        /// </summary>
        /// <param name="destination">Destiantion to follow.</param>
        public void SetDestinationSync(Vector3 destination)
        {
            NavigationVolume.CalculatePathSync(path, Rigidbody.position, destination);
            PathFollower.SetPath(path);
        }

        /// <summary>
        /// Set the target destination of this agent.
        /// </summary>
        /// <param name="destination">Destiantion to follow.</param>
        public void SetDestination(Vector3 destination)
        {
            NavigationVolume.CalculatePath(path, Rigidbody.position, destination);
            IsPending = true;
        }

        private Vector3 GetDirection() => PathFollower.GetDirection(Rigidbody) + ObstacleAvoidance.GetDirection(Rigidbody);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnDestroy() => path.Dispose();
    }
}