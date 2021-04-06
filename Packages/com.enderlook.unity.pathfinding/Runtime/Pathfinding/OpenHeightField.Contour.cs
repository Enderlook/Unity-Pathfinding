using Enderlook.Collections.Pooled.LowLevel;

using System;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    /*internal partial struct OpenHeightField
    {
        public void CalculateContour()
        {
            int lengthXZ = resolution.x * resolution.z;

            FindEdges(ref this, lengthXZ);

            void FindEdges(ref OpenHeightField self, int lengthXZ)
            {
                for (int xz = 0; xz < lengthXZ; xz++)
                {
                    Span<HeightSpan> spans = self.columns[xz].AsSpan();
                    for (int y = 0; y < spans.Length; y++)
                    {
                        ref HeightSpan span = ref spans[y];
                        self.CalculateContourCheckNeighbour(ref span, xz, -self.resolution.z, span.Left, HeightSpan.LEFT_FLAG);
                        self.CalculateContourCheckNeighbour(ref span, xz, self.resolution.z, span.Right, HeightSpan.RIGHT_FLAG);
                        self.CalculateContourCheckNeighbour(ref span, xz, -1, span.Backward, HeightSpan.BACKWARD_FLAG);
                        self.CalculateContourCheckNeighbour(ref span, xz, 1, span.Forward, HeightSpan.FORWARD_FLAG);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CalculateContourCheckNeighbour(ref HeightSpan span, int xz, int d, int y, byte flag)
        {
            if (y == HeightSpan.NULL_SIDE)
                return;

            xz += d;
            ref HeightSpan neighbour = ref columns[xz].AsSpan()[y];
            if (span.Region != neighbour.Region)
                span.FlaggedEdges |= flag;
        }
    }

    internal struct Contour
    {
        private RawPooledList<Vector3> vertices;
        private RawPooledList<(int xz, int y)> neighbours;
    }*/
}