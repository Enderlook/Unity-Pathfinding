using System;

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
            if (octants is null || octants.Count == 0)
            {
                serializedOctantsRaw = Array.Empty<int>();
                return;
            }

            if (serializedOctantsRaw is null)
                serializedOctantsRaw = new int[octants.Count];
            else
                Array.Resize(ref serializedOctantsRaw, octants.Count);

            unsafe
            {
                fixed (int* serializedOctansRawPointer = serializedOctantsRaw)
                {
                    SerializableOctant* serializedOctants = (SerializableOctant*)serializedOctansRawPointer;
                    int fronteer = 0;
                    int stackLength = (subdivisions * 8) + 2;
                    OnBeforeSerializeFrame* stackFrame = stackalloc OnBeforeSerializeFrame[stackLength];
                    stackFrame[0] = new OnBeforeSerializeFrame(0, 0);
                    int stackPointer = 0;

                    while (stackPointer >= 0)
                    {
                        OnBeforeSerializeFrame frame = stackFrame[stackPointer];
                        ref InnerOctant octant = ref octants[frame.OldIndex];
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

                            Debug.Assert(stackPointer + 7 < stackLength);
                            stackFrame[stackPointer++] = new OnBeforeSerializeFrame(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                            stackFrame[stackPointer++] = new OnBeforeSerializeFrame(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                            stackFrame[stackPointer++] = new OnBeforeSerializeFrame(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                            stackFrame[stackPointer++] = new OnBeforeSerializeFrame(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                            stackFrame[stackPointer++] = new OnBeforeSerializeFrame(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                            stackFrame[stackPointer++] = new OnBeforeSerializeFrame(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                            stackFrame[stackPointer++] = new OnBeforeSerializeFrame(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                            stackFrame[stackPointer  ] = new OnBeforeSerializeFrame(oldChildrenStartAtIndex  , newChildrenStartAtIndex);
                        }
                    }

                    Debug.Assert(octants.Count == (fronteer + 1));
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
            if (serializedOctantsRaw is null || serializedOctantsRaw.Length == 0)
                octants = new OctantCollection();
            else
            {
                octants = OctantCollection.WithCount(serializedOctantsRaw.Length);
                unsafe
                {
                    fixed (int* serializedOctansRawPointer = serializedOctantsRaw)
                    {
                        SerializableOctant* serializedOctants = (SerializableOctant*)serializedOctansRawPointer;
                        for (int i = 0; i < octants.Count; i++)
                            octants[i] = new InnerOctant(i, serializedOctants[i].ChildrenStartAtIndex);

                        int stackLenght = (subdivisions * 8) + 2;
                        OnAfterDeserializeFrame* stackFrame = stackalloc OnAfterDeserializeFrame[stackLenght];
                        stackFrame[0] = new OnAfterDeserializeFrame(0, center, size / 2, Vector3Int.zero);
                        int stackPointer = 0;

                        while (stackPointer >= 0)
                        {
                            ref OnAfterDeserializeFrame frame = ref stackFrame[stackPointer];

                            octants[frame.Index].Center = frame.Center;
                            InnerOctant octant = octants[frame.Index];

                            if (octant.IsLeaf || octant.IsIntransitable)
                            {
                                stackPointer--;
                                continue;
                            }

                            int childrenStartAtIndex = octant.ChildrenStartAtIndex;

                            float halfSize = frame.Size * .5f;

                            Debug.Assert(stackPointer + 7 < stackLenght);
                            Vector3 center = frame.Center;
                            stackFrame[stackPointer++] = new OnAfterDeserializeFrame(childrenStartAtIndex++, center + (ChildrenPositions.Child0 * halfSize), halfSize, frame.Position + ChildrenPositions.ChildZ0);
                            stackFrame[stackPointer++] = new OnAfterDeserializeFrame(childrenStartAtIndex++, center + (ChildrenPositions.Child1 * halfSize), halfSize, frame.Position + ChildrenPositions.ChildZ1);
                            stackFrame[stackPointer++] = new OnAfterDeserializeFrame(childrenStartAtIndex++, center + (ChildrenPositions.Child2 * halfSize), halfSize, frame.Position + ChildrenPositions.ChildZ2);
                            stackFrame[stackPointer++] = new OnAfterDeserializeFrame(childrenStartAtIndex++, center + (ChildrenPositions.Child3 * halfSize), halfSize, frame.Position + ChildrenPositions.ChildZ3);
                            stackFrame[stackPointer++] = new OnAfterDeserializeFrame(childrenStartAtIndex++, center + (ChildrenPositions.Child4 * halfSize), halfSize, frame.Position + ChildrenPositions.ChildZ4);
                            stackFrame[stackPointer++] = new OnAfterDeserializeFrame(childrenStartAtIndex++, center + (ChildrenPositions.Child5 * halfSize), halfSize, frame.Position + ChildrenPositions.ChildZ5);
                            stackFrame[stackPointer++] = new OnAfterDeserializeFrame(childrenStartAtIndex++, center + (ChildrenPositions.Child6 * halfSize), halfSize, frame.Position + ChildrenPositions.ChildZ6);
                            stackFrame[stackPointer  ] = new OnAfterDeserializeFrame(childrenStartAtIndex  , center + (ChildrenPositions.Child7 * halfSize), halfSize, frame.Position + ChildrenPositions.ChildZ7);
                        }
                    }
                }
            }
        }

        public readonly struct OnAfterDeserializeFrame
        {
            public readonly int Index;
            public readonly Vector3 Center;
            public readonly float Size;
            public readonly Vector3Int Position;

            public OnAfterDeserializeFrame(int index, Vector3 center, float size, Vector3Int position)
            {
                Index = index;
                Center = center;
                Size = size;
                Position = position;
            }
        }
    }
}
