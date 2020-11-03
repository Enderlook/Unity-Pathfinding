using System.Collections.Generic;

namespace System
{
    /// <summary>
    /// Extension methods for Span{T}, Memory{T}, and friends.
    /// </summary>
    public static class MemoryExtensions
    {
        /// <summary>
        /// Sorts the elements in the entire <see cref="Span{T}" /> using the <see cref="IComparable{T}" /> implementation
        /// of each element of the <see cref= "Span{T}" />
        /// </summary>
        /// <typeparam name="T">The type of the elements of the span.</typeparam>
        /// <param name="span">The <see cref="Span{T}"/> to sort.</param>
        /// <exception cref="InvalidOperationException">
        /// One or more elements in <paramref name="span"/> do not implement the <see cref="IComparable{T}" /> interface.
        /// </exception>
        public static void Sort<T>(this Span<T> span) => span.Sort((IComparer<T>)null);

        /// <summary>
        /// Sorts the elements in the entire <see cref="Span{T}" /> using the <typeparamref name="TComparer" />.
        /// </summary>
        /// <typeparam name="T">The type of the elements of the span.</typeparam>
        /// <typeparam name="TComparer">The type of the comparer to use to compare elements.</typeparam>
        /// <param name="span">The <see cref="Span{T}"/> to sort.</param>
        /// <param name="comparer">
        /// The <see cref="IComparer{T}"/> implementation to use when comparing elements, or null to
        /// use the <see cref="IComparable{T}"/> interface implementation of each element.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// <paramref name="comparer"/> is null, and one or more elements in <paramref name="span"/> do not
        /// implement the <see cref="IComparable{T}" /> interface.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The implementation of <paramref name="comparer"/> caused an error during the sort.
        /// </exception>
        public static void Sort<T, TComparer>(this Span<T> span, TComparer comparer)
            where TComparer : IComparer<T>
        {
            if (comparer == null)
                throw new InvalidOperationException(nameof(comparer));

            if (span.Length > 1)
                InsertionSort(span, comparer);
        }

        /// <summary>
        /// Sorts the elements in the entire <see cref="Span{T}" /> using the specified <see cref="Comparison{T}" />.
        /// </summary>
        /// <typeparam name="T">The type of the elements of the span.</typeparam>
        /// <param name="span">The <see cref="Span{T}"/> to sort.</param>
        /// <param name="comparison">The <see cref="Comparison{T}"/> to use when comparing elements.</param>
        /// <exception cref="ArgumentNullException"><paramref name="comparison"/> is null.</exception>
        public static void Sort<T>(this Span<T> span, Comparison<T> comparison)
        {
            if (comparison == null)
                throw new ArgumentNullException(nameof(comparison));

            if (span.Length > 1)
                InsertionSort(span, comparison);
        }

        /// <summary>
        /// Sorts a pair of spans (one containing the keys and the other containing the corresponding items)
        /// based on the keys in the first <see cref="Span{TKey}" /> using the <see cref="IComparable{T}" />
        /// implementation of each key.
        /// </summary>
        /// <typeparam name="TKey">The type of the elements of the key span.</typeparam>
        /// <typeparam name="TValue">The type of the elements of the items span.</typeparam>
        /// <param name="keys">The span that contains the keys to sort.</param>
        /// <param name="items">The span that contains the items that correspond to the keys in <paramref name="keys"/>.</param>
        /// <exception cref="ArgumentException">
        /// The length of <paramref name="keys"/> isn't equal to the length of <paramref name="items"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// One or more elements in <paramref name="keys"/> do not implement the <see cref="IComparable{T}" /> interface.
        /// </exception>
        public static void Sort<TKey, TValue>(this Span<TKey> keys, Span<TValue> items) => keys.Sort(items, (IComparer<TKey>)null);

