using System;
using System.Collections.Generic;

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
            if (serializedOctans is null)
                serializedOctans = new SerializableOctant[octantsCount];
            else if (serializedOctans.Length < octantsCount)
                Array.Resize(ref serializedOctans, octantsCount);

            int freeCount;
            if (freeOctanRegions is null || freeOctanRegions.Count == 0)
            {
                if (octantsCount == 0)
                    return;

                for (int i = 0; i < octantsCount; i++)
                {
                    Debug.Assert(octants[i].ChildrenStartAtIndex != 3);
                    serializedOctans[i].ChildrenStartAtIndex = octants[i].ChildrenStartAtIndex;
                }

                Array.Resize(ref serializedOctans, octantsCount);
            }
            else
            {
                freeCount = freeOctanRegions.Count * 8;
                foreach (int index in freeOctanRegions)
                {
                    int to = index + 8;
                    for (int i = 0; i < to; i++)
                        octants[i].ChildrenStartAtIndex = -3;
                }

                int capacity = octantsCount - freeCount;
                Dictionary<int, int> map = new Dictionary<int, int>(capacity < 0 ? 0 : capacity);
                int j = 0;
                for (int i = 0; i < octantsCount; i++)
                {
                    if (octants[i].ChildrenStartAtIndex != -3)
                        j++;
                    map.Add(i, j);
                }

                j = 0;
                for (int i = 0; i < octantsCount; i++)
                {
                    int oldStart = octants[i].ChildrenStartAtIndex;
                    int start;
                    if (oldStart == -3)
                        continue;
                    if (oldStart < 0)
                        start = oldStart;
                    else
                        start = map[oldStart];

                    serializedOctans[j++].ChildrenStartAtIndex = start;
                }

                Array.Resize(ref serializedOctans, j);
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