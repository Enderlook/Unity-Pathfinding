using System;

namespace Assets.Enderlook.Unity.Pathfinding
{
    internal static class ErrorMessage
    {
        private const string CAN_NOT_BE_NEGATIVE = "Can't be negative.";

        public static float NoNegativeGuard(string name, float value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(name, CAN_NOT_BE_NEGATIVE);
            return value;
        }
    }
}
