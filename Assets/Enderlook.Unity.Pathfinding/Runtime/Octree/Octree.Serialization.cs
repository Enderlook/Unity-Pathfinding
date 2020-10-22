using System;
using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [Serializable]
    internal sealed partial class Octree : ISerializationCallbackReceiver
    {
        [SerializeField]
        private byte[] octantsBytes;

        [Serializable]
        private struct SerializableOctant
        {
            public LocationCode Code;

            public InnerOctantFlags Flags;

            public SerializableOctant(LocationCode code, InnerOctantFlags flags)
            {
                Code = code;
                Flags = flags;
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            octantsBytes = new byte[(LocationCode.SIZE + sizeof(byte)) * octants.Count];

            int index = 0;

            Span<LocationCode> stack = stackalloc LocationCode[(8 * subdivisions) + 9];
            stack[0] = new LocationCode(1);
            int stackPointer = 0;

            while (stackPointer >= 0)
            {
                LocationCode code = stack[stackPointer];
                if (!octants.TryGetValue(code, out InnerOctant octant))
                {
                    stackPointer--;
                    continue;
                }

                octant.Code.WriteBytes(octantsBytes.AsSpan(index, LocationCode.SIZE));
                index += LocationCode.SIZE;
                octantsBytes[index++] = (byte)octant.Flags;

                stack[stackPointer++] = code.GetChildTh(0);
                stack[stackPointer++] = code.GetChildTh(1);
                stack[stackPointer++] = code.GetChildTh(2);
                stack[stackPointer++] = code.GetChildTh(3);
                stack[stackPointer++] = code.GetChildTh(4);
                stack[stackPointer++] = code.GetChildTh(5);
                stack[stackPointer++] = code.GetChildTh(6);
                stack[stackPointer  ] = code.GetChildTh(7);
            }
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (octantsBytes is null || octantsBytes.Length == 0)
                octants = new Dictionary<LocationCode, InnerOctant>();
            else
            {
                octants = new Dictionary<LocationCode, InnerOctant>(octantsBytes.Length / (LocationCode.SIZE + sizeof(byte)));

                for (int i = 0; i < octantsBytes.Length;)
                {
                    LocationCode code = new LocationCode(octantsBytes.AsSpan(i));
                    i += LocationCode.SIZE;

                    InnerOctantFlags flags = (InnerOctantFlags)octantsBytes[i++];

                    octants[code] = new InnerOctant(code, flags);
                }

                Span<OnAfterDeserialize> stack = stackalloc OnAfterDeserialize[(8 * subdivisions) + 9];
                stack[0] = new OnAfterDeserialize(new LocationCode(1), center);
                int stackPointer = 0;

                while (stackPointer >= 0)
                {
                    OnAfterDeserialize frame = stack[stackPointer];

                    LocationCode code = frame.Code;
                    if (!octants.TryGetValue(code, out InnerOctant octant))
                    {
                        stackPointer--;
                        continue;
                    }

                    Vector3 center = frame.Center;
                    octant.Center = center;
                    octants[code] = octant;

                    float currentSize = code.GetSize(size) / 4;

                    stack[stackPointer++] = new OnAfterDeserialize(code.GetChildTh(0), center + (ChildrenPositions.Child0 * currentSize));
                    stack[stackPointer++] = new OnAfterDeserialize(code.GetChildTh(1), center + (ChildrenPositions.Child1 * currentSize));
                    stack[stackPointer++] = new OnAfterDeserialize(code.GetChildTh(2), center + (ChildrenPositions.Child2 * currentSize));
                    stack[stackPointer++] = new OnAfterDeserialize(code.GetChildTh(3), center + (ChildrenPositions.Child3 * currentSize));
                    stack[stackPointer++] = new OnAfterDeserialize(code.GetChildTh(4), center + (ChildrenPositions.Child4 * currentSize));
                    stack[stackPointer++] = new OnAfterDeserialize(code.GetChildTh(5), center + (ChildrenPositions.Child5 * currentSize));
                    stack[stackPointer++] = new OnAfterDeserialize(code.GetChildTh(6), center + (ChildrenPositions.Child6 * currentSize));
                    stack[stackPointer  ] = new OnAfterDeserialize(code.GetChildTh(7), center + (ChildrenPositions.Child7 * currentSize));
                }
            }
        }

        private struct OnAfterDeserialize
        {
            public LocationCode Code;
            public Vector3 Center;

            public OnAfterDeserialize(LocationCode code, Vector3 center)
            {
                Code = code;
                Center = center;
            }
        }
    }
}