        /// <summary>
        /// Sorts a pair of spans (one containing the keys and the other containing the corresponding items)
        /// based on the keys in the first <see cref="Span{TKey}" /> using the specified comparer.
        /// </summary>
        /// <typeparam name="TKey">The type of the elements of the key span.</typeparam>
        /// <typeparam name="TValue">The type of the elements of the items span.</typeparam>
        /// <typeparam name="TComparer">The type of the comparer to use to compare elements.</typeparam>
        /// <param name="keys">The span that contains the keys to sort.</param>
        /// <param name="items">The span that contains the items that correspond to the keys in <paramref name="keys"/>.</param>
        /// <param name="comparer">
        /// The <see cref="IComparer{T}"/> implementation to use when comparing elements, or null to
        /// use the <see cref="IComparable{T}"/> interface implementation of each element.
        /// </param>
        /// <exception cref="ArgumentException">
        /// The length of <paramref name="keys"/> isn't equal to the length of <paramref name="items"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// <paramref name="comparer"/> is null, and one or more elements in <paramref name="keys"/> do not
        /// implement the <see cref="IComparable{T}" /> interface.
        /// </exception>
        public static void Sort<TKey, TValue, TComparer>(this Span<TKey> keys, Span<TValue> items, TComparer comparer)
            where TComparer : IComparer<TKey>
        {
            if (keys.Length != items.Length)
                throw new ArgumentException($"Lenght of {nameof(keys)} and {nameof(items)} must be the same.");

            if (keys.Length > 1)
                InsertionSort(keys, items, comparer);
        }

        /// <summary>
        /// Sorts a pair of spans (one containing the keys and the other containing the corresponding items)
        /// based on the keys in the first <see cref="Span{TKey}" /> using the specified comparison.
        /// </summary>
        /// <typeparam name="TKey">The type of the elements of the key span.</typeparam>
        /// <typeparam name="TValue">The type of the elements of the items span.</typeparam>
        /// <param name="keys">The span that contains the keys to sort.</param>
        /// <param name="items">The span that contains the items that correspond to the keys in <paramref name="keys"/>.</param>
        /// <param name="comparison">The <see cref="Comparison{T}"/> to use when comparing elements.</param>
        /// <exception cref="ArgumentNullException"><paramref name="comparison"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The length of <paramref name="keys"/> isn't equal to the length of <paramref name="items"/>.
        /// </exception>
        public static void Sort<TKey, TValue>(this Span<TKey> keys, Span<TValue> items, Comparison<TKey> comparison)
        {
            if (comparison == null)
                throw new ArgumentNullException(nameof(comparison));
            if (keys.Length != items.Length)
                throw new ArgumentException($"Lenght of {nameof(keys)} and {nameof(items)} must be the same.");

            if (keys.Length > 1)
                InsertionSort(keys, items, comparison);
        }

        private static void InsertionSort<T, U>(Span<T> input, U comparer)
            where U : IComparer<T>
        {
            for (int i = 0; i < input.Length; i++)
            {
                T item = input[i];
                int currentIndex = i;

                while (currentIndex > 0 && comparer.Compare(input[currentIndex - 1], item) > 0)
                {
                    input[currentIndex] = input[currentIndex - 1];
                    currentIndex--;
                }

                input[currentIndex] = item;
            }
        }

        private static void InsertionSort<T>(Span<T> input, Comparison<T> comparison)
        {
            for (int i = 0; i < input.Length; i++)
            {
                T item = input[i];
                int currentIndex = i;

                while (currentIndex > 0 && comparison(input[currentIndex - 1], item) > 0)
                {
                    input[currentIndex] = input[currentIndex - 1];
                    currentIndex--;
                }

                input[currentIndex] = item;
            }
        }

        private static void InsertionSort<TKey, TValue, TComparer>(Span<TKey> keys, Span<TValue> values, TComparer comparer)
            where TComparer : IComparer<TKey>
        {
            for (int i = 0; i < keys.Length; i++)
            {
                TKey key = keys[i];
                TValue value = values[i];
                int currentIndex = i;

                while (currentIndex > 0 && comparer.Compare(keys[currentIndex - 1], key) > 0)
                {
                    keys[currentIndex] = keys[currentIndex - 1];
                    values[currentIndex] = values[currentIndex - 1];
                    currentIndex--;
                }

                keys[currentIndex] = key;
                values[currentIndex] = value;
            }
        }

        private static void InsertionSort<TKey, TValue>(Span<TKey> keys, Span<TValue> values, Comparison<TKey> comparison)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                TKey key = keys[i];
                TValue value = values[i];
                int currentIndex = i;

                while (currentIndex > 0 && comparison(keys[currentIndex - 1], key) > 0)
                {
                    keys[currentIndex] = keys[currentIndex - 1];
                    values[currentIndex] = values[currentIndex - 1];
                    currentIndex--;
                }

                keys[currentIndex] = key;
                values[currentIndex] = value;
            }
        }
    }
}