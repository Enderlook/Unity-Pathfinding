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
            float overlapRadius = radius;
            if (predictionTime != 0)
                overlapRadius += predictionRadius;

            int amount = Physics.OverlapSphereNonAlloc(rigidbody.position, overlapRadius, colliders, Layers);
            if (amount == colliders.Length)
            {
                colliders = Physics.OverlapSphere(rigidbody.position, overlapRadius, Layers);
                amount = colliders.Length;
                Array.Resize(ref colliders, amount + 1);
            }

            if (amount == 0)
                return Vector3.zero;

            Span<Collider> span = colliders.AsSpan(0, amount);
            Vector3 currentPosition = rigidbody.position;
            Vector3 predicedPosition = rigidbody.position + (rigidbody.velocity * predictionTime);
            Vector3 total = Vector3.zero;
            float squaredRadius = radius * radius;
            int count = 0;
            foreach (Collider collider in span)
            {
                // Check if collider belongs to the same entity which has the obstacle avoidance.
                Transform current = collider.transform;
                do
                {
                    if (current == transform)
                        continue;
                    current = current.parent;
                } while (current != null);

                Vector3 difference;
                Vector3 position = collider.ClosestPointOnBounds(rigidbody.position);
                current = collider.transform;
                do
                {
                    if (current.TryGetComponent(out Rigidbody rigidbody_))
                    {
                        position += rigidbody_.velocity * predictionTime;
                        difference = position - predicedPosition;
                        goto outside;
                    }
                    current = current.parent;
                } while (current != null);
                difference = position - currentPosition;
            outside:;
                float sqrMagnitude = difference.sqrMagnitude;
                if (sqrMagnitude >= squaredRadius)
                    continue;

                count++;
                total -= (radius - Mathf.Sqrt(sqrMagnitude)) / radius * difference;
            }

            if (count == 0)
                return Vector3.zero;

            total /= count;

            if (total.sqrMagnitude > 1)
                total = total.normalized;

            return total;
        }

#if UNITY_EDITOR
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnDrawGizmosSelected()
        {
            if (rigidbody == null)
                rigidbody = GetComponent<Rigidbody>();

            float radius = Radius;
            float overlapRadius = radius;
            if (predictionTime != 0)
                overlapRadius += predictionRadius;

            int amount = Physics.OverlapSphereNonAlloc(rigidbody.position, overlapRadius, colliders, Layers);
            if (amount == colliders.Length)
            {
                colliders = Physics.OverlapSphere(rigidbody.position, overlapRadius, Layers);
                amount = colliders.Length;
                Array.Resize(ref colliders, amount + 1);
            }

            if (amount == 0)
                return;

            Gizmos.color = Color.yellow;
            Span<Collider> span = colliders.AsSpan(0, amount);
            Vector3 currentPosition = rigidbody.position;
            Vector3 predicedPosition = rigidbody.position + (rigidbody.velocity * predictionTime);
            float squaredRadius = radius * radius;
            foreach (Collider collider in span)
            {
                // Check if collider belongs to the same entity which has the obstacle avoidance.
                Transform current = collider.transform;
                do
                {
                    if (current == transform)
                        continue;
                    current = current.parent;
                } while (current != null);

                Vector3 difference;
                Vector3 closestPoint = collider.ClosestPointOnBounds(rigidbody.position);
                Vector3 position = closestPoint;
                current = collider.transform;
                do
                {
                    if (current.TryGetComponent(out Rigidbody rigidbody_))
                    {
                        position += rigidbody_.velocity * predictionTime;
                        difference = position - predicedPosition;
                        goto outside;
                    }
                    current = current.parent;
                } while (current != null);
                difference = position - currentPosition;
            outside:;
                float sqrMagnitude = difference.sqrMagnitude;
                if (sqrMagnitude >= squaredRadius)
                    continue;

                Gizmos.DrawLine(currentPosition, position);
                if (closestPoint != currentPosition)
                {
                    Gizmos.DrawLine(position, closestPoint);
                    Gizmos.DrawLine(position, predicedPosition);
                }
            }
        }
#endif
    }
}
