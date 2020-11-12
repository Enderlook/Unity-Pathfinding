﻿using Enderlook.Unity.Attributes;

using System;

using UnityEngine;

namespace Assets.Enderlook.Unity.Pathfinding.Steerings
{
    /// <summary>
    /// An steering behaviour to avoid multiple obstacles.
    /// </summary>
    [Serializable]
    public struct ObstacleAvoidance : ISteering
    {
        private static Collider[] colliders = new Collider[100];
        private static int COLLIDER_GROW_FACTOR = 2;

        [SerializeField, Tooltip("Determines which layers does the agent tries to avoid.")]
        public LayerMask avoidanceLayers;

        [SerializeField, Min(0), Tooltip("Determines the radius in which tries to avoid obstacles.")]
        private float avoidanceRadius;
        public float AvoidanceRadius {
            get => avoidanceRadius;
            set => avoidanceRadius = ErrorMessage.NoNegativeGuard(nameof(AvoidanceRadius), value);
        }

        [SerializeField, Min(0), Tooltip("Determines the prediction time used for moving obstacles to avoid their futures positions.")]
        private float avoidancePredictionTime;
        public float AvoidancePredictionTime {
            get => avoidancePredictionTime;
            set => avoidancePredictionTime = ErrorMessage.NoNegativeGuard(nameof(AvoidancePredictionTime), value);
        }

        [SerializeField, Min(0), ShowIf(nameof(avoidancePredictionTime), typeof(float), 0, mustBeEqual: false), Tooltip("Determines additional detection radius used for moving obstacles.")]
        private float avoidancePredictionRadius;
        public float AvoidancePredictionRadius {
            get => avoidancePredictionRadius;
            set => avoidancePredictionRadius = ErrorMessage.NoNegativeGuard(nameof(AvoidancePredictionRadius), value);
        }

        [SerializeField, Min(0), Tooltip("Determines the avoidance strength to repel from obstacles.")]
        private float avoidanceStrength;
        public float AvoidanceStrength {
            get => avoidanceStrength;
            set => avoidanceStrength = ErrorMessage.NoNegativeGuard(nameof(AvoidanceStrength), value);
        }

        Vector3 ISteering.GetDirection(Rigidbody agent) => GetDirection(agent);

        internal Vector3 GetDirection(Rigidbody agent)
        {
            Transform transform = agent.transform;

            float radius = avoidanceRadius;
            if (avoidancePredictionTime != 0)
                radius += avoidancePredictionRadius;

            start:
            int amount = Physics.OverlapSphereNonAlloc(agent.position, radius, colliders, avoidanceLayers);
            if (amount == colliders.Length)
            {
                Array.Resize(ref colliders, colliders.Length * COLLIDER_GROW_FACTOR);
                goto start;
            }

            if (amount == 0)
                return Vector3.zero;

            Span<Collider> span = colliders.AsSpan(0, amount);
            Vector3 currentPosition = agent.position;
            Vector3 total = Vector3.zero;
            int count = 0;
            foreach (Collider collider in span)
            {
                if (IsMine(collider.transform))
                    continue;

                Vector3 position;
                Vector3 closestPoint = collider.ClosestPointOnBounds(agent.position);
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

            return total * avoidanceStrength;

            bool IsMine(Transform otherTransform)
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
        }
    }
}