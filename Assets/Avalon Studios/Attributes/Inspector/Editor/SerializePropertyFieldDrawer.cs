using UnityEngine;
using UnityEditor;

namespace AvalonStudios.Additions.Attributes
{
    [CustomPropertyDrawer(typeof(SerializePropertyField))]
    public class SerializePropertyFieldDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Debug.Log(property.name);
            base.OnGUI(position, property, label);
        }
    }
}
