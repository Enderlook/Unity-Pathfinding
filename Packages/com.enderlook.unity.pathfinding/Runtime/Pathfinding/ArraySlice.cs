using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Enderlook.Unity.Pathfinding2
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
        public ArraySlice(T[] array, int count)
        {
            Array = array;
            Length = count;
            Debug.Assert(array.Length >= count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Span<T>(ArraySlice<T> source)
            => source.Array.AsSpan(0, source.Length);
    }

    /// <summary>
    /// Represent an sliced array.
    /// </summary>
    /// <typeparam name="T">Type of array elements.</typeparam>
    internal readonly struct ReadOnlyArraySlice<T>
    {
        public readonly T[] Array;
        public readonly int Length;

        public ref readonly T this[int index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                Debug.Assert(index < Length);
                return ref Array[index];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyArraySlice(T[] array, int count)
        {
            Array = array;
            Length = count;
            Debug.Assert(array.Length >= count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlySpan<T>(ReadOnlyArraySlice<T> source)
            => source.Array.AsSpan(0, source.Length);
    }
}