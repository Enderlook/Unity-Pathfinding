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

        [SerializeField, Tooltip("Determines how direction must be normalized.")]
        private DirectionNormalizationModes normalizationMode = DirectionNormalizationModes.WhenIsAboveOne;
        public DirectionNormalizationModes NormalizationMode
        {
            get => normalizationMode;
            set {
                if (value != DirectionNormalizationModes.Never
                    && value != DirectionNormalizationModes.WhenIsAboveOne
                    && value != DirectionNormalizationModes.Always)
                    ThrowHelper.ThrowArgumentException_ValueIsNotValidValueOfEnum();
                normalizationMode = value;
            }
        }

        [Header("Collision Prediction")]
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

        [SerializeField, Range(0, 1), ShowIf(nameof(predictionTime), 0, ComparisonMode.NotEqual), Tooltip("Determines the ratio strength of the current position (value 0) and the strength of predicted positions (value 1) of moving obstacles.")]
        private float predictionStrength = .5f;
        public float PredictionStrength {
            get => predictionStrength;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                predictionStrength = value;
            }
        }

        [Header("Direction Collision Check")]
        [SerializeField, Min(0), Tooltip("After calculating the new direction, check if the new direction would collide with an obstacle obstructing the path.")]
        private float directionCheckDistance = 2;
        public float DirectionCheckDistance {
            get => directionCheckDistance;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                directionCheckDistance = value;
            }
        }

        [SerializeField, Min(0), ShowIf(nameof(directionCheckDistance), 0, ComparisonMode.NotEqual), Tooltip("If an obstacle would block the avoidance direction, a new direction is looked around.\n" +
            "Determines each how much distance in a circumference formed by Direction Check Distance should obstacles be checked (by using a raycast).")]
        private float circumferenceCheckDistance = .25f;
        public float CircumferenceCheckDistance
        {
            get => circumferenceCheckDistance;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                circumferenceCheckDistance = value;
            }
        }

        [SerializeField, Range(0, 1), ShowIf(nameof(directionCheckDistance), 0, ComparisonMode.NotEqual), Tooltip("Determines the ratio strength of the current direction (value 0) and the strength of new direction (value 1) that evades collision with new direction.")]
        private float directionStrength = .5f;
        public float DirectionStrength
        {
            get => directionStrength;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                directionStrength = value;
            }
        }

        private new Rigidbody rigidbody;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void Awake() => rigidbody = GetComponent<Rigidbody>();

        /// <inheritdoc cref="ISteeringBehaviour.GetDirection()"/>
        Vector3 ISteeringBehaviour.GetDirection()
#if UNITY_EDITOR
            => GetDirection<Toggle.No>();
#else
            => GetDirection();
#endif

        private Vector3 GetDirection
#if UNITY_EDITOR
            <TGizmos>
#endif
            ()
        {
#if UNITY_EDITOR

            if (this.rigidbody == null)
                this.rigidbody = GetComponent<Rigidbody>();
#endif

            Rigidbody rigidbody = this.rigidbody;

            float radius = Radius;
            float overlapRadius = radius;
            float predictionTime = this.predictionTime;
            if (predictionTime != 0)
                overlapRadius += predictionRadius;

            if (overlapRadius == 0)
                return Vector3.zero;

            Vector3 currentPosition = rigidbody.position;

#if UNITY_EDITOR
            if (Toggle.IsToggled<TGizmos>())
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(currentPosition, radius);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentPosition, overlapRadius);
            }
#endif

            int amount = Physics.OverlapSphereNonAlloc(currentPosition, overlapRadius, colliders, Layers);
            if (amount == colliders.Length)
            {
                colliders = Physics.OverlapSphere(currentPosition, overlapRadius, Layers);
                amount = colliders.Length;
                Array.Resize(ref colliders, amount + 1);
            }

            if (amount == 0)
                return Vector3.zero;

            Transform transform = this.transform;
            Span<Collider> span = colliders.AsSpan(0, amount);
            float squaredRadius = radius * radius;

            float predictionStrength = this.predictionStrength;
            Vector3 result;
            if (predictionTime > 0 && predictionStrength > 0)
            {
                if (predictionStrength == 1)
                    result = AvoidObstacles<Toggle.Yes, Toggle.Yes>(span);
                else
                    result = AvoidObstacles<Toggle.Yes, Toggle.No>(span);
            }
            else
                result = AvoidObstacles<Toggle.No, Toggle.Yes>(span);

            if (result == Vector3.zero)
                return Vector3.zero;

