using System.Collections;
using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [CustomEditor(typeof(PathAreaAnchor))]
    internal sealed class PathAreaAnchorEditor : Editor
    {
        private readonly static GUIContent BAKE_BUTTON = new GUIContent("Bake", "Bake the path area.");

        private new PathAreaAnchor target;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnEnable() => target = (PathAreaAnchor)base.target;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUI.BeginChangeCheck();
            if (GUILayout.Button(BAKE_BUTTON))
                target.Bake();
            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();
        }
    }
}