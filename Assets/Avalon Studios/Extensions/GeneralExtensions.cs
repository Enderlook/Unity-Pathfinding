using UnityEngine;

namespace AvalonStudios.Additions.Extensions
{
    public static class GeneralExtensions
    {
        /// <summary>
        /// Returns true if the object is null,
        /// false if is not null.
        /// </summary>
        /// <typeparam name="T">The generic type of object to check.</typeparam>
        /// <returns>
        /// True if is null.
        /// False if is not null.
        /// </returns>
        public static bool IsNull<T>(this T v) =>
            v == null ? true : false;
    }
}
