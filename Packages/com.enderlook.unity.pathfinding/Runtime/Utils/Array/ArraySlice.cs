using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Enderlook.Unity.Pathfinding.Utils
{
    /// <summary>
    /// Represent an sliced array.
    /// </summary>
    /// <typeparam name="T">Type of array elements.</typeparam>
    internal readonly struct ArraySlice<T>
    {
        public readonly T[] Array;
        public readonly int Length;

        public ref T this[int index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                Debug.Assert(index < Length);
                return ref Array[index];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySlice(T[] array, int length)
        {
            Array = array;
            Length = length;
            Debug.Assert(array.Length >= length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Span<T>(ArraySlice<T> source)
            => source.Array.AsSpan(0, source.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ArraySlice<T>(T[] source)
            => new ArraySlice<T>(source, source.Length);
    }
}
