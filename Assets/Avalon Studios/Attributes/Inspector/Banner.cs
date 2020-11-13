using System;

using UnityEngine;

namespace AvalonStudios.Additions.Attributes
{
    /// <summary>
    /// Use this PropertyAttribute to add a styled banner above in the Inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class Banner : PropertyAttribute
    {
        public readonly string title;
        public readonly string file;

        public Banner(string title, string file)
        {
            this.title = title;
            this.file = file;
        }
    }
}
