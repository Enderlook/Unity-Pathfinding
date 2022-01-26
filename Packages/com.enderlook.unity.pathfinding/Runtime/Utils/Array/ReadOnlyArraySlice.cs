using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Enderlook.Unity.Pathfinding.Utils
{
    /// <summary>
    /// Represent an sliced array.
    /// </summary>
    /// <typeparam name="T">Type of array elements.</typeparam>
    internal readonly struct ReadOnlyArraySlice<T>
    {
        private readonly T[] Array;
        public readonly int Length;

        public ref readonly T this[int index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                Debug.Assert(index < Length);
                return ref Array[index];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyArraySlice(T[] array, int length)
        {
            Array = array;
            Length = length;
            Debug.Assert(array.Length >= length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlySpan<T>(ReadOnlyArraySlice<T> source)
            => source.Array.AsSpan(0, source.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlyArraySlice<T>(ArraySlice<T> source)
            => new ReadOnlyArraySlice<T>(source.Array, source.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlyArraySlice<T>(T[] source)
            => new ReadOnlyArraySlice<T>(source, source.Length);
    }
}
