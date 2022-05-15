using System;

namespace Enderlook.Unity.Pathfinding.Utils
{
    internal static class ThrowHelper
    {
        public static void ThrowArgumentOutOfRangeException_ValueMustBeGreaterThanZero()
            => throw new ArgumentOutOfRangeException("value", "Must be positive.");

        public static void ThrowArgumentOutOfRangeException_ValueCannotBeNegative()
            => throw new ArgumentOutOfRangeException("value", "Can't be negative.");

        public static void ThrowArgumentNullException_SteeringBehaviour()
            => throw new ArgumentNullException("steeringBehaviour");
    }
}