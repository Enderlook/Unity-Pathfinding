using Enderlook.Unity.Pathfinding.Steerings;

using System;
using System.Collections;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

namespace Enderlook.Unity.Pathfinding
{
    [CustomEditor(typeof(FlockingLeader))]
    internal sealed class FlockingLeaderEditor : Editor
    {
        private static readonly Func<VisualElement> CREATE_ELEMENT = () =>
        {
            ObjectField field = new ObjectField();
            field.RegisterValueChangedCallback(e => field.SetValueWithoutNotify(e.previousValue));
            return field;
        };

        public override VisualElement CreateInspectorGUI()
        {
            FlockingLeader target = (FlockingLeader)this.target;

            VisualElement root = new VisualElement();
            {
                root.Add(new IMGUIContainer(() => DrawDefaultInspector()));

                Foldout foldout = new Foldout();
                {
                    foldout.text = "Flock Entities";
                    foldout.tooltip = "Followers of this leader.";

                    ListView list = new ListView();
                    {
                        list.tooltip = "Followers of this leader.";
#if UNITY_2020_1_OR_NEWER
                        list.showBoundCollectionSize = true;
                        list.showBorder = true;
                        list.showAlternatingRowBackgrounds = AlternatingRowBackground.All;
#endif
                        list.itemHeight = 20;
                        list.style.flexGrow = 1;
                        list.makeItem = CREATE_ELEMENT;
                        list.bindItem = (e, i) => ((ObjectField)e).value = target.followers[i];
                        list.itemsSource = new FollowersList(target);
                    }
                    foldout.Add(list);

                    root.schedule.Execute(() =>
                    {
                        bool isPlaying = Application.IsPlaying(target);
                        foldout.style.display = isPlaying ? DisplayStyle.Flex : DisplayStyle.None;

                        if (isPlaying)
                        {
                            target.Remove();
                            list.Refresh();
                            list.style.height = Math.Min(target.followers.Count * 20, 100) + 5;
                        }
                    }).Every(1000 / 60);
                }
                root.Add(foldout);
            }
            return root;
        }

        private sealed class FollowersList : IList
        {
            private readonly FlockingLeader target;

            public FollowersList(FlockingLeader target) => this.target = target;

            public object this[int index]
            {
                get => target.followers[index];
                set => target.followers[index] = (FlockingFollower)value;
            }

            public bool IsFixedSize => throw new NotImplementedException();

            public bool IsReadOnly => false;

            public int Count => target.followers.Count;

            public bool IsSynchronized => throw new NotImplementedException();

            public object SyncRoot => throw new NotImplementedException();

            public int Add(object value) => throw new NotImplementedException();

            public void Clear() => throw new NotImplementedException();

            public bool Contains(object value) => throw new NotImplementedException();

            public void CopyTo(Array array, int index) => throw new NotImplementedException();

            public IEnumerator GetEnumerator() => throw new NotImplementedException();

            public int IndexOf(object value) => throw new NotImplementedException();

            public void Insert(int index, object value) => throw new NotImplementedException();

            public void Remove(object value) => throw new NotImplementedException();

            public void RemoveAt(int index) => throw new NotImplementedException();
        }
    }
}