using System;
using UnityEngine;

namespace AvalonStudios.Additions.Attributes
{
    /// <summary>
    /// Use this PropertyAttribute to add a styled header above some fields in the Inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = true)]
    public sealed class StyledHeader : PropertyAttribute
    {
        public readonly string header;

        public readonly int fontSize;
        public bool defaultFontSize = true;

        /// <summary>
        /// Add a header above some fields in the Inspector.
        /// </summary>
        /// <param name="header">The header text.</param>
        /// <param name="fontSize">Font size</param>
        public StyledHeader(string header, int fontSize = 0)
        {
            this.header = header;
            defaultFontSize = fontSize == 0;
            if (!defaultFontSize)
                this.fontSize = Mathf.Abs(fontSize);
        }
    }
}
