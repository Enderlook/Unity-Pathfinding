using System;

using UnityEngine;

namespace Assets.Enderlook.Unity.Pathfinding.Steerings
{
    /// <summary>
    /// An steering behaviour to evade a target.
    /// </summary>
    [Serializable]
    internal sealed class EvadeTransform : SteeringWithPrediction
    {
        [SerializeField, Tooltip("Transform to evade.")]
        public Transform TargetToEvade;

        public EvadeTransform(Transform targetToEvade, float distanceToEnable, float timePrediction)
            : base(distanceToEnable, timePrediction) => TargetToEvade = targetToEvade;

        /// <inheritdoc cref="ISteering.GetDirection(Rigidbody)"/>
        public override Vector3 GetDirection(Rigidbody agent)
        {
            Vector3 agentPosition = agent.position + agent.velocity * TimePrediction;
            Vector3 direction = agentPosition - TargetToEvade.position;
            return LerpWithToDistanceToEnableAndNormalizeIfGreaterThanOneOrReturnZeroIfGreaterThanDistanceToEnable(direction);
        }
    }
}
