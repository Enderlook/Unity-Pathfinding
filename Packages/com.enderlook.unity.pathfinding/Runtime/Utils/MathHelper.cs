using System;
using System.Runtime.CompilerServices;

using Enderlook.Collections.Pooled.LowLevel; 
using Enderlook.Unity.Pathfinding.Utils;

using Debug = UnityEngine.Debug;
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

        public unsafe static int GetIndex<T>(RawPooledList<T> list, ref T current)
        {
            long currentPointer = ((IntPtr)Unsafe.AsPointer(ref current)).ToInt64();
            long startPointer = ((IntPtr)Unsafe.AsPointer(ref list[0])).ToInt64();
            int index = (int)((currentPointer - startPointer) / Unsafe.SizeOf<T>());
            Debug.Assert(list.Count > index);
            return index;
        }
    }
}