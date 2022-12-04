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

                        Task task = Task.Run(async () => await navigationSurface.BuildNavigation(true));

                        root.schedule
                            .Execute(() =>
                            {
                                if (task.IsCompleted)
                                {
                                    build.SetEnabled(true);
                                    progressBar.value = 0;
                                    progressBar.style.display = DisplayStyle.None;
                                    if (task.IsFaulted)
                                        Debug.LogException(task.Exception);
                                    return;
                                }
                                progressBar.value = navigationSurface.Progress() * 100;
                            })
                            .Every(1000 / 60)
                            .Until(() => task.IsCompleted);
                    };
                }
                root.Add(build);
                root.Add(progressBar);
            }
            return root;
        }
    }
}