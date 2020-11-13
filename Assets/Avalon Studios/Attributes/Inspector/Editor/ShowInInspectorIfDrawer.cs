using AvalonStudios.Additions.Extensions;

using UnityEngine;
using UnityEditor;

namespace AvalonStudios.Additions.Attributes
{
    [CustomPropertyDrawer(typeof(ShowInInspectorIf))]
    public class ShowInInspectorIfDrawer : PropertyDrawer
    {
        private ShowInInspectorIf conditionalHideAtt;
        private bool enabledDisabled = false; // if this is true, then draw property
        private bool active;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (conditionalHideAtt == null) conditionalHideAtt = attribute as ShowInInspectorIf;
            bool enabled = GetConditionalHideAttribute(conditionalHideAtt, property);

            //Enable / Disable the property (field)
            bool wasEnabled = GUI.enabled;

            active = active == conditionalHideAtt.hideInInspector;

            //EditorGUI.BeginProperty(position, label, property);

            //Check if we should draw the property
            if (!conditionalHideAtt.hideInInspector || enabled)
            {
                enabledDisabled = true;
                EditorGUI.PropertyField(position, property, label.text.ContainsBackingField() ? new GUIContent(label.text.RenameAutoProperty()) : label, true);
                //if (!active)
                //    Draw(conditionalHideAtt, position, property, label, true);
            }
            //else
            //{
            //    if (active)
            //        Draw(conditionalHideAtt, position, property, label, true);
            //}

            GUI.enabled = wasEnabled;

            //EditorGUI.EndProperty();
        }

        private void Draw(ShowInInspectorIf condHAtt, Rect position, SerializedProperty property, GUIContent label, bool includeChilds = false)
        {
            bool idented = condHAtt.idented;
            if (idented)
                EditorGUI.indentLevel++;
            EditorGUI.PropertyField(position, property, label, true);
            if (idented)
                EditorGUI.indentLevel--;
        }

        private bool GetConditionalHideAttribute(ShowInInspectorIf condHAtt, SerializedProperty property)
        {
            bool enabled = true;
            // Look for the sourcefield within the object that the property belongs to
            string propertyPath = property.propertyPath; // Returns the property path of the property we want to apply the attribute to
            string conditionalPath = propertyPath.Replace(property.name, condHAtt.conditionalSourceField); // Changes the path to the conditionalsource property path
            SerializedProperty sourcePropertyValue = property.serializedObject.FindSerializedProperty(conditionalPath);
            if (sourcePropertyValue == null)
            {
                conditionalPath = propertyPath.Replace(property.name, condHAtt.conditionalSourceField.RenamePropertyToAutoProperty());
                sourcePropertyValue = property.serializedObject.FindSerializedProperty(conditionalPath);
            }

            if (sourcePropertyValue != null)
                enabled = sourcePropertyValue.boolValue;
            else
                Debug.LogWarning($"Attempting to use a ConditionalHideAttribute but no matching SourcePropertyValue found in object: {condHAtt.conditionalSourceField}",
                    property.serializedObject.targetObject);

            return enabled;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (enabledDisabled)
                return EditorGUI.GetPropertyHeight(property, label, true);
            else
                //The property is not being drawn
                //Undo the spacing added before and after the property
                return 0;
        }

        private void LogWarning(string logWarning, SerializedProperty property)
        {
            var warning = $"Property <color = brown> ${fieldInfo.Name} </color>";
            if (fieldInfo.DeclaringType != null) warning += $"on behaviour <color = brown> ${fieldInfo.DeclaringType.Name} </color>";
            warning += $"caused: {logWarning}";

            Debug.LogWarning(warning, property.serializedObject.targetObject);
        }
    }
}
