using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [AddComponentMenu("Enderlook/Pathfinding/Navigation Agent Auto Path"), RequireComponent(typeof(NavigationAgent)), DisallowMultipleComponent, DefaultExecutionOrder(ExecutionOrder.NavigationAgentAutoPath)]
    public class NavigationAgentAutoPath : MonoBehaviour
    {
        [SerializeField, Tooltip("Determines the navigation volume used for pathfinding.")]
        public NavigationVolume volume;

        public NavigationAgent Agent { get; private set; }

        private Path<Vector3> path;
        private bool canSetPath;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void Awake()
        {
            Agent = GetComponent<NavigationAgent>();
            path = new Path<Vector3>();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void FixedUpdate()
        {
            if (path.IsComplete && canSetPath)
            {
                canSetPath = false;
                path.Complete();
                Agent.SetPath(path);
            }
        }

        /// <summary>
        /// Set the target destination of this agent.
        /// </summary>
        /// <param name="destination">Destiantion to follow.</param>
        public void SetDestinationSync(Vector3 destination)
        {
            volume.CalculatePathSync(path, Agent.Rigidbody.position, destination);
            Agent.SetPath(path);
        }

        /// <summary>
        /// Set the target destination of this agent.
        /// </summary>
        /// <param name="destination">Destiantion to follow.</param>
        public void SetDestination(Vector3 destination)
        {
            volume.CalculatePath(path, Agent.Rigidbody.position, destination);
            canSetPath = true;
        }

        internal void Inject(NavigationAgent navigationAgent) => Agent = navigationAgent;
    }
}