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

        private void CalculateConnectionsBruteForce()
        {
            // TODO: replace this with http://www.cs.jhu.edu/~misha/ReadingSeminar/Papers/Lewiner10.pdf

            if (connections is null)
                connections = new Dictionary<OctantCode, HashSet<OctantCode>>();
            else
                connections.Clear();

            Span<CalculateConnectionsBruteForceFrame> stack = stackalloc CalculateConnectionsBruteForceFrame[8 * subdivisions + 8];
            stack[0] = new CalculateConnectionsBruteForceFrame(new OctantCode(1), Vector3.zero);
            int stackPointer = 0;

            Dictionary<Vector3, HashSet<OctantCode>> positions = new Dictionary<Vector3, HashSet<OctantCode>>();

            while (stackPointer >= 0)
            {
                CalculateConnectionsBruteForceFrame frame = stack[stackPointer];

                OctantCode code = frame.Code;

                if (code.Depth == MaxDepth)
                {
                    while (!octants.ContainsKey(code))
                       code = code.Parent;

                    if (!octants[code].IsIntransitable)
                    {
                        float currentSize = code.GetSize(this.size) * .5f;
                        for (int i = 0; i < 8; i++)
                        {
                            Vector3 vertex = octants[code].Center + (ChildrenPositions.Childs[i] * currentSize);
                            if (!positions.TryGetValue(vertex, out HashSet<OctantCode> list))
                            {
                                list = new HashSet<OctantCode>();
                                positions.Add(vertex, list);
                            }

                            list.Add(code);
                        }
                    }

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
                stack[stackPointer  ] = new CalculateConnectionsBruteForceFrame(code.GetChildTh(7), frame.Center + (ChildrenPositions.Child7 * size));
                uint firstChild = code.GetChildTh(0).Code;
            }

            if (connections is null)
                connections = new Dictionary<OctantCode, HashSet<OctantCode>>();
            else
                connections.Clear();

            foreach (HashSet<OctantCode> octants in positions.Values)
            {
                foreach (OctantCode code in octants)
                {
                    if (!connections.TryGetValue(code, out HashSet<OctantCode> codes))
                    {
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

        private Dictionary<VertexCode, int> GetVerticesAndDepth()
        {
            Dictionary<VertexCode, int> verticesAndDepth = new Dictionary<VertexCode, int>();

            foreach (InnerOctant octant in octants.Values)
            {
                if (!octant.IsLeaf)
                    continue;

                octant.Code.GetVertexCodes(verticesAndDepth);
            }

            return verticesAndDepth;
        }

        private void GetOctantLeaves(Dictionary<VertexCode, int> verticesAndDepth, Span<OctantCode> octantCodesGroupes)
        {
            Debug.Assert(octantCodesGroupes.Length == verticesAndDepth.Count * 8);

            int index = 0;
            foreach (KeyValuePair<VertexCode, int> pair in verticesAndDepth)
            {
                Span<OctantCode> slice = octantCodesGroupes.Slice(index, 8);
                pair.Key.GetLeavesCode(pair.Value, slice, MaxDepth);
                foreach (OctantCode code in slice)
                {
                    OctantCode code_ = code;

                    while (!octants.ContainsKey(code_))
                        code_ = code_.Parent;

                    octantCodesGroupes[index++] = code_;
                }
            }
        }

        private void CalculateConnections()
        {
            Dictionary<VertexCode, int> verticesAndDepth = GetVerticesAndDepth();

            int count = verticesAndDepth.Count * 8;

            Span<OctantCode> octantCodes = (count * OctantCode.SIZE) > 500 ? new OctantCode[count] : stackalloc OctantCode[count];

            GetOctantLeaves(verticesAndDepth, octantCodes);

            if (connections is null)
                connections = new Dictionary<OctantCode, HashSet<OctantCode>>(verticesAndDepth.Count * 8);
            else
                connections.Clear();

            for (int i = 0; i < count; i += 8)
            {
                int iTo = i + 8;
                for (int j = i; j < iTo; j++)
                {
                    if (!connections.TryGetValue(octantCodes[i], out HashSet<OctantCode> to))
                    {
                        to = new HashSet<OctantCode>(); // TODO: In .Net Standard 2.1 add 8 as initial capacity.
                        connections[octantCodes[i]] = to;
                    }
                    else
                    {
                        for (int k = i; k < j; k++)
                            to.Add(octantCodes[k]);

                        for (int k = j + 1; k < iTo; k++)
                            to.Add(octantCodes[k]);
                    }
                }
            }
        }
    }
}