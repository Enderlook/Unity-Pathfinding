using System;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Assets.Enderlook.Unity.Pathfinding.Steerings
{
    [Serializable]
    public abstract class Steering : ISteering
    {
        [SerializeField, Tooltip("At which distance from the target(s) will the steering enable.")]
        private float distanceToEnable;
        public float DistanceToEnable {
            get => distanceToEnable;
            set => distanceToEnable = ErrorMessage.NoNegativeGuard(nameof(DistanceToEnable), value);
        }

        protected Steering(float distanceToEnable) => DistanceToEnable = distanceToEnable;

        /// <inheritdoc cref="ISteering.GetDirection(Rigidbody)"/>
        public abstract Vector3 GetDirection(Rigidbody agent);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Vector3 LerpWithToDistanceToEnableAndNormalizeIfGreaterThanOneOrReturnZeroIfGreaterThanDistanceToEnable(Vector3 vector)
        {
            float distance = vector.magnitude;
            return LerpWithToDistanceToEnableAndNormalizeIfGreaterThanOneOrReturnZeroIfGreaterThanDistanceToEnable(vector, distance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Vector3 LerpWithToDistanceToEnableAndNormalizeIfGreaterThanOneOrReturnZeroIfGreaterThanDistanceToEnable(Vector3 vector, float distance)
        {
            if (distance > DistanceToEnable)
                return Vector3.zero;
            return NormalizeIfGreaterThanOne(LerpWithDistance(vector, distance));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Vector3 LerpWithToDistanceToEnableAndNormalizeIfGreaterThanOne(Vector3 vector, float distance)
            => NormalizeIfGreaterThanOne(LerpWithDistance(vector, distance));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Vector3 LerpWithDistance(Vector3 vector, float distance) => (DistanceToEnable - distance) / DistanceToEnable * vector;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Vector3 NormalizeIfGreaterThanOne(Vector3 vector)
        {
            // If vector is low we don't normalize to reflect that it's not very important
            // otherwise we normalize
            if (vector.magnitude > 1)
                return vector.normalized;
            return vector;
        }
    }
}
