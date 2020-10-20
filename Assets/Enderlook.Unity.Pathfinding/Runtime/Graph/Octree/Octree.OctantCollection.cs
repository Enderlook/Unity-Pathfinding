using System;
using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree
    {
        private class OctantCollection
        {
            private InnerOctant[] array;
            public int Count { get; private set; }

            /// <summary>
            /// Each index in this list represent 8 contiguos free octans in the <see cref="array"/> array.
            /// </summary>
            private Stack<int> freeOctanRegions = new Stack<int>();

            public ref InnerOctant this[int index] => ref array[index];

            public OctantCollection() => array = Array.Empty<InnerOctant>();

            private OctantCollection(int count)
            {
                array = new InnerOctant[count];
                Count = count;
            }

            public static OctantCollection WithCount(int count) => new OctantCollection(count);

            public void Clear()
            {
                Array.Clear(array, 0, array.Length);
                freeOctanRegions.Clear();
                Count = 0;
            }

            public void SetRoot(InnerOctant octant)
            {
                Debug.Assert(Count == 0);
                if (array.Length < 9)
                    Array.Resize(ref array, 9);
                array[0] = octant;
                Count = 1;
            }

            public void Free8ConsecutiveOctants(int indexOfFirstOctant)
            {
                Debug.Assert(indexOfFirstOctant > 0, indexOfFirstOctant);
                freeOctanRegions.Push(indexOfFirstOctant);
                int to = indexOfFirstOctant + 8;
                for (int i = indexOfFirstOctant; i < to; i++)
                    array[i] = default;
            }

            public int Allocate8ConsecutiveOctans()
            {
                if (freeOctanRegions.TryPop(out int index))
                    return index;

                EnsureAdditionalCapacity(8);
                index = Count;
                Count += 8;
                return index;
            }

            private void EnsureAdditionalCapacity(int additional)
            {
                Debug.Assert(additional > 0);
                int required = Count + additional;
                if (required <= array.Length)
                    return;

                int newSize = (array.Length * GROW_ARRAY_FACTOR_MULTIPLICATIVE) + GROW_ARRAY_FACTOR_ADDITIVE;
                if (newSize < required)
                    newSize = required;

                Array.Resize(ref array, newSize);
            }
        }
    }
}