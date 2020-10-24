using System;
using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree
    {
        private Dictionary<OctantCode, HashSet<OctantCode>> connections;

#if UNITY_EDITOR
        /// <summary>
        /// Only use in Editor.
        /// </summary>
        internal int NeighboursCount {
            get {
                if (connections is null)
                    return 0;
                int edgesCount = 0;
                foreach (HashSet<OctantCode> edges in connections.Values)
                    edgesCount += edges.Count;
                return edgesCount;
            }
        }
#endif

        internal void CalculateConnections(ConnectionType connectionType)
        {
            // TODO: replace this with http://www.cs.jhu.edu/~misha/ReadingSeminar/Papers/Lewiner10.pdf

            isSerializationUpdated = false;

            Stack<HashSet<OctantCode>> pool;
            if (connections is null)
            {
                connections = new Dictionary<OctantCode, HashSet<OctantCode>>();
                pool = new Stack<HashSet<OctantCode>>();
            }
            else
            {
                pool = new Stack<HashSet<OctantCode>>(connections.Values);
                connections.Clear();
            }

            // We calcualte all true leaf octants (deepest), even from leaves that we don't store.
            // For each leaf, we traverse the parent hierarchy until find an octant which we store.
            // Then we calculate the position of the vertices of those octans.
            // Finally we create connection between those octants which shares a vertex position.

            Span<CalculateConnectionsBruteForceFrame> stack = stackalloc CalculateConnectionsBruteForceFrame[8 * subdivisions + 8];
            stack[0] = new CalculateConnectionsBruteForceFrame(OctantCode.Root, Vector3.zero);
            int stackPointer = 0;

            Dictionary<Vector3, HashSet<OctantCode>> positions = new Dictionary<Vector3, HashSet<OctantCode>>();

            while (stackPointer >= 0)
            {
                CalculateConnectionsBruteForceFrame frame = stack[stackPointer];

                OctantCode code = frame.Code;

                // If we reach bottom depth.
                if (code.Depth == MaxDepth)
                {
                    // Find an octant which is stored from its hierarchy
                    while (!octants.ContainsKey(code))
                        code = code.Parent;

                    if (octants[code].IsIntransitable)
                    {
                        if ((connectionType & ConnectionType.Intransitable) == 0)
                            goto next;
                    }
                    else
                    {
                        if ((connectionType & ConnectionType.Transitable) == 0)
                            goto next;
                    }

                    // Calculate the 8 vertex of this octant and store them in a dictionary:
                    // key: vertex position, value: all octants which has this vertex.
                    float currentSize = code.GetSize(this.size) * .5f;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 vertex = octants[code].Center + (ChildrenPositions.Childs[i] * currentSize);
                        if (!positions.TryGetValue(vertex, out HashSet<OctantCode> list))
                        {
                            if (pool.TryPop(out list))
                                list.Clear();
                            else
                                list = new HashSet<OctantCode>();

                            positions.Add(vertex, list);
                        }

                        list.Add(code);
                    }

                    next:
                    stackPointer--;
                    continue;
                }

                float size = code.GetSize(this.size) * .5f;

                stack[stackPointer++] = new CalculateConnectionsBruteForceFrame(code.GetChildTh(0), frame.Center + (ChildrenPositions.Child0 * size));
                stack[stackPointer++] = new CalculateConnectionsBruteForceFrame(code.GetChildTh(1), frame.Center + (ChildrenPositions.Child1 * size));
                stack[stackPointer++] = new CalculateConnectionsBruteForceFrame(code.GetChildTh(2), frame.Center + (ChildrenPositions.Child2 * size));
                stack[stackPointer++] = new CalculateConnectionsBruteForceFrame(code.GetChildTh(3), frame.Center + (ChildrenPositions.Child3 * size));
                stack[stackPointer++] = new CalculateConnectionsBruteForceFrame(code.GetChildTh(4), frame.Center + (ChildrenPositions.Child4 * size));
                stack[stackPointer++] = new CalculateConnectionsBruteForceFrame(code.GetChildTh(5), frame.Center + (ChildrenPositions.Child5 * size));
                stack[stackPointer++] = new CalculateConnectionsBruteForceFrame(code.GetChildTh(6), frame.Center + (ChildrenPositions.Child6 * size));
                stack[stackPointer] = new CalculateConnectionsBruteForceFrame(code.GetChildTh(7), frame.Center + (ChildrenPositions.Child7 * size));
                uint firstChild = code.GetChildTh(0).Code;
            }

            // Create connections between octans which shares a vertex
            foreach (HashSet<OctantCode> octants in positions.Values)
            {
                foreach (OctantCode code in octants)
                {
                    if (!connections.TryGetValue(code, out HashSet<OctantCode> codes))
                    {
                        if (pool.TryPop(out codes))
                            codes.Clear();
                        else
                            codes = new HashSet<OctantCode>();
                        connections.Add(code, codes);
                    }

                    foreach (OctantCode code2 in octants)
                    {
                        if (!octants.Contains(code))
                            continue;

                        if (code == code2)
                            continue;

                        codes.Add(code2);
                    }
                }
                pool.Push(octants);
            }
        }

        private readonly struct CalculateConnectionsBruteForceFrame
        {
            public readonly OctantCode Code;
            public readonly Vector3 Center;

            public CalculateConnectionsBruteForceFrame(OctantCode code, Vector3 center)
            {
                Code = code;
                Center = center;
            }
        }
    }
}