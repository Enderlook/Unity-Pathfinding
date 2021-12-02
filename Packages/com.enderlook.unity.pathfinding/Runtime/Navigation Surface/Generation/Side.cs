using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Generation
{
    internal struct Side
    {
        public struct Left { }

        public struct Forward { }

        public struct Right { }

        public struct Backward { }

        [System.Diagnostics.Conditional("UNITY_ASSERTIONS"), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugAssert<T>()
            => Debug.Assert(
                typeof(T) == typeof(Left) ||
                typeof(T) == typeof(Forward) ||
                typeof(T) == typeof(Right) ||
                typeof(T) == typeof(Backward));
    }
}