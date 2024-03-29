﻿using Enderlook.Collections.Pooled.LowLevel;

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
#if DEBUG
    internal struct ArraySlice<T>
#else
    internal readonly struct ArraySlice<T>
#endif
    {
#if DEBUG
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
#if DEBUG
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
#if DEBUG
            this.length = length;
            array = ArrayPool<T>.Shared.Rent(length);
            if (clear)
                System.Array.Clear(array, 0, length);
#else
            Length = length;
            Array = ArrayPool<T>.Shared.Rent(length);
            if (clear)
                System.Array.Clear(Array, 0, length);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => System.Array.Clear(Array, 0, Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
#if !DEBUG
            Debug.Assert(!(Array is null));
#endif
            ArrayPool<T>.Shared.Return(Array);
#if DEBUG
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
