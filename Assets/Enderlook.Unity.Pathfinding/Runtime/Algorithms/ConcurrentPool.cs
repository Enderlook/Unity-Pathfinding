using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Enderlook.Unity.Pathfinding.Algorithms
{
    internal static class ConcurrentPool<T> where T : class, new()
    {
        private static readonly ConcurrentBag<T> pool = new ConcurrentBag<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Rent()
        {
            if (pool.TryTake(out T result))
                return result;
            return new T();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(T value) => pool.Add(value);
    }
}