namespace AvalonStudios.Additions.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Returns the correct name of the auto serialize property.
        /// </summary>
        /// <returns>
        /// Returns the name of the auto property without "<T>k__BackingField".
        /// </returns>
        public static string RenameAutoProperty(this string name)
        {
            string tempName = name.Replace("k__BackingField", "");
            if (tempName.Length == name.Length)
                tempName = name.Replace("k__Backing Field", "");
            string newName = tempName.Trim('<', '>');
            return newName;
        }

        /// <summary>
        /// Returns the name with the format of auto property.
        /// </summary>
        /// <returns>
        /// Returns the name of the property with the format of auto property.
        /// </returns>
        public static string RenamePropertyToAutoProperty(this string name)
            => $"<{name}>k__BackingField";

        /// <summary>
        /// This extension method is specific for Auto Properties names.
        /// Returns a <seealso cref="bool"/> result.
        /// </summary>
        /// <returns>
        /// True if the <seealso cref="string"/> contains the "k__BackingField".
        /// False if the <seealso cref="string"/> not contains the "k__BackingField".
        /// </returns>
        public static bool ContainsBackingField(this string s)
        {
            if (s.Contains("k__BackingField"))
                return true;
            else if (s.Contains("k__Backing Field"))
                return true;
            else
                return false;
        }
    }
}
