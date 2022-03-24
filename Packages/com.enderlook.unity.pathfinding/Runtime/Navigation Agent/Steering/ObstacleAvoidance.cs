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

        [Header("Prediction")]
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

        [SerializeField, Min(0), ShowIf(nameof(predictionTime), 0, ComparisonMode.NotEqual), Tooltip("Determines the strengh multiplied of predicted position of moving obstacles.")]
        private float predictionStrength = 1;

        public float PredictionStrength
        {
            get => predictionStrength;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                predictionStrength = value;
            }
        }

        private new Rigidbody rigidbody;
        private float clockwiseRotationCheck;

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

            float fixedTime = Time.fixedTime;
            if (fixedTime - Mathf.Abs(clockwiseRotationCheck) > 1)
            {
                // Determines if rotates clockwise or counterclockwise.
                fixedTime *= UnityEngine.Random.value > .5 ? 1 : -1;
                clockwiseRotationCheck = fixedTime;
            }

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
                        goto next;
                    current = current.parent;
                } while (current != null);

                Vector3 obstaclePosition = collider.ClosestPointOnBounds(rigidbody.position);
                current = collider.transform;
                do
                {
                    if (current.TryGetComponent(out Rigidbody rigidbody_))
                    {
                        // Position of objects with rigidbody is calculated multiple times:
                        // 1) Direction of avoidance without taking into account velocity of objects.
                        // 2) Direction of avoidance taking into account velocity of objects.
                        // 3) Direction of avoidance taking into account velocity of objects and rotating 90 degrees clockwise,
                        // this is required in order to avoid obstacles that move towards the creature and
                        // the predicted position is after the creature.
                        // Step 2) and 3) are only executed if the obstacle is moving.

                        Vector3 currentDifference = obstaclePosition - currentPosition;
                        float currentSqrMagnitude = currentDifference.sqrMagnitude;
                        if (currentSqrMagnitude < squaredRadius)
                        {
                            // 1)
                            count++;
                            total -= (radius - Mathf.Sqrt(currentSqrMagnitude)) / radius * currentDifference;
                        }

                        Vector3 velocity = rigidbody_.velocity;
                        if (velocity.x == 0 && velocity.y == 0 && velocity.z == 0)
                            goto next;

                        Vector3 predictedObstaclePosition = obstaclePosition + (velocity * predictionTime);
                        Vector3 predictedDifference = predictedObstaclePosition - predicedPosition;

                        float sqrMagnitude = predictedDifference.sqrMagnitude;
                        if (sqrMagnitude < squaredRadius)
                        {
                            count += 2;
                            float a = (radius - Mathf.Sqrt(sqrMagnitude)) / radius * predictionStrength;

                            // 2)
                            total -= a * predictedDifference;

                            // 3)
                            Vector3 rotated;
                            if (fixedTime > 0)
                                rotated = new Vector3(predictedDifference.z, predictedDifference.y, -predictedDifference.x);
                            else
                                rotated = new Vector3(-predictedDifference.z, predictedDifference.y, predictedDifference.x);
                            total -= a * rotated;
                        }
                        goto next;
                    }
                    current = current.parent;
                } while (current != null);

                {
                    Vector3 difference = obstaclePosition - currentPosition;
                    float sqrMagnitude = difference.sqrMagnitude;
                    if (sqrMagnitude < squaredRadius)
                    {
                        count++;
                        total -= (radius - Mathf.Sqrt(sqrMagnitude)) / radius * difference;
                    }
                }

            next:;
            }

            if (count == 0)
                return Vector3.zero;

            total /= count;

            if (total.sqrMagnitude > 1)
                total = total.normalized;

            return total;
        }

