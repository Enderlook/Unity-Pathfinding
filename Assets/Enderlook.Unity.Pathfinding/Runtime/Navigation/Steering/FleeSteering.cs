using System;

using UnityEngine;

namespace Assets.Enderlook.Unity.Pathfinding.Steerings
{
    /// <summary>
    /// An steering behaviour to flee from a target.
    /// </summary>
    [Serializable]
    public sealed class FleeSteering : Steering
    {
        [SerializeField, Tooltip("Transform to flee from.")]
        private Transform TargetToFleeFrom;

        public FleeSteering(Transform targetToFleeFrom, float distanceFromTarget)
            : base(distanceFromTarget) => TargetToFleeFrom = targetToFleeFrom;

        /// <inheritdoc cref="ISteering.GetDirection(Rigidbody)"/>
        public override Vector3 GetDirection(Rigidbody agent)
        {
            Vector3 direction = agent.position - TargetToFleeFrom.position;
            return LerpWithToDistanceToEnableAndNormalizeIfGreaterThanOneOrReturnZeroIfGreaterThanDistanceToEnable(direction);
        }
    }
}
