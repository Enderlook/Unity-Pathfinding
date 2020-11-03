using System.Collections.Generic;

namespace System
{
    public static class MemoryExtensions
    {
        public static void Sort<T, U>(this Span<T> source, U comparer)
            where U : IComparer<T>
        {
            if (source.Length > 1)
            {
                InsertionSort(source, comparer);
            }
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
    }
}