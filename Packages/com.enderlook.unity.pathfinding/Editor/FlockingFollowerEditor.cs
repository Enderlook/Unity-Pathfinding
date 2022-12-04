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
    [CustomEditor(typeof(FlockingFollower))]
    internal sealed class FlockingFollowerEditor : Editor
    {
        private static readonly Func<VisualElement> CREATE_ELEMENT = () =>
        {
            ObjectField field = new ObjectField();
            field.RegisterValueChangedCallback(e => field.SetValueWithoutNotify(e.previousValue));
            return field;
        };

        public override VisualElement CreateInspectorGUI()
        {
            FlockingFollower target = (FlockingFollower)this.target;

            VisualElement root = new VisualElement();
            {
                root.Add(new IMGUIContainer(() => DrawDefaultInspector()));

                Foldout foldout = new Foldout();
                {
                    foldout.text = "Flock in Range";
                    foldout.tooltip = "Others flocking follower agents in range.";

                    List<UnityObject> followers = new List<UnityObject>();
                    ListView list = new ListView();
                    {
                        list.tooltip = "Others flocking follower agents in range.";
#if UNITY_2020_1_OR_NEWER
                        list.showBoundCollectionSize = true;
                        list.showBorder = true;
                        list.showAlternatingRowBackgrounds = AlternatingRowBackground.All;
#endif
                        list.itemHeight = 20;
                        list.style.flexGrow = 1;
                        list.makeItem = CREATE_ELEMENT;
                        list.bindItem = (e, i) => ((ObjectField)e).value = followers[i];
                        list.itemsSource = followers;
                    }
                    foldout.Add(list);

                    root.schedule.Execute(() =>
                    {
                        bool isPlaying = Application.IsPlaying(target);
                        foldout.style.display = isPlaying ? DisplayStyle.Flex : DisplayStyle.None;

                        followers.Clear();
                        if (isPlaying)
                        {
                            Span<EntityInfo> entities = target.FlockingLeader.GetEntitiesInRange(target.Rigidbody, target.FlockingRange, target.BlockVisionLayers);
                            followers.Capacity = entities.Length;
                            foreach (EntityInfo entity in entities)
                            {
                                if (entity.Entity == target)
                                    continue;
                                followers.Add(entity.Entity);
                            }
                            list.Refresh();
                            list.style.height = Math.Min(followers.Count * 20, 100) + 5;
                        }
                    }).Every(1000 / 60);
                }
                root.Add(foldout);
            }
            return root;
        }
    }
}