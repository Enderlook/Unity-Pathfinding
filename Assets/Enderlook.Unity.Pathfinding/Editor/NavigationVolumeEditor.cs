using Unity.Jobs;

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
        private static readonly GUIContent DRAW_PATH_FOLDOUT = new GUIContent("Draw Path", "If true, two handles will be provided to calculate paths between them.");
        private static readonly GUIContent START_LABEL = new GUIContent("Start", "Start position of the path to show.");
        private static readonly GUIContent END_LABEL = new GUIContent("End", "End position of the path to show.");
        private static readonly GUIContent PATH_COUNT_LABEL = new GUIContent("Count", "Amount of points it must travel.");
        private static readonly GUIContent PATH_DISTANCE_LABEL = new GUIContent("Distance", "Distance it must travel.");
        private static readonly GUIContent CACHED_COSTS = new GUIContent("Cached Costs", "Amount of costs that are cached.");
        private static readonly GUIContent CACHED_LINE_OF_SIGHT = new GUIContent("Cached Sights", "Amount of line of sights that are cached.");

        private new NavigationVolume target;

        private bool drawGizmos;

        private bool drawPath;
        private Vector3 startPosition;
        private Transform startTransform;
        private Octree.OctantCode startOctant;
        private Vector3 endPosition;
        private Transform endTransform;
        private Octree.OctantCode endOctant;
        private Path<Vector3> path;
        private bool mustRecalculate;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnEnable()
        {
            target = (NavigationVolume)base.target;
            startPosition = endPosition = target.transform.position;
            path = new Path<Vector3>();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (Application.isPlaying)
                EditorGUILayout.HelpBox(
                    "Modifying any of this values at runtime has undefined behaviour.\n" +
                    "Don't touch anything unless you know what you are doing.",
                    MessageType.Warning
                );
            EditorGUILayout.HelpBox("If you have baked results and have modified a value, you shall re-bake this.", MessageType.Warning);

            if (path.IsComplete)
                path.Complete();

            EditorGUILayout.BeginHorizontal();
            {
                EditorGUI.BeginDisabledGroup(target.bakedContent == null);
                {
                    if (GUILayout.Button(BAKE_BUTTON))
                    {
                        target.Bake();
                        target.Save();
                        EditorUtility.SetDirty(target);
                        EditorUtility.SetDirty(target.bakedContent);
                        if (drawPath)
                        {
                            startOctant = target.Graph.FindClosestNodeTo(startPosition);
                            endOctant = target.Graph.FindClosestNodeTo(endPosition);
                            if (path.IsComplete)
                            {
                                target.CalculatePath(path, startPosition, endPosition);
                                JobHandle.ScheduleBatchedJobs();
                            }
                            else
                                mustRecalculate = true;
                        }
                    }
                    if (GUILayout.Button(CLEAR_BUTTON))
                    {
                        target.Clear();
                        EditorUtility.SetDirty(target);
                        target.bakedContent.Clear();
                        EditorUtility.SetDirty(target.bakedContent);
                    }
                }
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndHorizontal();

            if (drawGizmos = EditorGUILayout.Foldout(drawGizmos, GIZMOS_FOLDOUT, true))
            {
                EditorGUI.indentLevel++;
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.IntField(OCTANS_COUNT_LABEL, target.Graph.OctantsCount);
                    EditorGUILayout.IntField(NEIGHBOURS_COUNT_LABEL, target.Graph.NeighboursCount);
                    EditorGUILayout.IntField(CACHED_COSTS, target.Graph.CachedDistances);
                    EditorGUILayout.IntField(CACHED_LINE_OF_SIGHT, target.Graph.CachedLineOfSights);
                    EditorGUI.EndDisabledGroup();
                    target.Graph.drawMode = (Octree.DrawMode)EditorGUILayout.EnumFlagsField(DRAW_MODE_ENUM, target.Graph.drawMode);

                    if (drawPath = EditorGUILayout.Foldout(drawPath, DRAW_PATH_FOLDOUT, true))
                    {
                        EditorGUI.indentLevel++;
                        {
                            if (DrawPositionPathField(START_LABEL, ref startTransform, ref startPosition, ref startOctant) |
                                DrawPositionPathField(END_LABEL, ref endTransform, ref endPosition, ref endOctant))
                            {
                                if (path.IsComplete)
                                {
                                    target.CalculatePath(path, startPosition, endPosition);
                                    JobHandle.ScheduleBatchedJobs();
                                }
                                else
                                    mustRecalculate = true;
                            }
                            EditorGUI.BeginDisabledGroup(true);
                            int count = 0;
                            float distance = 0;
                            if (!(startOctant.IsInvalid || endOctant.IsInvalid) && path.IsComplete && path.HasPath)
                            {
                                path.Complete();
                                count = path.Count;
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
                                            distance += Vector3.Distance(start, end);
                                        }
                                    }
                                }
                            }
                            EditorGUILayout.IntField(PATH_COUNT_LABEL, count);
                            EditorGUILayout.FloatField(PATH_DISTANCE_LABEL, distance);
                            EditorGUI.EndDisabledGroup();
                        }
                        EditorGUI.indentLevel--;
                    }
                }
                EditorGUI.indentLevel--;
            }
            if (mustRecalculate && path.IsComplete)
            {
                mustRecalculate = false;
                path.Complete();
                target.CalculatePath(path, startPosition, endPosition);
                JobHandle.ScheduleBatchedJobs();
            }
        }

        private bool DrawPositionPathField(GUIContent label, ref Transform transform, ref Vector3 position, ref Octree.OctantCode octant)
        {
            EditorGUILayout.LabelField(label);
            EditorGUI.indentLevel++;
            bool hasChanges;
            {
                Vector3 old = position;

                transform = (Transform)EditorGUILayout.ObjectField(GUIContent.none, transform, typeof(Transform), allowSceneObjects: true);
                if (transform != null)
                    position = transform.position;

                Vector3 old2 = position;
                position = EditorGUILayout.Vector3Field(GUIContent.none, position);
                if (transform != null)
                    transform.position = position;

                if (old != position || old2 != position)
                {
                    octant = target.Graph.FindClosestNodeTo(position);
                    hasChanges = true;
                }
                else
                    hasChanges = false;
            }
            EditorGUI.indentLevel--;
            return hasChanges;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnSceneGUI()
        {
            if (path.IsComplete)
                path.Complete();

            if (drawPath)
            {
                if (DrawPositionHandle(ref startPosition, startTransform, ref startOctant) | DrawPositionHandle(ref endPosition, endTransform, ref endOctant))
                {
                    if (path.IsComplete)
                    {
                        target.CalculatePath(path, startPosition, endPosition);
                        JobHandle.ScheduleBatchedJobs();
                    }
                    else
                        mustRecalculate = true;
                }
                else
                {
                    if (mustRecalculate && path.IsComplete)
                    {
                        mustRecalculate = false;
                        path.Complete();
                        target.CalculatePath(path, startPosition, endPosition);
                        JobHandle.ScheduleBatchedJobs();
                    }
                }

                Handles.color = Color.blue;

                if (!startOctant.IsInvalid)
                    target.Graph.DrawOctantWithHandle(startOctant);

                if (!endOctant.IsInvalid)
                    target.Graph.DrawOctantWithHandle(endOctant);

                if (!(startOctant.IsInvalid || endOctant.IsInvalid) && path.IsComplete && path.HasPath)
                {
                    path.Complete();
                    using (Path<Vector3>.Enumerator enumerator = path.GetEnumerator())
                    {
                        if (enumerator.MoveNext())
                        {
                            Vector3 start;
                            Vector3 end = enumerator.Current;
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
        }

        private bool DrawPositionHandle(ref Vector3 position, Transform transform, ref Octree.OctantCode octant)
        {
            Vector3 old = position;
            position = Handles.DoPositionHandle(position, Quaternion.identity);
            if (old != position)
            {
                if (transform != null)
                    transform.position = position;
                octant = target.Graph.FindClosestNodeTo(position);
                return true;
            }
            return false;
        }
    }
}