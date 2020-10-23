using System;
using System.Buffers.Binary;
using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [Serializable]
    internal sealed partial class Octree : ISerializationCallbackReceiver
    {
        /// <summary>
        /// Data Layout:<br/><br/>
        /// 
        /// Header:<br/>
        /// -(<see cref="int"/>: <c><see cref="octants"/>.Count</c>. Amount of stored octants and body.)<br/>
        /// -(<see cref="int"/>: <c><see cref="connections"/>.Count</c>. Amount of stored connections.)<br/><br/>
        /// 
        /// -Body (repeated an amount of times equal to <c><see cref="octants"/>.Count</c>):<br/>
        /// --(<see cref="OctantCode"/>: <see cref="Octant.Code"/>. Code of this octant.)<br/>
        /// --(<see cref="StatusFlags"/>(<see cref="byte"/>): <see cref="Octant.Flags"/>. Flags of this octant.)<br/>
        /// --(<see cref="int"/>: <c><see cref="connections"/>[<see cref="Octant.Code"/>].Count</c>. Amount of connections this octant has.)<br/>
        /// ---Connections (repeated an amoaunt of times euqal to <c><see cref="connections"/>[<see cref="Octant.Code"/>].Count</c>)<br/>
        /// ----(<see cref="OctantCode"/>: <see cref="Octant.Code"/>. Code of connected octant.)<br/>
        /// </summary>
        [SerializeField]
        private byte[] serialized;

        private bool isSerializationUpdated;

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            if (isSerializationUpdated)
                return;

            isSerializationUpdated = true;

            if (octants is null)
                octants = new Dictionary<OctantCode, Octant>();
            if (connections is null)
                connections = new Dictionary<OctantCode, HashSet<OctantCode>>();

            int edgesCount = 0;
            foreach (HashSet<OctantCode> edges in connections.Values)
                edgesCount += edges.Count;

            serialized = new byte[
                (sizeof(int) * 2) + // Number of octants and number of connections
                ((OctantCode.SIZE + sizeof(byte)) * octants.Count) + // Space for octant information
                (OctantCode.SIZE * edgesCount) + // Space for neighbour information
                (sizeof(int) * octants.Count) // Space to say if an octant has or not neighbours
            ];

            int index = 0;

            BinaryPrimitives.WriteInt32LittleEndian(serialized.AsSpan(index, sizeof(int)), octants.Count);
            index += sizeof(int);

            BinaryPrimitives.WriteInt32LittleEndian(serialized.AsSpan(index, sizeof(int)), connections.Count);
            index += sizeof(int);

            Span<OctantCode> stack = stackalloc OctantCode[(8 * subdivisions) + 20]; // TODO: this value causes to much trouble. Calculate this better.
            stack[0] = OctantCode.Root;
            int stackPointer = 0;
            while (stackPointer >= 0)
            {
                OctantCode code = stack[stackPointer];
                if (!octants.TryGetValue(code, out Octant octant))
                {
                    stackPointer--;
                    continue;
                }

                octant.Code.WriteBytes(serialized.AsSpan(index, OctantCode.SIZE));
                index += OctantCode.SIZE;
                serialized[index++] = (byte)octant.Flags;

                if (!connections.TryGetValue(octant.Code, out HashSet<OctantCode> neighbours))
                {
                    BinaryPrimitives.WriteInt32LittleEndian(serialized.AsSpan(index, sizeof(int)), 0);
                    index += sizeof(int);
                }
                else
                {
                    BinaryPrimitives.WriteInt32LittleEndian(serialized.AsSpan(index, sizeof(int)), neighbours.Count);
                    index += sizeof(int);

                    foreach (OctantCode neighbour in neighbours)
                    {
                        neighbour.WriteBytes(serialized.AsSpan(index, OctantCode.SIZE));
                        index += OctantCode.SIZE;
                    }
                }

                uint firstChild = code.GetChildTh(0).Code;
                stack[stackPointer++] = new OctantCode(firstChild++);
                stack[stackPointer++] = new OctantCode(firstChild++);
                stack[stackPointer++] = new OctantCode(firstChild++);
                stack[stackPointer++] = new OctantCode(firstChild++);
                stack[stackPointer++] = new OctantCode(firstChild++);
                stack[stackPointer++] = new OctantCode(firstChild++);
                stack[stackPointer++] = new OctantCode(firstChild++);
                stack[stackPointer] = new OctantCode(firstChild);
            }
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (isSerializationUpdated)
                return;

            isSerializationUpdated = true;

            if (serialized is null || serialized.Length == 0)
            {
                octants = new Dictionary<OctantCode, Octant>();
                connections = new Dictionary<OctantCode, HashSet<OctantCode>>();
            }
            else
            {
                int index = 0;

                int octantsCount = BinaryPrimitives.ReadInt32LittleEndian(serialized.AsSpan(index, sizeof(int)));
                octants = new Dictionary<OctantCode, Octant>(octantsCount);
                index += sizeof(int);

                connections = new Dictionary<OctantCode, HashSet<OctantCode>>(BinaryPrimitives.ReadInt32LittleEndian(serialized.AsSpan(index, sizeof(int))));
                index += sizeof(int);

                for (int i = 0; i < octantsCount; i++)
                {
                    OctantCode code = new OctantCode(serialized.AsSpan(index, OctantCode.SIZE));
                    index += OctantCode.SIZE;

                    Octant.StatusFlags flags = (Octant.StatusFlags)serialized[index++];

                    octants[code] = new Octant(code, flags);

                    int neigboursCount = BinaryPrimitives.ReadInt32LittleEndian(serialized.AsSpan(index, sizeof(int)));
                    index += sizeof(int);
                    HashSet<OctantCode> neigbours = new HashSet<OctantCode>(); // TODO: In .Net Standard 2.1 we can add initial capacity

                    for (int j = 0; j < neigboursCount; j++)
                    {
                        neigbours.Add(new OctantCode(serialized.AsSpan(index, OctantCode.SIZE)));
                        index += OctantCode.SIZE;
                    }
                    connections.Add(code, neigbours);
                }

                Span<OnAfterDeserialize> stack = stackalloc OnAfterDeserialize[(8 * subdivisions) + 15];
                stack[0] = new OnAfterDeserialize(OctantCode.Root, center);
                int stackPointer = 0;

                while (stackPointer >= 0)
                {
                    OnAfterDeserialize frame = stack[stackPointer];

                    OctantCode code = frame.Code;
                    if (!octants.TryGetValue(code, out Octant octant))
                    {
                        stackPointer--;
                        continue;
                    }

                    Vector3 center = frame.Center;
                    octant.Center = center;
                    octants[code] = octant;

                    float currentSize = code.GetSize(size) / 4;

                    uint firstChild = code.GetChildTh(0).Code;
                    stack[stackPointer++] = new OnAfterDeserialize(new OctantCode(firstChild++), center + (ChildrenPositions.Child0 * currentSize));
                    stack[stackPointer++] = new OnAfterDeserialize(new OctantCode(firstChild++), center + (ChildrenPositions.Child1 * currentSize));
                    stack[stackPointer++] = new OnAfterDeserialize(new OctantCode(firstChild++), center + (ChildrenPositions.Child2 * currentSize));
                    stack[stackPointer++] = new OnAfterDeserialize(new OctantCode(firstChild++), center + (ChildrenPositions.Child3 * currentSize));
                    stack[stackPointer++] = new OnAfterDeserialize(new OctantCode(firstChild++), center + (ChildrenPositions.Child4 * currentSize));
                    stack[stackPointer++] = new OnAfterDeserialize(new OctantCode(firstChild++), center + (ChildrenPositions.Child5 * currentSize));
                    stack[stackPointer++] = new OnAfterDeserialize(new OctantCode(firstChild++), center + (ChildrenPositions.Child6 * currentSize));
                    stack[stackPointer] = new OnAfterDeserialize(new OctantCode(firstChild), center + (ChildrenPositions.Child7 * currentSize));
                }
            }
        }

        private struct OnAfterDeserialize
        {
            public OctantCode Code;
            public Vector3 Center;

            public OnAfterDeserialize(OctantCode code, Vector3 center)
            {
                Code = code;
                Center = center;
            }
        }
    }
}
