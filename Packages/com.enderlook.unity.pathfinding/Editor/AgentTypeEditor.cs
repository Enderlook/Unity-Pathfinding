using UnityEditor;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [CustomEditor(typeof(AgentType))]
    internal sealed class AgentTypeEditor : Editor
    {
        private static readonly GUIContent NAME_CONTENT = new GUIContent("Name", "Name of the agent type.");

        private new AgentType target;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnEnable() => target = (AgentType)base.target;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUI.BeginChangeCheck();
            target.name = EditorGUILayout.TextField(NAME_CONTENT, target.name);
            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();
        }
    }
}
