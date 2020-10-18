using UnityEditor;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [CustomEditor(typeof(PathAreaAnchor))]
    internal sealed class PathAreaAnchorEditor : Editor
    {
        private static readonly GUIContent BAKE_BUTTON = new GUIContent("Bake", "Bake the path area.");
        private static readonly GUIContent CLEAR_BUTTON = new GUIContent("Clear", "Remove the bake.");
        private static readonly GUIContent GIZMOS_LABEL = new GUIContent("Gizmos", "Gizmos configuration.");
        private static readonly GUIContent DRAW_MODE_ENUM = new GUIContent("Draw Mode", "Determines how the octree is drawed by the gizmos.");
        private static readonly GUIContent NODES_COUNT = new GUIContent("Nodes", "Amount of stores nodes. Note that this value is actually the number of serialized nodes, so it can be outdated until a new serialization if changes are made.");

        private new PathAreaAnchor target;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnEnable() => target = (PathAreaAnchor)base.target;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button(BAKE_BUTTON))
                {
                    target.Bake();
                    EditorUtility.SetDirty(target);
                }
                if (GUILayout.Button(CLEAR_BUTTON))
                {
                    target.Clear();
                    EditorUtility.SetDirty(target);
                }
            }
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();

            EditorGUILayout.PrefixLabel(GIZMOS_LABEL);
            EditorGUI.indentLevel++;
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.IntField(NODES_COUNT, target.SerializedNodesCount);
                EditorGUI.EndDisabledGroup();
                target.DrawMode = (Octree.DrawMode)EditorGUILayout.EnumFlagsField(DRAW_MODE_ENUM, target.DrawMode);
            }
            EditorGUI.indentLevel--;
        }
    }
}