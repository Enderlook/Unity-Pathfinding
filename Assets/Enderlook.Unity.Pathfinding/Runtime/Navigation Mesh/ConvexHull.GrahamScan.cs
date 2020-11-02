using NET5.System;

using System;
using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal static partial class ConvexHull
    {
        private static class GrahamScan
        {
            public static void CalculateConvexHull(Span<Vector2> points, List<Vector2> output)
            {
                if (output is null)
                    throw new ArgumentNullException(nameof(output));

                output.Clear();

                int n = points.Length;
                if (n <= 1)
                    return;

                Vector2 c = points[FindBottomostPoint(points)];
                points.Sort(new Comparer(c));

                int j = 0;
                for (int i = 0; i < n; ++i)
                    KeepLeft(output, 0, ref j, points[i]);

                points.Reverse();

                int k = 0;
                for (int i = 0; i < n; ++i)
                    KeepLeft(output, j, ref k, points[i]);
            }

            private static void KeepLeft(List<Vector2> v, int start, ref int index, Vector2 p)
            {
                while (index > 1 && GetOrientation(v[start + index - 2], v[start + index - 1], p) != OrientationType.LeftTurn)
                    v.RemoveAt(start + index-- - 1);
                if (index == 0 || v[start + index - 1] != p)
                {
                    v.Add(p);
                    index++;
                }
            }
        }
    }
}