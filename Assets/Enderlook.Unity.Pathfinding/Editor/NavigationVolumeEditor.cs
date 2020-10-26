using UnityEditor;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [CustomEditor(typeof(NavigationVolume))]
    internal sealed class NavigationVolumeEditor : Editor
    {
        private static readonly GUIContent BAKE_BUTTON = new GUIContent("Bake", "Bake the path area.");
        private static readonly GUIContent CLEAR_BUTTON = new GUIContent("Clear", "Remove the bake.");
        private static readonly GUIContent GIZMOS_FOLDOUT = new GUIContent("Gizmos", "Gizmos configuration.");
        private static readonly GUIContent DRAW_MODE_ENUM = new GUIContent("Draw Mode", "Determines how the octree is drawed by the gizmos.");
        private static readonly GUIContent OCTANS_COUNT_LABEL = new GUIContent("Octans", "Amount of stores octans.");
        private static readonly GUIContent NEIGHBOURS_COUNT_LABEL = new GUIContent("Neighbours", "Amount of stored neighbours.");
        private static readonly GUIContent DRAW_PATH_TOGGLE = new GUIContent("Draw Path", "If true, two handles will be provided to calculate paths between them.");
        private static readonly GUIContent START_LABEL = new GUIContent("Start", "Start position of the path to show.");
        private static readonly GUIContent END_LABEL = new GUIContent("End", "End position of the path to show.");

        private new NavigationVolume target;

        private bool drawGizmos;

        private bool drawPath;
        private Vector3 startPosition;
        private Transform startTransform;
        private Octree.OctantCode startOctant;
        private Vector3 endPosition;
        private Transform endTransform;
        private Octree.OctantCode endOctant;
        private PathBuilder<Octree.OctantCode, Vector3> pathBuilder;
        private Path<Vector3> path;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnEnable()
        {
            target = (NavigationVolume)base.target;
            startPosition = endPosition = target.transform.position;
            pathBuilder = new PathBuilder<Octree.OctantCode, Vector3>();
            path = new Path<Vector3>();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button(BAKE_BUTTON))
                {
                    target.Bake();
                    EditorUtility.SetDirty(target);
                    startOctant = target.Graph.FindClosestNodeTo(startPosition);
                    endOctant = target.Graph.FindClosestNodeTo(endPosition);
                    if (!(startOctant.IsInvalid || endOctant.IsInvalid))
                    {
                        target.CalculatePath(startPosition, endPosition, pathBuilder);
                        if (pathBuilder.Status == PathBuilderState.PathFound)
                            pathBuilder.FeedPathTo(path);
                    }
                }
                if (GUILayout.Button(CLEAR_BUTTON))
                {
                    target.Clear();
                    EditorUtility.SetDirty(target);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (drawGizmos = EditorGUILayout.Foldout(drawGizmos, GIZMOS_FOLDOUT, true))
            {
                EditorGUI.indentLevel++;
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.IntField(OCTANS_COUNT_LABEL, target.Graph.OctantsCount);
                    EditorGUILayout.IntField(NEIGHBOURS_COUNT_LABEL, target.Graph.NeighboursCount);
                    EditorGUI.EndDisabledGroup();
                    target.Graph.drawMode = (Octree.DrawMode)EditorGUILayout.EnumFlagsField(DRAW_MODE_ENUM, target.Graph.drawMode);

                    if (drawPath = EditorGUILayout.Toggle(DRAW_PATH_TOGGLE, drawPath))
                    {
                        EditorGUI.indentLevel++;
                        {
                            Vector3 start = startPosition;
                            Vector3 end = endPosition;
                            DrawPositionPathField(START_LABEL, ref startTransform, ref startPosition, ref startOctant);
                            DrawPositionPathField(END_LABEL, ref endTransform, ref endPosition, ref endOctant);
                            if (start != startPosition || end != endPosition)
                            {
                                target.CalculatePath(start, end, pathBuilder);
                                if (pathBuilder.Status == PathBuilderState.PathFound)
                                    pathBuilder.FeedPathTo(path);
                            }
                        }
                        EditorGUI.indentLevel--;
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        private void DrawPositionPathField(GUIContent label, ref Transform transform, ref Vector3 position, ref Octree.OctantCode octant)
        {
            EditorGUILayout.LabelField(label);
            EditorGUI.indentLevel++;
            {
                transform = (Transform)EditorGUILayout.ObjectField(GUIContent.none, transform, typeof(Transform), allowSceneObjects: true);
                if (transform != null)
                    position = transform.position;

                Vector3 old = position;
                position = EditorGUILayout.Vector3Field(GUIContent.none, position);
                if (transform != null)
                    transform.position = position;

                if (old != position)
                    octant = target.Graph.FindClosestNodeTo(position);
            }
            EditorGUI.indentLevel--;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnSceneGUI()
        {
            if (drawPath)
            {
                startPosition = Handles.DoPositionHandle(startPosition, Quaternion.identity);
                endPosition = Handles.DoPositionHandle(endPosition, Quaternion.identity);

                Handles.color = Color.blue;

                if (!startOctant.IsInvalid)
                    target.Graph.DrawOctantWithHandle(startOctant);

                if (!endOctant.IsInvalid)
                    target.Graph.DrawOctantWithHandle(endOctant);

                if (!(startOctant.IsInvalid || endOctant.IsInvalid) && pathBuilder.Status == PathBuilderState.PathFound)
                {
                    using (Path<Vector3>.Enumerator enumerator = path.GetEnumerator())
                    {
                        if (enumerator.MoveNext())
                        {
                            Vector3 start;
                            Vector3 end = enumerator.Current;
                            while (enumerator.MoveNext())
                            {
                                start = end;
                                end = enumerator.Current;
                                Handles.DrawLine(start, end);
                            }
                        }
                    }
                }
            }
        }
    }
}