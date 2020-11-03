using System;
using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal static partial class ConvexHull
    {
        internal static class ChansAlgorithm
        {
            public static List<Vector2> CalculateConvexHull(Span<Vector2> points)
            {
                int n = points.Length;
                for (int t = 0; t < n; ++t)
                {
                    for (int m = 1; m < (1 << (1 << t)); ++m)
                    {
                        List<List<Vector2>> hulls = new List<List<Vector2>>();
                        for (int i = 0; i < n; i += m)
                        {
                            Span<Vector2> chunk;
                            if (i + m <= n)
                                chunk = points.Slice(i, m);
                            else
                                chunk = points.Slice(i, n - i);
                            hulls.Add(GrahamScan.CalculateConvexHull(chunk));
                        }

                        List<(int hullsIndex, int chunkIndex)> hull = new List<(int hullsIndex, int chunkIndex)>
                        {
                            ExtremeHullPoint(hulls)
                        };

                        for (int i = 0; i < m; ++i)
                        {
                            (int hullsIndex, int chunkIndex) p = NextHullPoint(hulls, hull[hull.Count - 1]);
                            List<Vector2> output = new List<Vector2>();
                            if (p == hull[0])
                            {
                                for (int j = 0; j < hull.Count; ++j)
                                    output.Add(hulls[hull[j].hullsIndex][hull[j].chunkIndex]);
                                return output;
                            }
                            hull.Add(p);
                        }
                    }
                }
                Debug.LogError("Impossible State");
                return null;
            }

            private static (int hullsIndex, int chunkIndex) NextHullPoint(List<List<Vector2>> hulls, (int hullsIndex, int chunkIndex) lPoint)
            {
                Vector2 p = hulls[lPoint.hullsIndex][lPoint.chunkIndex];
                (int hullsIndex, int chunkIndex) next = (lPoint.hullsIndex, (lPoint.chunkIndex + 1) % hulls[lPoint.hullsIndex].Count);
                for (int h = 0; h < hulls.Count; h++)
                {
                    if (h != lPoint.hullsIndex)
                    {
                        int s = Tangent(hulls[h], p);
                        Vector2 q = hulls[next.hullsIndex][next.chunkIndex];
                        Vector2 r = hulls[h][s];
                        OrientationType t = GetOrientation(p, q, r);
                        if (t == OrientationType.RightTurn || (t == OrientationType.Collinear) && Vector2.Distance(p, r) > Vector2.Distance(p, q))
                            next = (h, s);
                    }
                }
                return next;
            }

            private static int Tangent(List<Vector2> v, Vector2 p)
            {
                int l = 0;
                int r = v.Count;
                OrientationType lBefore = GetOrientation(p, v[0], v[v.Count - 1]);
                OrientationType lAfter = GetOrientation(p, v[0], v[(l + 1) % v.Count]);
                while (l < r)
                {
                    int c = ((l + r) >> 1);
                    OrientationType cBefore = GetOrientation(p, v[c], v[(c - 1) % v.Count]);
                    OrientationType cAfter = GetOrientation(p, v[c], v[(c + 1) % v.Count]);
                    OrientationType cSide = GetOrientation(p, v[l], v[c]);
                    if (cBefore != OrientationType.RightTurn && cAfter != OrientationType.RightTurn)
                        return c;
                    else if ((cSide == OrientationType.LeftTurn) && (lAfter == OrientationType.RightTurn || lBefore == lAfter) || (cSide == OrientationType.RightTurn && cBefore == OrientationType.RightTurn))
                        r = c;
                    else
                        l = c + 1;
                    lBefore = ReverseOrientation(cAfter);
                    lAfter = GetOrientation(p, v[l], v[(l + 1) % v.Count]);
                }
                return l;
            }

            private static OrientationType ReverseOrientation(OrientationType orientation) => (OrientationType)(-(int)orientation);

            private static (int hullsIndex, int chunkIndex) ExtremeHullPoint(List<List<Vector2>> hulls)
            {
                int h = 0;
                int p = 0;
                for (int i = 0; i < hulls.Count; ++i)
                {
                    int minIndex = 0;
                    float minY = hulls[i][0].y;
                    for (int j = 1; j < hulls[i].Count; ++j)
                    {
                        if (hulls[i][j].y < minY)
                        {
                            minY = hulls[i][j].y;
                            minIndex = j;
                        }
                    }
                    if (hulls[i][minIndex].y < hulls[h][p].y)
                    {
                        h = i;
                        p = minIndex;
                    }
                }
                return (h, p);
            }
        }
    }
}