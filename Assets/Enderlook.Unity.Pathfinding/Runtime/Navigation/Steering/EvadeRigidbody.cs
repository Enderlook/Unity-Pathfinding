using System;

using UnityEngine;

namespace Assets.Enderlook.Unity.Pathfinding.Steerings
{
    /// <summary>
    /// An steering behaviour to evade a target.
    /// </summary>
    [Serializable]
    internal sealed class EvadeRigidbody : SteeringWithPrediction
    {
        [SerializeField, Tooltip("Rigidbody to evade.")]
        public Rigidbody TargetToEvade;

        public EvadeRigidbody(Rigidbody targetToEvade, float distanceToEnable, float timePrediction)
            : base(distanceToEnable, timePrediction) => TargetToEvade = targetToEvade;

        /// <inheritdoc cref="ISteering.GetDirection(Rigidbody)"/>
        public override Vector3 GetDirection(Rigidbody agent)
        {
            Vector3 targetPosition = TargetToEvade.position + TargetToEvade.velocity * TimePrediction;
            Vector3 agentPosition = agent.position + agent.velocity * TimePrediction;
            Vector3 direction = agentPosition - targetPosition;
            return LerpWithToDistanceToEnableAndNormalizeIfGreaterThanOneOrReturnZeroIfGreaterThanDistanceToEnable(direction);
        }
    }
}
