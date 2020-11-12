using Assets.Enderlook.Unity.Pathfinding;
using Assets.Enderlook.Unity.Pathfinding.Steerings;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [AddComponentMenu("Enderlook/Pathfinding/Navigation Agent"), RequireComponent(typeof(Rigidbody)), DisallowMultipleComponent, DefaultExecutionOrder(ExecutionOrder.NavigationAgent)]
    public sealed class NavigationAgent : MonoBehaviour
    {
        [SerializeField, Tooltip("Determines if the agent has control over the rigidbody.")]
        public bool isStopped;

        [Header("Steering")]
        [SerializeField, Min(0), Tooltip("Determines the maximum speed of the agent while following a path.")]
        private float linealSpeed = 3.5f;
        public float LinealSpeed {
            get => linealSpeed;
            set => linealSpeed = ErrorMessage.NoNegativeGuard(nameof(LinealSpeed), value);
        }

        [SerializeField, Min(0), Tooltip("Determines the acceleration of the lineal speed while following a path.")]
        private float linealAcceleration = 10;
        public float LinealAcceleration {
            get => linealAcceleration;
            set => linealAcceleration = ErrorMessage.NoNegativeGuard(nameof(LinealAcceleration), value);
        }

        [SerializeField, Min(0), Tooltip("Determines the turning speed while following a path.")]
        private float angularSpeed = 120;
        public float AngularSpeed {
            get => angularSpeed;
            set => angularSpeed = ErrorMessage.NoNegativeGuard(nameof(AngularSpeed), value);
        }

        [SerializeField, Min(0), Tooltip("Determines the minimal distance from the target to consider it as reached.")]
        private float stoppingDistance = .25f;
        public float StoppingDistance {
            get => stoppingDistance;
            set => stoppingDistance = ErrorMessage.NoNegativeGuard(nameof(StoppingDistance), value);
        }

        [Header("Obstacle Avoidance")]
        [SerializeField, Tooltip("Configuration of the obstacle avoidance.")]
        public ObstacleAvoidance obstacleAvoidance;

        internal Rigidbody Rigidbody;

        private DynamicArray<Vector3> innerPath;
        internal DynamicArray<Vector3>.Enumerator enumerator;
        private bool hasPath;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void Awake()
        {
            Rigidbody = GetComponent<Rigidbody>();
            innerPath = DynamicArray<Vector3>.Create();

            Rigidbody.constraints |= RigidbodyConstraints.FreezeRotation;

            if (TryGetComponent(out NavigationAgentAutoPath autoPath))
                autoPath.Inject(this);
        }

#if UNITY_EDITOR
        private Vector3 gizmosMovementDirection;
        private Vector3 gizmosAvoidDirection;
#endif

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void FixedUpdate()
        {
            if (isStopped)
                return;

            Vector3 movementDirection = CalculateCurrentDirection();
            Vector3 avoidDirection = obstacleAvoidance.GetDirection(Rigidbody);
            Vector3 direction = movementDirection + avoidDirection;
            MoveAndRotate(direction);

#if UNITY_EDITOR
            gizmosMovementDirection = movementDirection;
            gizmosAvoidDirection = avoidDirection;
#endif
        }

        private Vector3 CalculateCurrentDirection()
        {
            if (!hasPath)
                return Vector3.zero;

            Vector3 current = enumerator.Current;
            current.y = Rigidbody.position.y;

            Vector3 direction = current - Rigidbody.position;
            float distance = direction.magnitude;
            if (distance <= stoppingDistance)
            {
                if (!enumerator.MoveNext())
                {
                    hasPath = false;
                    return Vector3.zero;
                }
            }

            return direction.normalized;
        }

        private void MoveAndRotate(Vector3 direction)
        {
            direction = direction.normalized;
            direction.y = 0;

            Vector3 targetSpeed = direction * linealSpeed;
            Rigidbody.velocity = Vector3.MoveTowards(Rigidbody.velocity, targetSpeed, linealAcceleration * Time.fixedDeltaTime);

            Rigidbody.rotation = Quaternion.RotateTowards(Rigidbody.rotation, Quaternion.LookRotation(-direction), angularSpeed * Time.fixedDeltaTime);
        }

        /// <summary>
        /// Set the path to follow.
        /// </summary>
        /// <param name="path"></param>
        public void SetPath(Path<Vector3> path)
        {
            innerPath.Clear();
            innerPath.AddRange(path.AsSpan);
            enumerator = innerPath.GetEnumerator();
            enumerator.MoveNext();
            hasPath = true;
        }

#if UNITY_EDITOR
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
                Rigidbody = GetComponent<Rigidbody>();

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(Rigidbody.position, obstacleAvoidance.AvoidanceRadius);

            Color orange = (Color.yellow + Color.red) / 2;
            orange.a = 1;
            Gizmos.color = orange;
            Gizmos.DrawWireSphere(Rigidbody.position, obstacleAvoidance.AvoidanceRadius + obstacleAvoidance.AvoidancePredictionRadius);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(Rigidbody.position, Rigidbody.position + Rigidbody.velocity);

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(Rigidbody.position, Rigidbody.position - transform.forward);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(Rigidbody.position, Rigidbody.position + gizmosMovementDirection);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(Rigidbody.position, Rigidbody.position + gizmosAvoidDirection);
        }
#endif
    }
}