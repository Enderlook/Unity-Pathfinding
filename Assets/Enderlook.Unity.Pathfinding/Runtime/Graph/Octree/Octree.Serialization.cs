using System;
using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [Serializable]
    internal sealed partial class Octree : ISerializationCallbackReceiver
    {
        [SerializeField]
        private int[] serializedOctantsRaw;

#if UNITY_EDITOR
        /// <summary>
        /// Only use in Editor.
        /// </summary>
        internal int SerializedOctansCount => serializedOctantsRaw.Length;
#endif

        [Serializable]
        private struct SerializableOctant
        {
            //  0 -> Completely transitable but has no children
            // -1 -> Intransitable Leaf
            // -2 -> Intransitable Non-Leaf (all its children are intransitable)
            // ChildrenStartAtIndex
            [SerializeField]
            private int i; // Use a short name to reduce serialization size

            public int ChildrenStartAtIndex {
                get => i;
                set => i = value;
            }

            public bool IsLeaf => ChildrenStartAtIndex == 0 || ChildrenStartAtIndex == -1;

            public bool IsIntransitable => ChildrenStartAtIndex == -1 || ChildrenStartAtIndex == -2;

            public SerializableOctant(int childrenStartAtIndex) => i = childrenStartAtIndex;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            if (octantsCount == 0)
            {
                serializedOctantsRaw = Array.Empty<int>();
                return;
            }

            if (serializedOctantsRaw is null)
                serializedOctantsRaw = new int[octantsCount];
            else
                Array.Resize(ref serializedOctantsRaw, octantsCount);

            unsafe
            {
                fixed (int* serializedOctansRawPointer = serializedOctantsRaw)
                {
                    SerializableOctant* serializedOctants = (SerializableOctant*)serializedOctansRawPointer;
                    int fronteer = 0;
                    int stackLenght = (subdivisions * 8) + 2;
                    OnBeforeSerializeFrame* stackFrame = stackalloc OnBeforeSerializeFrame[stackLenght];
                    stackFrame[0] = new OnBeforeSerializeFrame(0, 0);
                    int stackPointer = 0;

                    while (stackPointer >= 0)
                    {
                        OnBeforeSerializeFrame frame = stackFrame[stackPointer];
                        InnerOctant octant = octants[frame.OldIndex];
                        Debug.Assert(octant.ChildrenStartAtIndex != 3);
                        Debug.Assert(serializedOctantsRaw.Length > frame.NewIndex);
                        if (octant.IsLeaf || octant.IsIntransitable)
                        {
                            stackPointer--;
                            serializedOctants[frame.NewIndex].ChildrenStartAtIndex = octant.ChildrenStartAtIndex;
                        }
                        else
                        {
                            int newChildrenStartAtIndex = ++fronteer;
                            serializedOctants[frame.NewIndex].ChildrenStartAtIndex = newChildrenStartAtIndex;
                            fronteer += 7;

                            int oldChildrenStartAtIndex = octant.ChildrenStartAtIndex;

                            Debug.Assert(stackPointer + 7 < stackLenght);
                            stackFrame[stackPointer++] = new OnBeforeSerializeFrame(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                            stackFrame[stackPointer++] = new OnBeforeSerializeFrame(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                            stackFrame[stackPointer++] = new OnBeforeSerializeFrame(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                            stackFrame[stackPointer++] = new OnBeforeSerializeFrame(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                            stackFrame[stackPointer++] = new OnBeforeSerializeFrame(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                            stackFrame[stackPointer++] = new OnBeforeSerializeFrame(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                            stackFrame[stackPointer++] = new OnBeforeSerializeFrame(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                            stackFrame[stackPointer] = new OnBeforeSerializeFrame(oldChildrenStartAtIndex, newChildrenStartAtIndex);
                        }
                    }

                    Debug.Assert(octantsCount == (fronteer + 1));
                }
            }
        }

        private readonly struct OnBeforeSerializeFrame
        {
            public readonly int OldIndex;
            public readonly int NewIndex;

            public OnBeforeSerializeFrame(int oldIndex, int newIndex)
            {
                OldIndex = oldIndex;
                NewIndex = newIndex;
            }
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            freeOctanRegions = new Stack<int>();

            if (serializedOctantsRaw is null)
            {
                octants = Array.Empty<InnerOctant>();
                octantsCount = 0;
            }
            else
            {
                unsafe
                {
                    fixed (int* serializedOctansRawPointer = serializedOctantsRaw)
                    {
                        SerializableOctant* serializedOctants = (SerializableOctant*)serializedOctansRawPointer;
                        octantsCount = serializedOctantsRaw.Length;
                        octants = new InnerOctant[octantsCount];
                        for (int i = 0; i < octantsCount; i++)
                            octants[i] = new InnerOctant(serializedOctants[i].ChildrenStartAtIndex);

                        int stackLenght = (subdivisions * 8) + 2;
                        OnAfterDeserializeFrame* stackFrame = stackalloc OnAfterDeserializeFrame[stackLenght];
                        stackFrame[0] = new OnAfterDeserializeFrame(0, center);
                        int stackPointer = 0;

                        while (stackPointer >= 0)
                        {
                            OnAfterDeserializeFrame frame = stackFrame[stackPointer];

                            octants[frame.Index].Center = center;
                            InnerOctant octant = octants[frame.Index];

                            if (octant.IsLeaf || octant.IsIntransitable)
                            {
                                stackPointer--;
                                return;
                            }

                            int childrenStartAtIndex = octant.ChildrenStartAtIndex;

                            Debug.Assert(stackPointer + 7 < stackLenght);
                            stackFrame[stackPointer++] = new OnAfterDeserializeFrame(childrenStartAtIndex++, center + (DirectionsHelper.Dir0 * size * .5f));
                            stackFrame[stackPointer++] = new OnAfterDeserializeFrame(childrenStartAtIndex++, center + (DirectionsHelper.Dir1 * size * .5f));
                            stackFrame[stackPointer++] = new OnAfterDeserializeFrame(childrenStartAtIndex++, center + (DirectionsHelper.Dir2 * size * .5f));
                            stackFrame[stackPointer++] = new OnAfterDeserializeFrame(childrenStartAtIndex++, center + (DirectionsHelper.Dir3 * size * .5f));
                            stackFrame[stackPointer++] = new OnAfterDeserializeFrame(childrenStartAtIndex++, center + (DirectionsHelper.Dir4 * size * .5f));
                            stackFrame[stackPointer++] = new OnAfterDeserializeFrame(childrenStartAtIndex++, center + (DirectionsHelper.Dir5 * size * .5f));
                            stackFrame[stackPointer++] = new OnAfterDeserializeFrame(childrenStartAtIndex++, center + (DirectionsHelper.Dir6 * size * .5f));
                            stackFrame[stackPointer] = new OnAfterDeserializeFrame(childrenStartAtIndex, center + (DirectionsHelper.Dir7 * size * .5f));
                        }
                    }
                }
            }
        }

        public readonly struct OnAfterDeserializeFrame
        {
            public readonly int Index;
            public readonly Vector3 Center;

            public OnAfterDeserializeFrame(int index, Vector3 center)
            {
                Index = index;
                Center = center;
            }
        }
    }
}