#if UNITY_EDITOR
        void ISteeringBehaviour.DrawGizmos()
        {
            if (rigidbody == null)
                rigidbody = GetComponent<Rigidbody>();

            float radius = Radius;
            float overlapRadius = radius;
            if (predictionTime != 0)
                overlapRadius += predictionRadius;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(rigidbody.position, overlapRadius);
            Gizmos.DrawWireSphere(rigidbody.position, radius);

            int amount = Physics.OverlapSphereNonAlloc(rigidbody.position, overlapRadius, colliders, Layers);
            if (amount == colliders.Length)
            {
                colliders = Physics.OverlapSphere(rigidbody.position, overlapRadius, Layers);
                amount = colliders.Length;
                Array.Resize(ref colliders, amount + 1);
            }

            if (amount == 0)
                return;

            float fixedTime = Time.fixedTime;
            if (fixedTime - Mathf.Abs(clockwiseRotationCheck) > 1)
            {
                // Determines if rotates clockwise or counterclockwise.
                fixedTime *= UnityEngine.Random.value > .5 ? 1 : -1;
                clockwiseRotationCheck = fixedTime;
            }

            Span<Collider> span = colliders.AsSpan(0, amount);
            Vector3 currentPosition = rigidbody.position;
            Vector3 predicedPosition = rigidbody.position + (rigidbody.velocity * predictionTime);
            Vector3 total = Vector3.zero;
            float squaredRadius = radius * radius;
            int count = 0;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(predicedPosition, currentPosition);
            foreach (Collider collider in span)
            {
                // Check if collider belongs to the same entity which has the obstacle avoidance.
                Transform current = collider.transform;
                do
                {
                    if (current == transform)
                        goto next;
                    current = current.parent;
                } while (current != null);

                Vector3 obstaclePosition = collider.ClosestPointOnBounds(rigidbody.position);
                current = collider.transform;
                do
                {
                    if (current.TryGetComponent(out Rigidbody rigidbody_))
                    {
                        Vector3 currentDifference = obstaclePosition - currentPosition;
                        float currentSqrMagnitude = currentDifference.sqrMagnitude;
                        if (currentSqrMagnitude < squaredRadius)
                        {
                            count++;
                            total -= (radius - Mathf.Sqrt(currentSqrMagnitude)) / radius * currentDifference;

                            Gizmos.color = Color.red;
                            Gizmos.DrawLine(obstaclePosition, currentPosition);
                        }

                        Vector3 velocity = rigidbody_.velocity;
                        if (velocity.x == 0 && velocity.y == 0 && velocity.z == 0)
                            goto next;

                        Vector3 predictedObstaclePosition = obstaclePosition + (velocity * predictionTime);
                        Vector3 predictedDifference = predictedObstaclePosition - predicedPosition;

                        float sqrMagnitude = predictedDifference.sqrMagnitude;
                        if (sqrMagnitude < squaredRadius)
                        {
                            count += 2;
                            float a = (radius - Mathf.Sqrt(sqrMagnitude)) / radius * predictionStrength;

                            // 2)
                            total -= a * predictedDifference;

                            Gizmos.color = Color.yellow;
                            Gizmos.DrawLine(predictedObstaclePosition, predicedPosition);

                            Gizmos.color = Color.cyan;
                            Gizmos.DrawRay(predicedPosition, a * predictedDifference);

                            // 3)
                            Vector3 rotated;
                            if (fixedTime > 0)
                                rotated = new Vector3(predictedDifference.z, predictedDifference.y, -predictedDifference.x);
                            else
                                rotated = new Vector3(-predictedDifference.z, predictedDifference.y, predictedDifference.x);
                            total -= a * rotated;

                            Gizmos.color = Color.blue;
                            Gizmos.DrawRay(predicedPosition, a * rotated);
                        }
                        goto next;
                    }
                    current = current.parent;
                } while (current != null);

                {
                    Vector3 difference = obstaclePosition - currentPosition;
                    float sqrMagnitude = difference.sqrMagnitude;
                    if (sqrMagnitude < squaredRadius)
                    {
                        count++;
                        total -= (radius - Mathf.Sqrt(sqrMagnitude)) / radius * difference;

                        Gizmos.color = Color.red;
                        Gizmos.DrawLine(obstaclePosition, currentPosition);
                    }
                }

            next:;
            }

            total /= count;

            if (total.sqrMagnitude > 1)
                total = total.normalized;

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(currentPosition, currentPosition + (total * 3));
        }
#endif
    }
}
