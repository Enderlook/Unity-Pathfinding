using Enderlook.Unity.Attributes;

using System;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [AddComponentMenu("Enderlook/Pathfinding/Navigation Agent"), RequireComponent(typeof(Rigidbody))]
    public class NavigationAgent : MonoBehaviour
    {
        private const string CAN_NOT_BE_NEGATIVE = "Can't be negative.";

        [SerializeField, Tooltip("Determines if the agent has control over the rigidbody.")]
        public bool isStopped;

        [Header("Steering")]
        [SerializeField, Min(0), Tooltip("Determines the maximum speed of the agent while following a path.")]
        private float linealSpeed = 3.5f;
        public float LinealSpeed {
            get => linealSpeed;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(LinealSpeed), CAN_NOT_BE_NEGATIVE);
                linealSpeed = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the acceleration of the lineal speed while following a path.")]
        private float linealAcceleration = 10;
        public float LinealAcceleration {
            get => linealAcceleration;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(LinealAcceleration), CAN_NOT_BE_NEGATIVE);
                linealAcceleration = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the turning speed while following a path.")]
        private float angularSpeed = 120;
        public float AngularSpeed {
            get => angularSpeed;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(AngularSpeed), CAN_NOT_BE_NEGATIVE);
                angularSpeed = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the minimal distance from the target to consider it as reached.")]
        private float stoppingDistance = .25f;
        public float StoppingDistance {
            get => stoppingDistance;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(StoppingDistance), CAN_NOT_BE_NEGATIVE);
                stoppingDistance = value;
            }
        }

        [Header("Obstacle Avoidance")]
        [SerializeField, Tooltip("Determines which layers does the agent tries to avoid.")]
        public LayerMask avoidanceLayers;

        [SerializeField, Min(0), Tooltip("Determines the radius in which tries to avoid obstacles.")]
        private float avoidanceRadius = 1.5f;
        public float AvoidanceRadius {
            get => avoidanceRadius;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(AvoidanceRadius), CAN_NOT_BE_NEGATIVE);
                avoidanceRadius = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the avoidance strength to repel from obstacles.")]
        private float avoidanceStrength = 1;
        public float AvoidanceStrength {
            get => avoidanceRadius;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(AvoidanceStrength), CAN_NOT_BE_NEGATIVE);
                avoidanceStrength = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the prediction time used for moving obstacles to avoid their futures positions.")]
        private float avoidancePredictionTime = 1;
        public float AvoidancePredictionTime {
            get => avoidancePredictionTime;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(AvoidancePredictionTime), CAN_NOT_BE_NEGATIVE);
                avoidancePredictionTime = value;
            }
        }

        [SerializeField, Min(0), ShowIf(nameof(avoidancePredictionTime), typeof(float), 0, mustBeEqual: false), Tooltip("Determines additional detection radius used for moving obstacles.")]
        private float avoidancePredictionRadius = 1;
        public float AvoidancePredictionRadius {
            get => avoidancePredictionRadius;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(AvoidancePredictionRadius), CAN_NOT_BE_NEGATIVE);
                avoidancePredictionRadius = value;
            }
        }

        private new Rigidbody rigidbody;

        private DynamicArray<Vector3> innerPath;
        internal DynamicArray<Vector3>.Enumerator enumerator;
        private bool hasPath;

        private static Collider[] colliders = new Collider[100];
        private static int COLLIDER_GROW_FACTOR = 2;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void Awake()
        {
            rigidbody = GetComponent<Rigidbody>();
            innerPath = DynamicArray<Vector3>.Create();

            rigidbody.constraints |= RigidbodyConstraints.FreezeRotation;
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
            current.y = rigidbody.position.y;

            Vector3 direction = current - rigidbody.position;
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
            int amount = Physics.OverlapSphereNonAlloc(rigidbody.position, radius, colliders, avoidanceLayers);
            if (amount == colliders.Length)
            {
                Array.Resize(ref colliders, colliders.Length * COLLIDER_GROW_FACTOR);
                goto start;
            }

            if (amount == 0)
                return Vector3.zero;

            Span<Collider> span = colliders.AsSpan(0, amount);
            Vector3 currentPosition = rigidbody.position;
            Vector3 total = Vector3.zero;
            int count = 0;
            foreach (Collider collider in span)
            {
                if (IsMine(collider.transform))
                    continue;

                Vector3 position;
                Vector3 closestPoint = collider.ClosestPointOnBounds(rigidbody.position);
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
            rigidbody.velocity = Vector3.MoveTowards(rigidbody.velocity, targetSpeed, linealAcceleration * Time.fixedDeltaTime);

            rigidbody.rotation = Quaternion.RotateTowards(rigidbody.rotation, Quaternion.LookRotation(-direction), angularSpeed * Time.fixedDeltaTime);
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
                rigidbody = GetComponent<Rigidbody>();

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(rigidbody.position, avoidanceRadius);

            Color orange = (Color.yellow + Color.red) / 2;
            orange.a = 1;
            Gizmos.color = orange;
            Gizmos.DrawWireSphere(rigidbody.position, avoidanceRadius + avoidancePredictionRadius);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(rigidbody.position, rigidbody.position + rigidbody.velocity);

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(rigidbody.position, rigidbody.position - transform.forward);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(rigidbody.position, rigidbody.position + gizmosMovementDirection);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(rigidbody.position, rigidbody.position + gizmosAvoidDirection);
        }
#endif
    }
}