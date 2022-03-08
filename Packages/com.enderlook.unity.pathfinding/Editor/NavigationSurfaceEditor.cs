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

                        Task.Run(async () =>
                        {
                            ValueTask task = navigationSurface.BuildNavigation(true);
                            EditorApplication.CallbackFunction onUpdate = UpdateBar;
                            EditorApplication.update += onUpdate;
                            Action onSchedule = null;
                            onSchedule = () =>
                            {
                                if (task.IsCompleted)
                                    return;

                                UpdateBar();
                                root.schedule.Execute(onSchedule);
                            };
                            UnityThread.RunNow(onSchedule);
                            await task;
                            EditorApplication.update -= onUpdate;
                        }).ContinueWith(e =>
                        {
                            build.SetEnabled(true);
                            UnityThread.RunNow(() =>
                            {
                                // Executed on the Unity thread because the progress bar requires to call an internal Unity API.
                                progressBar.value = 0;
                                progressBar.style.display = DisplayStyle.None;
                            });
                            if (e.IsFaulted)
                                Debug.LogException(e.Exception);
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