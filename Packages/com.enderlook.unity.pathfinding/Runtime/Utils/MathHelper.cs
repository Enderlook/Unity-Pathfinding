using System.Runtime.CompilerServices;

using UVector3 = UnityEngine.Vector3;
using NVector3 = System.Numerics.Vector3;

namespace Enderlook.Unity.Pathfinding.Generation
{
    internal static class MathHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NVector3 ToNumerics(this UVector3 value) => new NVector3(value.x, value.y, value.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UVector3 ToUnity(this NVector3 value) => new UVector3(value.X, value.Y, value.Z);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public unsafe static int IndexesTo<T>(ref T start, ref T end)
            => (int)(Unsafe.ByteOffset(ref start, ref end).ToInt64() / Unsafe.SizeOf<T>());
    }
}