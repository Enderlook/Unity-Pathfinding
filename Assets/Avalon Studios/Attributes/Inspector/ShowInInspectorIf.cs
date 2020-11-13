using System;
using UnityEngine;

namespace AvalonStudios.Additions.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public sealed class ShowInInspectorIf : PropertyAttribute
    {
        public readonly string conditionalSourceField;

        public readonly bool hideInInspector = false;

        public bool idented;

        public ShowInInspectorIf(string conditionalSourceField)
        {
            this.conditionalSourceField = conditionalSourceField;
            this.hideInInspector = false;
        }

        public ShowInInspectorIf(string conditionalSourceField, bool hideInInspector)
        {
            this.conditionalSourceField = conditionalSourceField;
            this.hideInInspector = hideInInspector;
        }
    }
}
