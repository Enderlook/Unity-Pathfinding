using UnityEditor;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [CustomEditor(typeof(NavigationVolume))]
    internal sealed class NavigationVolumeEditor : Editor
    {
        private static readonly GUIContent BAKE_BUTTON = new GUIContent("Bake", "Bake the path area.");
        private static readonly GUIContent CLEAR_BUTTON = new GUIContent("Clear", "Remove the bake.");
        private static readonly GUIContent GIZMOS_LABEL = new GUIContent("Gizmos", "Gizmos configuration.");
        private static readonly GUIContent DRAW_MODE_ENUM = new GUIContent("Draw Mode", "Determines how the octree is drawed by the gizmos.");
        private static readonly GUIContent OCTANS_COUNT = new GUIContent("Octans", "Amount of stores octans.");
        private static readonly GUIContent NEIGHBOURS_COUNT = new GUIContent("Neighbours", "Amount of stored neighbours.");

        private new NavigationVolume target;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnEnable() => target = (NavigationVolume)base.target;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

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

            EditorGUILayout.PrefixLabel(GIZMOS_LABEL);
            EditorGUI.indentLevel++;
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.IntField(OCTANS_COUNT, target.Graph.OctantsCount);
                EditorGUILayout.IntField(NEIGHBOURS_COUNT, target.Graph.NeighboursCount);
                EditorGUI.EndDisabledGroup();
                target.Graph.drawMode = (Octree.DrawMode)EditorGUILayout.EnumFlagsField(DRAW_MODE_ENUM, target.Graph.drawMode);
            }
            EditorGUI.indentLevel--;
        }
    }
}