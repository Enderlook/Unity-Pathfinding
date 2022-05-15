using Enderlook.Unity.Threading;

using System;
using System.Threading.Tasks;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

namespace Enderlook.Unity.Pathfinding
{
    [CustomEditor(typeof(NavigationSurface))]
    public sealed class NavigationSurfaceEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            {
                root.Add(new IMGUIContainer(() => DrawDefaultInspector()));

                Label title = new Label("Building");
                {
                    title.style.unityFontStyleAndWeight = FontStyle.Bold;
                }
                root.Add(title);

                ProgressBar progressBar = new ProgressBar();
                {
                    progressBar.style.display = DisplayStyle.None;
                }
                Button build = new Button();
                {
                    build.text = "Build";
                    build.clickable.clicked += () =>
                    {
                        NavigationSurface navigationSurface = (NavigationSurface)serializedObject.targetObject;
                        build.SetEnabled(false);
                        progressBar.value = 0;
                        progressBar.style.display = DisplayStyle.Flex;

                        ValueTask task = default;
                        Action onSchedule = null;
                        onSchedule = () =>
                        {
                            if (task.IsCompleted)
                            {
                                build.SetEnabled(true);
                                progressBar.value = 0;
                                progressBar.style.display = DisplayStyle.None;
                                if (task.IsFaulted)
                                    Debug.LogException(task.AsTask().Exception);
                                return;
                            }

                            UpdateBar();
                            root.schedule.Execute(onSchedule);
                        };

                        Task.Run(async () =>
                        {
                            task = navigationSurface.BuildNavigation(true);
                            UnityThread.RunNow(onSchedule);
                            EditorApplication.CallbackFunction onUpdate = UpdateBar;
                            EditorApplication.update += onUpdate;
                            await task;
                            EditorApplication.update -= onUpdate;
                        });

                        void UpdateBar() => progressBar.value = navigationSurface.Progress() * 100;
                    };
                }
                root.Add(build);
                root.Add(progressBar);
            }
            return root;
        }
    }
}