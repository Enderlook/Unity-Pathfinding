using Assets.Enderlook.Unity.Pathfinding;

using Enderlook.Unity.Attributes;

using System;

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
        [SerializeField, Tooltip("Determines which layers does the agent tries to avoid.")]
        public LayerMask avoidanceLayers;

        [SerializeField, Min(0), Tooltip("Determines the radius in which tries to avoid obstacles.")]
        private float avoidanceRadius = 1.5f;
        public float AvoidanceRadius {
            get => avoidanceRadius;
            set => avoidanceRadius = ErrorMessage.NoNegativeGuard(nameof(AvoidanceRadius), value);
        }

        [SerializeField, Min(0), Tooltip("Determines the avoidance strength to repel from obstacles.")]
        private float avoidanceStrength = 1;
        public float AvoidanceStrength {
            get => avoidanceStrength;
            set => avoidanceStrength = ErrorMessage.NoNegativeGuard(nameof(AvoidanceStrength), value);
        }

        [SerializeField, Min(0), Tooltip("Determines the prediction time used for moving obstacles to avoid their futures positions.")]
        private float avoidancePredictionTime = 1;
        public float AvoidancePredictionTime {
            get => avoidancePredictionTime;
            set => avoidancePredictionTime = ErrorMessage.NoNegativeGuard(nameof(AvoidancePredictionTime), value);
        }

        [SerializeField, Min(0), ShowIf(nameof(avoidancePredictionTime), typeof(float), 0, mustBeEqual: false), Tooltip("Determines additional detection radius used for moving obstacles.")]
        private float avoidancePredictionRadius = 1;
        public float AvoidancePredictionRadius {
            get => avoidancePredictionRadius;
            set => avoidancePredictionRadius = ErrorMessage.NoNegativeGuard(nameof(AvoidancePredictionRadius), value);
        }

        internal Rigidbody Rigidbody;

        private DynamicArray<Vector3> innerPath;
        internal DynamicArray<Vector3>.Enumerator enumerator;
        private bool hasPath;

        private static Collider[] colliders = new Collider[100];
        private static int COLLIDER_GROW_FACTOR = 2;

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
            Vector3 avoidDirection = CalculateObstacleAvoidance();
            Vector3 direction = movementDirection + avoidDirection * avoidanceStrength;
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

        private Vector3 CalculateObstacleAvoidance()
        {
            float radius = avoidanceRadius;
            if (avoidancePredictionTime != 0)
                radius += avoidancePredictionRadius;

            start:
            int amount = Physics.OverlapSphereNonAlloc(Rigidbody.position, radius, colliders, avoidanceLayers);
            if (amount == colliders.Length)
            {
                Array.Resize(ref colliders, colliders.Length * COLLIDER_GROW_FACTOR);
                goto start;
            }

            if (amount == 0)
                return Vector3.zero;

            Span<Collider> span = colliders.AsSpan(0, amount);
            Vector3 currentPosition = Rigidbody.position;
            Vector3 total = Vector3.zero;
            int count = 0;
            foreach (Collider collider in span)
            {
                if (IsMine(collider.transform))
                    continue;

                Vector3 position;
                Vector3 closestPoint = collider.ClosestPointOnBounds(Rigidbody.position);
                if (collider.TryGetComponent(out Rigidbody _rigidbody))
                    position = closestPoint + _rigidbody.velocity * avoidancePredictionTime;
                else
                    position = closestPoint;

                Vector3 direction = position - currentPosition;
                float distance = direction.magnitude;
                if (distance >= avoidanceRadius)
                    continue;

                count++;
                total -= (avoidanceRadius - distance) / avoidanceRadius * direction;
            }

            if (count == 0)
                return Vector3.zero;

            total /= count;

            if (total.sqrMagnitude > 1)
                total = total.normalized;

            return total;
        }

        private bool IsMine(Transform transform)
        {
            if (transform == this.transform)
                return true;

            transform = transform.parent;
            while (transform != null)
            {
                if (transform == this.transform)
                    return true;
                transform = transform.parent;
            }

            return false;
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
            Gizmos.DrawWireSphere(Rigidbody.position, avoidanceRadius);

            Color orange = (Color.yellow + Color.red) / 2;
            orange.a = 1;
            Gizmos.color = orange;
            Gizmos.DrawWireSphere(Rigidbody.position, avoidanceRadius + avoidancePredictionRadius);

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