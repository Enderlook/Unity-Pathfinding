using Enderlook.Unity.Pathfinding.Steerings;

using System;
using System.Collections;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

using UnityObject = UnityEngine.Object;

namespace Enderlook.Unity.Pathfinding
{
    [CustomEditor(typeof(NavigationAgentRigidbody))]
    internal sealed class NavigationAgentRigidbodyEditor : Editor
    {
        private static readonly Func<VisualElement> CreateElement = () =>
        {
            VisualElement root = new VisualElement();
            {
                ObjectField behaviourField = new ObjectField("Behaviour");
                {
                    behaviourField.name = "Behaviour";
                    behaviourField.tooltip = "Steering behaviour";
                }
                root.Add(behaviourField);

                VisualElement managed = new VisualElement();
                {
                    managed.name = "Managed";
                    managed.style.flexDirection = FlexDirection.Row;
                    managed.style.justifyContent = Justify.SpaceBetween;

                    Label managedLabel = new Label(" Managed Object"); // The initial space is intentional and improves the UI
                    managed.Add(managedLabel);

                    Label typeLabel = new Label();
                    {
                        typeLabel.name = "Managed Type";
                    }
                    managed.Add(typeLabel);
                }
                root.Add(managed);

                FloatField strengthField = new FloatField("Strength");
                {
                    strengthField.name = "Strength";
                    strengthField.tooltip = "Factor that multiplies the effect of the behaviour in the agent.";
                }
                root.Add(strengthField);

                Button remove = new Button();
                {
                    remove.name = "Remove";
                    remove.text = "Remove";
                    remove.tooltip = "Remove this steering behaviour.";
                    remove.userData = new ButtonData();
#if UNITY_2020_1_OR_NEWER
                    remove.clicked += () =>
#else
                    remove.clickable.clicked += () =>
#endif
                    {
                        ButtonData data = (ButtonData)remove.userData;
                        NavigationAgentRigidbody target = data.Target;
                        target.SetSteeringBehaviour(target.allSteeringBehaviours[data.Index].Behaviour, 0);
#if UNITY_2021_2_OR_NEWER
                        data.List.Rebuild();
#else
                        data.List.Refresh();
#endif
                    };
                }
                root.Add(remove);
            }
            return root;
        };

