using System.Collections.Generic;
using System.Reflection;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// An unsafe class that provides a set of methods to access the underlying data representations of collections.
    /// </summary>
    public static class CollectionsMarshal
    {
        private const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo list_items = typeof(List<>).GetField("_items", flags);
        private static readonly FieldInfo list_size = typeof(List<>).GetField("_size", flags);

        /// <summary>
        /// Get a <see cref="Span{T}"/> view over a <see cref="List{T}"/>'s data.
        /// Items should not be added or removed from the <see cref="List{T}"/> while the <see cref="Span{T}"/> is in use.
        /// </summary>
        public static Span<T> AsSpan<T>(List<T> list)
            => list is null ? default : new Span<T>((T[])list_items.GetValue(list), 0, (int)list_size.GetValue(list));
    }
}