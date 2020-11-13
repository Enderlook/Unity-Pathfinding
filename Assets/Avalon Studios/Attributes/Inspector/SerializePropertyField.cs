using System;

using UnityEngine;

namespace AvalonStudios.Additions.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public sealed class SerializePropertyField : PropertyAttribute
    {
        public SerializePropertyField() { }
    }
}
