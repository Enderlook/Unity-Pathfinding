using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Defines a lighweight, zero-cost, simple dynamic array with no safety costly features.
    /// </summary>
    /// <typeparam name="T">Type of the array.</typeparam>
    [DebuggerDisplay("Count = {Count}")]
    internal struct DynamicArray<T> : IEnumerable<T>
    {
        private const int MaxArrayLength = 0X7FEFFFFF; // Copy of Array.MaxArrayLength
        private const int GrowFactor = 2;
        private const int DefaultCapacity = 4;
        private static readonly T[] emptyArray = new T[0];

        private T[] items;
        private int size;

        /// <summary>
        /// Access an element from the array.
        /// </summary>
        /// <param name="index">index to access.</param>
        /// <returns>Reference to the element of the given index.</returns>
        public ref T this[int index] {
            get {
                if (index >= size)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return ref items[index];
            }
        }

        /// <summary>
        /// Used slots in the array.
        /// </summary>
        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => size;
        }

        /// <summary>
        /// Capacity of the heap.
        /// </summary>
        public int Capacity {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => items.Length;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                if (value < size)
                    throw new ArgumentOutOfRangeException(nameof(value), "New capacity can't be smaller than Count.");

                if (value == items.Length)
                    return;

                if (value == 0)
                {
                    items = emptyArray;
                    return;
                }

                T[] newItems = new T[value];
                if (size > 0)
                    Array.Copy(items, newItems, size);
                items = newItems;
            }
        }

        /// <summary>
        /// Determines if this array is empty.
        /// </summary>
        public bool IsEmpty {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => size == 0;
        }

        /// <summary>
        /// Determines if this dynamic array was not initialized.
        /// </summary>
        public bool IsDefault {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => items is null;
        }

        /// <summary>
        /// Determines if this dynamic array was not initialized or is empty.
        /// </summary>
        public bool IsDefaultOrEmpty {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => IsDefault || IsEmpty;
        }

        /// <summary>
        /// Returns the span of the current underlying array.
        /// </summary>
        public Span<T> AsSpan {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => items.AsSpan(0, size);
        }

        /// <summary>
        /// Returns the current underlying array.
        /// </summary>
        public T[] UnderlyingArray {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => items;
        }

        /// <summary>
        /// Creates a dynamic array from an existent array consuming it.
        /// </summary>
        /// <param name="array">Array to consume.</param>
        /// <param name="count">Consumed part of the array.</param>
        /// <returns>New dynamic array.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DynamicArray<T> Create(T[] array, int count) => new DynamicArray<T>()
        {
            items = array,
            size = count,
        };

        /// <summary>
        /// Creates a dynamic array from an existent array consuming it.
        /// </summary>
        /// <param name="array">Array to consume.</param>
        /// <returns>New dynamic array.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DynamicArray<T> Create(T[] array) => Create(array, array.Length);

        /// <summary>
        /// Creates a dynamic array from an existent array consuming it.
        /// </summary>
        /// <returns>New dynamic array.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DynamicArray<T> Create() => Create(emptyArray, 0);

        /// <summary>
        /// Creates a dynamic array from an existent array consuming it.
        /// </summary>
        /// <param name="initialSize">Capacity of the underlying array.</param>
        /// <returns>New dynamic array.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DynamicArray<T> Create(int initialSize) => Create(new T[initialSize], 0);

        /// <summary>
        /// Creates a dynamic array from an enumerable.
        /// </summary>
        /// <param name="elements">Elements of the array</param>
        /// <returns>New dynamic array.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DynamicArray<T> Create<U>(U elements) where U : IEnumerable<T>
            => Create(elements.ToArray());

        /// <summary>
        /// Creates a dynamic array from an enumerator.
        /// </summary>
        /// <param name="elements">Elements of the array</param>
        /// <returns>New dynamic array.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DynamicArray<T> CreateFromEnumerator<U>(U elements) where U : IEnumerator<T>
        {
            DynamicArray<T> dynamicArray = Create();
            dynamicArray.AddRangeEnumerator(elements);
            return dynamicArray;
        }

        /// <summary>
        /// Add an element to the array.
        /// </summary>
        /// <param name="element">Element to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T element)
        {
            T[] array = items;
            int size = this.size;

            if ((uint)size < (uint)array.Length)
            {
                this.size = size + 1;
                array[size] = element;
            }
            else
                EnqueueWithResize(element);
        }

        // Non-inline to improve its code quality as uncommon path
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void EnqueueWithResize(T element)
        {
            int size = this.size;

            int newCapacity = items.Length == 0 ? DefaultCapacity : items.Length * GrowFactor;
            // Allow the list to grow to maximum possible capacity (~2G elements) before encountering overflow.
            // Note that this check works even when items.Length overflowed thanks to the (uint) cast
            if ((uint)newCapacity > MaxArrayLength)
                newCapacity = MaxArrayLength;
            T[] newItems = new T[newCapacity];
            if (items.Length != 0)
                Array.Copy(items, newItems, items.Length);
            items = newItems;

            this.size = size + 1;
            items[size] = element;
        }

        // Non-inline to improve its code quality as uncommon path
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void GrowCapacityTo(int min)
        {
            int newCapacity = items.Length == 0 ? DefaultCapacity : items.Length * GrowFactor;
            // Allow the list to grow to maximum possible capacity (~2G elements) before encountering overflow.
            // Note that this check works even when items.Length overflowed thanks to the (uint) cast
            if ((uint)newCapacity > MaxArrayLength)
                newCapacity = MaxArrayLength;
            if (newCapacity < min)
                newCapacity = min;
            T[] newItems = new T[newCapacity];
            if (items.Length != 0)
                Array.Copy(items, newItems, items.Length);
            items = newItems;
        }

        /// <summary>
        /// Add an element to the array.
        /// </summary>
        /// <param name="elements">Elements to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(params T[] elements)
        {
            int min = size + elements.Length;
            if (items.Length < min)
                GrowCapacityTo(min);

            Array.Copy(elements, 0, items, size, elements.Length);
            size = min;
        }

        /// <summary>
        /// Add an element to the array.
        /// </summary>
        /// <param name="elements">Elements to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange<U>(U elements) where U : IEnumerable<T>
        {
            if (elements is Collection<T> collection)
            {
                int min = size + collection.Count;
                if (items.Length < min)
                    GrowCapacityTo(min);

                foreach (T element in elements)
                    items[size++] = element;
            }
            else
            {
                foreach (T element in elements)
                    Add(element);
            }
        }

        /// <summary>
        /// Add an element to the array.
        /// </summary>
        /// <param name="elements">Elements to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRangeEnumerator<U>(U elements) where U : IEnumerator<T>
        {
            using (U _elements = elements)
            {
                while (_elements.MoveNext())
                    Add(_elements.Current);
            }
        }

        /// <summary>
        /// Add an element to the array.
        /// </summary>
        /// <param name="elements">Elements to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(Span<T> elements)
        {
            int min = size + elements.Length;
            if (items.Length < min)
                GrowCapacityTo(min);

            elements.CopyTo(items.AsSpan(size));
            size = min;
        }

        /// <summary>
        /// Add an element to the array.
        /// </summary>
        /// <param name="elements">Elements to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(ReadOnlySpan<T> elements)
        {
            int min = size + elements.Length;
            if (items.Length < min)
                GrowCapacityTo(min);

            elements.CopyTo(items.AsSpan(size));
            size = min;
        }

        /// <summary>
        /// Clear the content of the array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Array.Clear(items, 0, size);
            size = 0;
        }

        /// <summary>
        /// Reverse the content of the array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reverse() => Array.Reverse(items, 0, size);

        /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);

        /// <inheritdoc cref="IEnumerable.GetEnumerator"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// Enumerator of <see cref="DynamicArray{T}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            private DynamicArray<T> array;
            private int index;

            /// <summary>
            /// Determines if this enumerator was not initialized.
            /// </summary>
            public bool IsDefault {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => array.IsDefault;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(DynamicArray<T> array)
            {
                this.array = array;
                index = -1;
            }

            /// <inheritdoc cref="IEnumerator.MoveNext"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                int index = this.index + 1;
                if ((uint)index >= (uint)array.Count)
                {
                    this.index = array.Count;
                    return false;
                }
                this.index = index;
                return true;
            }

            /// <inheritdoc cref="IEnumerator{T}.Current"/>
            public T Current {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get {
                    if ((uint)index >= (uint)array.size)
                        throw new InvalidOperationException("Reached end of enumerator.");
                    return array.items[index];
                }
            }

            /// <inheritdoc cref="IEnumerator.Current"/>
            object IEnumerator.Current {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Current;
            }

            /// <inheritdoc cref="IEnumerator.Reset"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset() => index = -1;

            /// <inheritdoc cref="IDisposable.Dispose"/>
            public void Dispose() { }
        }
    }
}