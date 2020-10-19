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

            int top = 0;
            if (octantsCount > 0)
                Serialize(0, 0, ref top);

            void Serialize(int oldIndex, int newIndex, ref int fronteer)
            {
                InnerOctant octant = octants[oldIndex];
                Debug.Assert(octant.ChildrenStartAtIndex != 3);
                if (octant.IsLeaf || octant.IsIntransitable)
                    serializedOctans[newIndex].ChildrenStartAtIndex = octant.ChildrenStartAtIndex;
                else
                {
                    int newChildrenStartAtIndex = ++fronteer;
                    serializedOctans[newIndex].ChildrenStartAtIndex = newChildrenStartAtIndex;
                    fronteer += 7;

                    int oldChildrenStartAtIndex = octant.ChildrenStartAtIndex;

                    Serialize(oldChildrenStartAtIndex++, newChildrenStartAtIndex++, ref fronteer);
                    Serialize(oldChildrenStartAtIndex++, newChildrenStartAtIndex++, ref fronteer);
                    Serialize(oldChildrenStartAtIndex++, newChildrenStartAtIndex++, ref fronteer);
                    Serialize(oldChildrenStartAtIndex++, newChildrenStartAtIndex++, ref fronteer);
                    Serialize(oldChildrenStartAtIndex++, newChildrenStartAtIndex++, ref fronteer);
                    Serialize(oldChildrenStartAtIndex++, newChildrenStartAtIndex++, ref fronteer);
                    Serialize(oldChildrenStartAtIndex++, newChildrenStartAtIndex++, ref fronteer);
                    Serialize(oldChildrenStartAtIndex, newChildrenStartAtIndex, ref fronteer);
                }
            }

            Debug.Assert(octantsCount == (top + 1));
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