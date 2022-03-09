using Enderlook.Unity.Pathfinding.Utils;
using Enderlook.Unity.Toolset.Attributes;

using System;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Steerings
{
    /// <summary>
    /// An steering behaviour to avoid multiple obstacles.
    /// </summary>
    [AddComponentMenu("Enderlook/Pathfinding/Obstacle Avoidance"), RequireComponent(typeof(Rigidbody))]
    public sealed class ObstacleAvoidance : MonoBehaviour, ISteeringBehaviour
    {
        // We take advantage of Unity single threading to temporarily store in the same array the closest entites to the requested and so reduce allocations.
        private static Collider[] colliders = new Collider[1];

        [SerializeField, Tooltip("Determines which layers does the agent tries to avoid.")]
        private LayerMask layers;
        public LayerMask Layers {
            get => layers;
            set => layers = value;
        }

        [SerializeField, Min(0), Tooltip("Determines the radius in which tries to avoid obstacles.")]
        private float radius;
        public float Radius {
            get => radius;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                radius = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the prediction time used for moving obstacles to avoid their futures positions.")]
        private float predictionTime;
        public float PredictionTime
        {
            get => predictionTime;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                predictionTime = value;
            }
        }

        [SerializeField, Min(0), ShowIf(nameof(predictionTime), 0, ComparisonMode.NotEqual), Tooltip("Determines additional detection radius used for moving obstacles.")]
        private float predictionRadius;
        public float PredictionRadius {
            get => predictionRadius;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                predictionRadius = value;
            }
        }

        private new Rigidbody rigidbody;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void Awake() => rigidbody = GetComponent<Rigidbody>();

        /// <inheritdoc cref="ISteeringBehaviour.GetDirection()"/>
        Vector3 ISteeringBehaviour.GetDirection()
        {
#if UNITY_EDITOR
            if (rigidbody == null)
                rigidbody = GetComponent<Rigidbody>();
#endif

            float radius = Radius;
            if (predictionTime != 0)
                radius += predictionRadius;

            int amount = Physics.OverlapSphereNonAlloc(rigidbody.position, radius, colliders, Layers);
            if (amount == colliders.Length)
            {
                colliders = Physics.OverlapSphere(rigidbody.position, radius, Layers);
                amount = colliders.Length;
                Array.Resize(ref colliders, amount + 1);
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
                    position = closestPoint + (_rigidbody.velocity * predictionTime);
                else
                    position = closestPoint;

                Vector3 direction = position - currentPosition;
                float distance = direction.magnitude;
                if (distance >= Radius)
                    continue;

                count++;
                total -= (Radius - distance) / Radius * direction;
            }

            if (count == 0)
                return Vector3.zero;

            total /= count;

            if (total.sqrMagnitude > 1)
                total = total.normalized;

            return total;
        }

        private bool IsMine(Transform otherTransform)
        {
            if (otherTransform == transform)
                return true;

            otherTransform = otherTransform.parent;
            while (otherTransform != null)
            {
                if (otherTransform == transform)
                    return true;
                otherTransform = otherTransform.parent;
            }

            return false;
        }

#if UNITY_EDITOR
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnDrawGizmosSelected()
        {
            if (rigidbody == null)
                rigidbody = GetComponent<Rigidbody>();

            float radius = Radius;
            if (predictionTime != 0)
                radius += predictionRadius;

            int amount = Physics.OverlapSphereNonAlloc(rigidbody.position, radius, colliders, Layers);
            if (amount == colliders.Length)
            {
                colliders = Physics.OverlapSphere(rigidbody.position, radius, Layers);
                amount = colliders.Length;
                Array.Resize(ref colliders, amount + 1);
            }

            if (amount == 0)
                return;

            Vector3 transformPosition = transform.position;
            Gizmos.color = Color.yellow;
            Span<Collider> span = colliders.AsSpan(0, amount);
            Vector3 currentPosition = rigidbody.position;
            foreach (Collider collider in span)
            {
                if (IsMine(collider.transform))
                    continue;

                Vector3 position;
                Vector3 closestPoint = collider.ClosestPointOnBounds(rigidbody.position);
                if (collider.TryGetComponent(out Rigidbody _rigidbody))
                    position = closestPoint + (_rigidbody.velocity * predictionTime);
                else
                    position = closestPoint;

                Vector3 direction = position - currentPosition;
                float distance = direction.magnitude;
                if (distance >= Radius)
                    continue;

                Gizmos.DrawLine(transformPosition, position);
                if (position != closestPoint)
                    Gizmos.DrawLine(position, closestPoint);
            }
        }
#endif
    }
}
