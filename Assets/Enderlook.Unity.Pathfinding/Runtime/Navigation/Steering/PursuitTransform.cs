using System;

using UnityEngine;

namespace Assets.Enderlook.Unity.Pathfinding.Steerings
{
    /// <summary>
    /// An steering behaviour to evade a target.
    /// </summary>
    [Serializable]
    internal sealed class PursuitTransform : SteeringWithPrediction
    {
        [SerializeField, Tooltip("Transform to pursuit.")]
        public Transform TargetToPursuit;

        public PursuitTransform(Transform targetToPursuit, float distanceToEnable, float timePrediction)
            : base(distanceToEnable, timePrediction) => TargetToPursuit = targetToPursuit;

        /// <inheritdoc cref="ISteering.GetDirection(Rigidbody)"/>
        public override Vector3 GetDirection(Rigidbody agent)
        {
            Vector3 agentPosition = agent.position + agent.velocity * TimePrediction;
            Vector3 direction = TargetToPursuit.position - agentPosition;
            return LerpWithToDistanceToEnableAndNormalizeIfGreaterThanOneOrReturnZeroIfGreaterThanDistanceToEnable(direction);
        }
    }
}
