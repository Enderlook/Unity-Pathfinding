using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal static partial class ConvexHull
    {
        public static List<Vector2> GrahamScan(List<Vector2> points)
        {
            if (points.Count <= 1)
                return points;

            Vector2 c = points[FindBottomostPoint(points)];
            points.Sort((a, b) => Compare(c, a, b));
            List<Vector2> lowerHull = new List<Vector2>();
            for (int i = 0; i < points.Count; ++i)
            {
                lowerHull = KeepLeft(lowerHull, points[i]);
            }
            points.Reverse();
            List<Vector2> upperHull = new List<Vector2>();
            for (int i = 0; i < points.Count; ++i)
            {
                upperHull = KeepLeft(upperHull, points[i]);
            }
            for (int i = 1; i < upperHull.Count; ++i)
            {
                lowerHull.Add(upperHull[i]);
            }
            return lowerHull;
        }

        internal static int FindBottomostPoint(List<Vector2> P)
        {
            int n = P.Count;
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


        private static int Compare(Vector2 p0, Vector2 p1, Vector2 p2)
        {
            OrientationType orientation = GetOrientation(p0, p1, p2);
            if (orientation == OrientationType.Collinear)
                return (Vector2.Distance(p0, p2) >= Vector2.Distance(p0, p1)) ? -1 : 1;
            if (orientation == OrientationType.CounterClockwise)
                return -1;
            return 1;
        }

        private static List<Vector2> KeepLeft(List<Vector2> v, Vector2 p)
        {
            while (v.Count > 1 && GetOrientation(v[v.Count - 2], v[v.Count - 1], p) != OrientationType.LeftTurn)
                v.RemoveAt(v.Count - 1);
            if (v.Count == 0 || v[v.Count - 1] != p)
                v.Add(p);
            return v;
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