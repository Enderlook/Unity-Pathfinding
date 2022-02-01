using Enderlook.Collections.Pooled.LowLevel;

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Enderlook.Unity.Pathfinding.Utils
{
    /// <summary>
    /// Represent an sliced array.
    /// </summary>
    /// <typeparam name="T">Type of array elements.</typeparam>
#if UNITY_ASSERTIONS
    internal struct ArraySlice<T>
#else
    internal readonly struct ArraySlice<T>
#endif
    {
#if UNITY_ASSERTIONS
        private T[] array;
        private int length;

        public T[] Array
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(!(array is null));
                return array;
            }
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(!(array is null));
                return length;
            }
        }
#else
        public readonly T[] Array;
        public readonly int Length;
#endif

        public ref T this[int index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                Debug.Assert(index < Length);
                return ref Array[index];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ArraySlice(T[] array, int length)
        {
#if UNITY_ASSERTIONS
            this.array = array;
            this.length = length;
#else
            Array = array;
            Length = length;
#endif
            Debug.Assert(array.Length >= length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySlice(int length, bool clear)
        {
#if UNITY_ASSERTIONS
            this.length = length;
            array = ArrayPool<T>.Shared.Rent(length);
#else
            Length = length;
            Array = ArrayPool<T>.Shared.Rent(length);
#endif
            if (clear)
                System.Array.Clear(array, 0, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            Debug.Assert(!(array is null));
            ArrayPool<T>.Shared.Return(Array);
#if UNITY_ASSERTIONS
            array = null;
#endif

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Span<T>(ArraySlice<T> source)
            => source.Array.AsSpan(0, source.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlySpan<T>(ArraySlice<T> source)
            => source.Array.AsSpan(0, source.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ArraySlice<T>(RawPooledList<T> source)
            => new ArraySlice<T>(source.UnderlyingArray, source.Count);
    }
}
