using Enderlook.Unity.Utils.UnityEditor;

using UnityEditor;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [CustomEditor(typeof(NavigationAgent))]
    internal sealed class NavigationAgentEditor : Editor
    {
        private static readonly GUIContent DRAW_PATH_TOGGLE = new GUIContent("Draw Path", "Draw the current set path.");

        private new NavigationAgent target;

        private bool drawPath;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnEnable() => target = (NavigationAgent)base.target;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUIHelper.Header("Other");

            drawPath = EditorGUILayout.Toggle(DRAW_PATH_TOGGLE, drawPath);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnSceneGUI()
        {
            if (drawPath)
            {
                DynamicArray<Vector3>.Enumerator enumerator = target.PathFollower.enumerator;
                if (enumerator.IsDefault)
                    return;

                Handles.color = Color.blue;
                Vector3 start;
                Vector3 end = target.transform.position;
                enumerator.MoveBack();
                while (enumerator.MoveNext())
                {
                    Handles.DrawWireCube(end, Vector3.one * .1f);
                    start = end;
                    end = enumerator.Current;
                    Handles.DrawLine(start, end);
                }
                Handles.DrawWireCube(end, Vector3.one * .1f);
            }
        }
    }
}