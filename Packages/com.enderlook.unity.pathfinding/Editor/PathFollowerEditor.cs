using Enderlook.Unity.Pathfinding.Steerings;

using System;
using System.Collections.Generic;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

using UnityObject = UnityEngine.Object;

namespace Enderlook.Unity.Pathfinding
{
    [CustomEditor(typeof(PathFollower))]
    internal sealed class PathFollowerEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            PathFollower target = (PathFollower)this.target;

            VisualElement root = new VisualElement();
            {
                root.Add(new IMGUIContainer(() => DrawDefaultInspector()));

                VisualElement runtime = new VisualElement();
                {
                    Toggle hasPath = new Toggle("Has Path");
                    {
                        hasPath.tooltip = "Whenever it contains a path.";
                        hasPath.SetEnabled(false);
                    }
                    runtime.Add(hasPath);

                    Toggle isCalculatingPath = new Toggle("Is Calculating Path");
                    {
                        isCalculatingPath.tooltip = "Whenever a path is being calculated at this moment.";
                        isCalculatingPath.SetEnabled(false);
                    }
                    runtime.Add(isCalculatingPath);

                    Vector3Field nextPosition = new Vector3Field("Next Position");
                    {
                        nextPosition.tooltip = "Current point that this follower want to reach from its path.";
                        nextPosition.SetEnabled(false);
                    }
                    runtime.Add(nextPosition);

                    Vector3Field destination = new Vector3Field("Destinatiom");
                    {
                        destination.tooltip = "Destination of this follower's path.";
                        destination.SetEnabled(false);
                    }
                    runtime.Add(destination);

                    root.schedule.Execute(() =>
                    {
                        bool isPlaying = Application.IsPlaying(target);

                        runtime.style.display = isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
                        if (isPlaying)
                        {
                            hasPath.value = target.HasPath;
                            isCalculatingPath.value = target.IsCalculatingPath;
                            if (target.HasPath)
                            {
                                nextPosition.value = target.NextPosition;
                                destination.value = target.Destination;
                            }
                            else
                                nextPosition.value = destination.value = new Vector3(float.NaN, float.NaN, float.NaN);
                        }
                    }).Every(1000 / 60);
                }
                root.Add(runtime);
            }
            return root;
        }
    }
}