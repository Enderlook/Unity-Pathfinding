using System;
using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal static partial class ConvexHull
    {
        public static void CalculateConvexHull(Span<Vector2> points, List<Vector2> output)
            => GrahamScan.CalculateConvexHull(points, output);

        internal static int FindBottomostPoint(Span<Vector2> P)
        {
            int n = P.Length;
            int min = 0;
            float yMin = P[0].y;
            float xMin = P[0].x;
            for (int i = 1; i < n; i++)
            {
                float y = P[i].y;
                float x = P[i].x;

                // Pick the bottom-most or chose the left most point in case of tie 
                if ((y < yMin) || (yMin == y && x < xMin))
                {
                    yMin = y;
                    xMin = x;
                    min = i;
                }
            }
            return min;
        }

        private struct Comparer : IComparer<Vector2>
        {
            private readonly Vector2 p0;

            public Comparer(Vector2 p0) => this.p0 = p0;

            public int Compare(Vector2 p1, Vector2 p2)
            {
                OrientationType orientation = GetOrientation(p0, p1, p2);
                if (orientation == OrientationType.Collinear)
                    return (Vector2.Distance(p0, p2) >= Vector2.Distance(p0, p1)) ? -1 : 1;
                return (orientation == OrientationType.CounterClockwise) ? -1 : 1;
            }
        }

        private static OrientationType GetOrientation(Vector2 p0, Vector2 p1, Vector2 p2)
        {
            float value = ((p1.y - p0.y) * (p2.x - p1.x)) - ((p1.x - p0.x) * (p2.y - p1.y));
            if (value == 0)
                return OrientationType.Collinear;
            if (value > 0)
                return OrientationType.Clockwise;
            return OrientationType.CounterClockwise;
        }
    }
}