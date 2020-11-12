using System;

using UnityEngine;

namespace Assets.Enderlook.Unity.Pathfinding.Steerings
{
    /// <summary>
    /// An steering behaviour to seek a target.
    /// </summary>
    [Serializable]
    public class Seek : Steering
    {
        [SerializeField, Tooltip("Transform to seek.")]
        private Transform TargetToSeek;

        public Seek(Transform targetToSeek, float distanceToEnable)
            : base(distanceToEnable) => TargetToSeek = targetToSeek;

        /// <inheritdoc cref="ISteering.GetDirection(Rigidbody)"/>
        public override Vector3 GetDirection(Rigidbody agent)
        {
            Vector3 direction = TargetToSeek.position - agent.position;
            return LerpWithToDistanceToEnableAndNormalizeIfGreaterThanOneOrReturnZeroIfGreaterThanDistanceToEnable(direction);
        }
    }
}
