using System;
using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree
    {
#if UNITY_EDITOR
        /// <summary>
        /// Only use in Editor.
        /// </summary>
        internal DrawMode drawMode = DrawMode.Intransitable | DrawMode.Transitable;

        /// <summary>
        /// Only use in Editor.
        /// </summary>
        [Flags]
        internal enum DrawMode
        {
            Nothing = 0,
            Intransitable = 1 << 0,
            Transitable = 1 << 1,
            Connections = 1 << 2,
        }
#endif

        internal void DrawGizmos()
        {
            if (octants is null || octants.Count == 0)
                return;

            DrawGizmosChild(OctantCode.Root);

            void DrawGizmosChild(OctantCode code)
            {
                if (!octants.TryGetValue(code, out Octant octant))
                    return;

                if ((drawMode & DrawMode.Transitable) != 0 && !octant.IsIntransitable)
                {
                    Gizmos.color = Color.green;
                    goto alfa;
                }
                else if ((drawMode & DrawMode.Intransitable) != 0 && octant.IsIntransitable)
                {
                    Gizmos.color = Color.red;
                    goto alfa;
                }

                goto beta;

                alfa:
                Gizmos.DrawWireCube(octant.Center, Vector3.one * code.GetSize(size));

                if ((drawMode & DrawMode.Connections) != 0 && connections.TryGetValue(octant.Code, out HashSet<OctantCode> neighbours))
                {
                    foreach (OctantCode neighbourCode in neighbours)
                    {
                        Octant neighbour = octants[neighbourCode];

                        if ((drawMode & DrawMode.Transitable) != 0 && !neighbour.IsIntransitable)
                        {
                            Gizmos.color = Color.green;
                            goto gamma;
                        }
                        else if ((drawMode & DrawMode.Intransitable) != 0 && neighbour.IsIntransitable)
                        {
                            Gizmos.color = Color.red;
                            goto gamma;
                        }

                        continue;

                        gamma:
                        Gizmos.DrawLine(octant.Center, neighbour.Center);
                    }
                }

                beta:
                if (octant.IsIntransitable)
                    return;

                DrawGizmosChild(code.GetChildTh(0));
                DrawGizmosChild(code.GetChildTh(1));
                DrawGizmosChild(code.GetChildTh(2));
                DrawGizmosChild(code.GetChildTh(3));
                DrawGizmosChild(code.GetChildTh(4));
                DrawGizmosChild(code.GetChildTh(5));
                DrawGizmosChild(code.GetChildTh(6));
                DrawGizmosChild(code.GetChildTh(7));
            }
        }
    }
}