#if UNITY_EDITOR
            if (Toggle.IsToggled<TGizmos>())
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(currentPosition, currentPosition + (result * 3));
            }
#endif

            if (directionCheckDistance > 0)
            {
                float directionCheckDistance = DirectionCheckDistance;

#if UNITY_EDITOR
                if (Toggle.IsToggled<TGizmos>())
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawRay(currentPosition, result.normalized * directionCheckDistance);
                }
#endif

                if (Physics.Raycast(currentPosition, result, out RaycastHit hitInfo, directionCheckDistance, Layers))
                    return AvoidImminentCollision(hitInfo.distance, directionCheckDistance);
            }

            return result;

            Vector3 AvoidObstacles<TPredict, TPredictionStrengthIsOne>(Span<Collider> span_)
            {
                Vector3 predicedPosition = Toggle.IsToggled<TPredict>() ? rigidbody.position + (rigidbody.velocity * predictionTime) : default;

#if UNITY_EDITOR
                if (Toggle.IsToggled<TGizmos>())
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(predicedPosition, currentPosition);
                }
#endif

                Vector3 total = Vector3.zero;
                int count = 0;
                foreach (Collider collider in span_)
                {
                    // Check if collider belongs to the same entity which has the obstacle avoidance.
                    Transform current = collider.transform;
                    do
                    {
                        if (current == transform)
                            goto next;
                        current = current.parent;
                    } while (current != null);

                    Vector3 obstaclePosition = collider.ClosestPointOnBounds(currentPosition);
                    Vector3 currentDifference = obstaclePosition - currentPosition;
                    float currentSqrMagnitude = currentDifference.sqrMagnitude;

                    if (Toggle.IsToggled<TPredict>())
                    {
                        // Try get the Rigidbody of the obstacle.
                        Rigidbody rigidbody_;
                        current = collider.transform;
                        do
                        {
                            if (current.TryGetComponent(out rigidbody_))
                                goto foundComponent;
                            current = current.parent;
                        } while (current != null);

                        // If no Rigidbody is found, then fallback to simple case.
                        goto simple;

                    foundComponent:
                        // Position of objects with rigidbody is calculated using two interpolated factors:
                        // 1) Direction of avoidance without taking into account velocity of objects.
                        // 2) Direction of avoidance taking into account velocity of objects.
                        // Then this values are interpolated using predictionStrength.
                        // Step 2) is only executed if the obstacle is moving and if predictionStrength > 0 (already checked in the previous method).
                        // Step 1) is only executed if predictionStrength != 1.

                        Vector3 velocity = rigidbody_.velocity;
                        if (velocity.x != 0 || velocity.y != 0 || velocity.z != 0)
                        {
                            Vector3 predictedObstaclePosition = obstaclePosition + (velocity * predictionTime);
                            Vector3 predictedDifference = predictedObstaclePosition - predicedPosition;

                            float sqrMagnitude = predictedDifference.sqrMagnitude;
                            if (sqrMagnitude < squaredRadius)
                            {
                                count++;

                                // 2)
                                Vector3 predictedValue = (radius - Mathf.Sqrt(sqrMagnitude)) / radius * predictedDifference;

#if UNITY_EDITOR
                                if (Toggle.IsToggled<TGizmos>())
                                {
                                    Gizmos.color = Color.yellow;
                                    Gizmos.DrawLine(predicedPosition, predictedObstaclePosition);

                                    Gizmos.color = Color.magenta;
                                    Gizmos.DrawRay(predicedPosition, predictedValue);
                                }
#endif

                                // Check predictionStrength != 1 because if this is true, Vector3.Lerp would ignore this, so no need to calculate it.
                                if (currentSqrMagnitude < squaredRadius && !Toggle.IsToggled<TPredictionStrengthIsOne>())
                                {
                                    // 1)
                                    Vector3 currentValue = (radius - Mathf.Sqrt(currentSqrMagnitude)) / radius * currentDifference;
                                    Vector3 value = Vector3.Lerp(currentValue, predictedValue, (float)predictionStrength);
                                    total -= value;

#if UNITY_EDITOR
                                    if (Toggle.IsToggled<TGizmos>())
                                    {
                                        Gizmos.color = Color.red;
                                        Gizmos.DrawLine(currentPosition, obstaclePosition);

                                        Gizmos.color = new Color(1, .5f, 0);
                                        Gizmos.DrawRay(currentPosition, value);
                                    }
#endif
                                }
                                else
                                {
                                    total -= predictedValue;

#if UNITY_EDITOR
                                    if (Toggle.IsToggled<TGizmos>())
                                    {
                                        Gizmos.color = new Color(1, .5f, 0);
                                        Gizmos.DrawRay(currentPosition, predictedValue);
                                    }
#endif
                                }

                                goto next;
                            }
                        }
                    }

                simple:
                    if (currentSqrMagnitude < squaredRadius)
                    {
                        count++;
                        // 1)
                        Vector3 value = (radius - Mathf.Sqrt(currentSqrMagnitude)) / radius * currentDifference;
                        total -= value;

#if UNITY_EDITOR
                        if (Toggle.IsToggled<TGizmos>())
                        {
                            Gizmos.color = Color.red;
                            Gizmos.DrawLine(currentPosition, obstaclePosition);

                            Gizmos.color = new Color(1, .5f, 0);
                            Gizmos.DrawRay(currentPosition, value);
                        }
#endif
                    }

                next:;
                }

                if (count == 0)
                    return Vector3.zero;

                total /= count;

                switch (normalizationMode)
                {
                    case DirectionNormalizationModes.WhenIsAboveOne:
                        if (total.sqrMagnitude > 1)
                            total = total.normalized;
                        break;
                    case DirectionNormalizationModes.Always:
                        total = total.normalized;
                        break;
                }

                return total;
            }

            Vector3 AvoidImminentCollision(float distance, float directionCheckDistance)
            {
                Vector3 normalizedResult = result.normalized;
                Vector3 newResult = result;

                int steps = Mathf.Max(Mathf.RoundToInt(directionCheckDistance * 2 * Mathf.PI / CircumferenceCheckDistance), 2);
                float stepSize = 360f / steps;
                for (int i = 1; i < steps; i++)
                {
                    Vector3 direction = Quaternion.AngleAxis(stepSize * i, Vector3.up) * normalizedResult;

#if UNITY_EDITOR
                    if (Toggle.IsToggled<TGizmos>())
                    {
                        Gizmos.color = Color.blue;
                        Gizmos.DrawRay(currentPosition, direction * directionCheckDistance);
                    }
#endif

                    if (Physics.Raycast(currentPosition, direction, out RaycastHit hitInfo, directionCheckDistance, Layers))
                    {
                        if (hitInfo.distance < distance)
                        {
                            distance = hitInfo.distance;
                            newResult = direction;
                        }
                    }
                    else
                    {
                        distance = hitInfo.distance;
                        newResult = direction;
                        break;
                    }

                    direction = Quaternion.AngleAxis(-stepSize * i, Vector3.up) * normalizedResult;

#if UNITY_EDITOR
                    if (Toggle.IsToggled<TGizmos>())
                    {
                        Gizmos.color = Color.blue;
                        Gizmos.DrawRay(currentPosition, direction * directionCheckDistance);
                    }
#endif

                    if (Physics.Raycast(currentPosition, direction, out hitInfo, directionCheckDistance, Layers))
                    {
                        if (hitInfo.distance < distance)
                        {
                            distance = hitInfo.distance;
                            newResult = direction;
                        }
                    }
                    else
                    {
                        distance = hitInfo.distance;
                        newResult = direction;
                        break;
                    }
                }

#if UNITY_EDITOR
                if (Toggle.IsToggled<TGizmos>())
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawRay(currentPosition, newResult.normalized * directionCheckDistance);
                }
#endif

                return Vector3.Lerp(result, newResult.normalized * result.magnitude, DirectionStrength);
            }
        }

#if UNITY_EDITOR
        void ISteeringBehaviour.DrawGizmos() => GetDirection<Toggle.Yes>();
#endif

        public enum DirectionNormalizationModes
        {
            [Tooltip("Never normalize direction.")]
            Never,

            [Tooltip("Normalize direction when its magnitude when is above 1.")]
            WhenIsAboveOne,

            [Tooltip("Always normalize direction.")]
            Always,
        }
    }
}
