using System;

using UnityEngine;

namespace Assets.Enderlook.Unity.Pathfinding.Steerings
{
    /// <summary>
    /// An steering behaviour to evade a target.
    /// </summary>
    [Serializable]
    internal sealed class PursuitRigidbody : SteeringWithPrediction
    {
        [SerializeField, Tooltip("Rigidbody to pursuit.")]
        public Rigidbody TargetToPursuit;
        public PursuitRigidbody(Rigidbody targetToPursuit, float distanceToEnable, float timePrediction)
            : base(distanceToEnable, timePrediction) => TargetToPursuit = targetToPursuit;

        /// <inheritdoc cref="ISteering.GetDirection(Rigidbody)"/>
        public override Vector3 GetDirection(Rigidbody agent)
        {
            Vector3 targetPosition = TargetToPursuit.position + TargetToPursuit.velocity * TimePrediction;
            Vector3 agentPosition = agent.position + agent.velocity * TimePrediction;
            Vector3 direction = targetPosition - agentPosition;
            return LerpWithToDistanceToEnableAndNormalizeIfGreaterThanOneOrReturnZeroIfGreaterThanDistanceToEnable(direction);
        }
    }
}
