using System;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [Serializable]
    internal sealed partial class Octree : ISerializationCallbackReceiver
    {
        [SerializeField]
        private SerializableOctant[] serializedOctans;

#if UNITY_EDITOR
        /// <summary>
        /// Only use in Editor.
        /// </summary>
        internal int SerializedOctansCount => serializedOctans.Length;
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

            public SerializableOctant(int childrenStartAtIndex) => i = childrenStartAtIndex;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            if (octantsCount == 0)
            {
                serializedOctans = Array.Empty<SerializableOctant>();
                return;
            }

            if (serializedOctans is null)
                serializedOctans = new SerializableOctant[octantsCount];
            else
                Array.Resize(ref serializedOctans, octantsCount);

            int fronteer = 0;
            unsafe
            {
                int stackLenght = (subdivisions * 8) + 2;
                IndexTransfer* stack = stackalloc IndexTransfer[stackLenght];
                stack[0] = new IndexTransfer(0, 0);
                int stackPointer = 0;

                while (stackPointer >= 0)
                {
                    IndexTransfer item = stack[stackPointer];
                    int oldIndex = item.OldIndex;
                    int newIndex = item.NewIndex;
                    InnerOctant octant = octants[oldIndex];
                    Debug.Assert(octant.ChildrenStartAtIndex != 3);
                    if (octant.IsLeaf || octant.IsIntransitable)
                    {
                        stackPointer--;
                        serializedOctans[newIndex].ChildrenStartAtIndex = octant.ChildrenStartAtIndex;
                    }
                    else
                    {
                        int newChildrenStartAtIndex = ++fronteer;
                        serializedOctans[newIndex].ChildrenStartAtIndex = newChildrenStartAtIndex;
                        fronteer += 7;

                        int oldChildrenStartAtIndex = octant.ChildrenStartAtIndex;

                        Debug.Assert(stackPointer + 7 < stackLenght);
                        stack[stackPointer++] = new IndexTransfer(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                        stack[stackPointer++] = new IndexTransfer(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                        stack[stackPointer++] = new IndexTransfer(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                        stack[stackPointer++] = new IndexTransfer(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                        stack[stackPointer++] = new IndexTransfer(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                        stack[stackPointer++] = new IndexTransfer(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                        stack[stackPointer++] = new IndexTransfer(oldChildrenStartAtIndex++, newChildrenStartAtIndex++);
                        stack[stackPointer] = new IndexTransfer(oldChildrenStartAtIndex, newChildrenStartAtIndex);
                    }
                }
            }

            Debug.Assert(octantsCount == (fronteer + 1));
        }

        private readonly struct IndexTransfer
        {
            public readonly int OldIndex;
            public readonly int NewIndex;

            public IndexTransfer(int oldIndex, int newIndex)
            {
                OldIndex = oldIndex;
                NewIndex = newIndex;
            }
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (serializedOctans is null)
            {
                octants = Array.Empty<InnerOctant>();
                octantsCount = 0;
            }
            else
            {
                octantsCount = serializedOctans.Length;
                octants = new InnerOctant[octantsCount];
                for (int i = 0; i < octantsCount; i++)
                    octants[i] = new InnerOctant(serializedOctans[i].ChildrenStartAtIndex);
            }
        }
    }
}