        public override VisualElement CreateInspectorGUI()
        {
            NavigationAgentRigidbody target = (NavigationAgentRigidbody)this.target;

            VisualElement root = new VisualElement();
            {
                ObjectField objectField = new ObjectField("Script");
                {
                    objectField.SetEnabled(false);
                    objectField.value = target;
                }
                root.Add(objectField);

                PropertyField linearSpeed = new PropertyField(serializedObject.FindProperty("linearSpeed"));
                root.Add(linearSpeed);

                PropertyField linearAcceleration = new PropertyField(serializedObject.FindProperty("linearAcceleration"));
                root.Add(linearAcceleration);

                PropertyField linearBrackingSpeed = new PropertyField(serializedObject.FindProperty("linearBrackingSpeed"));
                root.Add(linearBrackingSpeed);

                PropertyField angularSpeed = new PropertyField(serializedObject.FindProperty("angularSpeed"));
                root.Add(angularSpeed);

                PropertyField initialSteeringBehaviours = new PropertyField(serializedObject.FindProperty("initialSteeringBehaviours"));
                root.Add(initialSteeringBehaviours);

                Toggle updateMovement;
                Toggle updateRotation;
                Toggle brake;
                Toggle rotateEventWhenBraking;
                ListView list;
                VisualElement runtimeConfiguration = new VisualElement();
                {
                    Label label = new Label("Runtime Configuration");
                    {
                        label.style.unityFontStyleAndWeight = FontStyle.Bold;
                    }
                    runtimeConfiguration.Add(label);

                    updateMovement = new Toggle("Update Movement");
                    {
                        updateMovement.tooltip = "Determines if the agent has control over the rigidbody's position and velocity.";
                        updateMovement.value = target.UpdateMovement;
                        updateMovement.RegisterValueChangedCallback(e => target.UpdateMovement = e.newValue);
                    }
                    runtimeConfiguration.Add(updateMovement);

                    updateRotation = new Toggle("Update Rotation");
                    {
                        updateRotation.tooltip = "Determines if the agent has control over the rigidbody's rotation and angular velocity.";
                        updateRotation.value = target.UpdateRotation;
                        updateRotation.RegisterValueChangedCallback(e => target.UpdateRotation = e.newValue);
                    }
                    runtimeConfiguration.Add(updateRotation);

                    brake = new Toggle("Brake");
                    {
                        brake.tooltip = "If true, agent will deaccelerate until reach 0 velocity and won't increase it's velocity until this property becomes false.";
                        brake.value = target.UpdateRotation;
                        brake.RegisterValueChangedCallback(e => target.UpdateRotation = e.newValue);
                    }
                    runtimeConfiguration.Add(brake);

                    rotateEventWhenBraking = new Toggle("Rotate Even When Braking");
                    {
                        rotateEventWhenBraking.tooltip = "If true, agent will rotate even if Brake is true and velocity reached 0.";
                        rotateEventWhenBraking.value = target.UpdateRotation;
                        rotateEventWhenBraking.RegisterValueChangedCallback(e => target.UpdateRotation = e.newValue);
                    }
                    runtimeConfiguration.Add(rotateEventWhenBraking);

                    Foldout listGroup = new Foldout();
                    {
                        listGroup.text = "Steering Behaviours";
                        listGroup.tooltip = "Current steering behaviours that determines the movement of this agent.";

                        list = null;
                        list = new ListView(new SteeringBehavioursList(target), 70, CreateElement, (element, index) =>
                        {
                            (ISteeringBehaviour Behaviour, float Strength) item = target.allSteeringBehaviours[index];

                            Button remove = element.Q<Button>("Remove");
                            ButtonData data = (ButtonData)remove.userData;
                            data.Target = target;
                            data.Index = index;
                            data.List = list;

                            FloatField strengthField = element.Q<FloatField>("Strength");
                            strengthField.value = item.Strength;
                            strengthField.RegisterValueChangedCallback(e => target.allSteeringBehaviours[index].Strength = e.newValue);

                            VisualElement managed = element.Q<VisualElement>("Managed");
                            Label managedLabel = element.Q<Label>("Managed Type");

                            ObjectField behaviourField = element.Q<ObjectField>("Behaviour");
                            ISteeringBehaviour behaviour = item.Behaviour;
                            switch (behaviour)
                            {
                                case null:
                                    behaviourField.value = null;
                                    managed.style.display = DisplayStyle.None;
                                    managedLabel.userData = null;
                                    break;
                                case UnityObject unityObject:
                                    behaviourField.value = unityObject;
                                    behaviourField.style.display = DisplayStyle.Flex;
                                    managed.style.display = DisplayStyle.None;
                                    managedLabel.userData = null;
                                    break;
                                default:
                                    behaviourField.value = null;
                                    behaviourField.style.display = DisplayStyle.None;
                                    if (managedLabel.userData != behaviour)
                                    {
                                        managedLabel.userData = behaviour;
                                        managedLabel.text = behaviour.ToString();
                                    }
                                    managed.style.display = DisplayStyle.Flex;
                                    break;
                            }
                        });
                        {
                            list.tooltip = "Current steering behaviours that determines the movement of this agent.";
#if UNITY_2020_1_OR_NEWER
                            list.showBoundCollectionSize = true;
                            list.showBorder = true;
                            list.showAlternatingRowBackgrounds = AlternatingRowBackground.All;
#endif
                            list.style.flexGrow = 1;
                        }
                        listGroup.Add(list);

                        ObjectField steeringBehavourToAdd = new ObjectField("New Steering Behaviour");
                        {
                            steeringBehavourToAdd.tooltip = $"Drag an script here to add it to the agent.\nObject must implement {typeof(ISteeringBehaviour)} and not be already included as a behaviour of this agent.";
                            steeringBehavourToAdd.objectType = typeof(ISteeringBehaviour);
                            steeringBehavourToAdd.RegisterValueChangedCallback(e =>
                            {
                                UnityObject newValue = e.newValue;
                                if (newValue == null)
                                    return;
                                if (newValue is ISteeringBehaviour behaviour)
                                {
                                    for (int i = 0; i < target.steeringBehavioursCount; i++)
                                    {
                                        (ISteeringBehaviour Behaviour, float Strength) tuple = target.allSteeringBehaviours[i];
                                        if (tuple.Behaviour == behaviour)
                                            goto end;
                                    }
                                    target.SetSteeringBehaviour(behaviour, 1);
#if UNITY_2021_2_OR_NEWER
                            list.Rebuild();
#else
                                    list.Refresh();
#endif
                                end:
                                    steeringBehavourToAdd.SetValueWithoutNotify(null);
                                }
                            });
                        }
                        listGroup.Add(steeringBehavourToAdd);
                    }
                    runtimeConfiguration.Add(listGroup);
                }
                root.Add(runtimeConfiguration);

                root.schedule.Execute(() =>
                {
                    bool isPlaying = Application.IsPlaying(target);

                    runtimeConfiguration.style.display = isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
                    list.style.display = isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
                    initialSteeringBehaviours.SetEnabled(!isPlaying);

                    updateMovement.value = target.UpdateMovement;
                    updateRotation.value = target.UpdateRotation;
                    brake.value = target.Brake;
                    rotateEventWhenBraking.value = target.RotateEvenWhenBraking;

                    list.style.height = Math.Min(target.steeringBehavioursCount * 70, 200) + 5;
                }).Every(1000 / 60);
            }
            return root;
        }

        private sealed class ButtonData
        {
            public NavigationAgentRigidbody Target;
            public int Index;
            public ListView List;
        }

        private sealed class SteeringBehavioursList : IList
        {
            private NavigationAgentRigidbody target;

            public SteeringBehavioursList(NavigationAgentRigidbody target) => this.target = target;

            public object this[int index]
            {
                get => target.allSteeringBehaviours[index];
                set => target.allSteeringBehaviours[index] = ((ISteeringBehaviour Behaviour, float Strength))value;
            }

            public bool IsFixedSize => throw new NotImplementedException();

            public bool IsReadOnly => false;

            public int Count => target.steeringBehavioursCount;

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
