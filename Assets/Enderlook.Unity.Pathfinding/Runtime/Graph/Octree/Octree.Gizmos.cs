using System;

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
        }
#endif

        internal void DrawGizmos()
        {
            if (octants is null || octants.Count == 0)
                return;

            DrawGizmosChild(new LocationCode(1));

            void DrawGizmosChild(LocationCode code)
            {
                if (!octants.TryGetValue(code, out InnerOctant octant))
                    return;

                bool draw = false;

                if ((drawMode & DrawMode.Transitable) != 0 && !octant.IsIntransitable)
                {
                    Gizmos.color = Color.green;
                    draw = true;
                }
                else if ((drawMode & DrawMode.Intransitable) != 0 && octant.IsIntransitable)
                {
                    Gizmos.color = Color.red;
                    draw = true;
                }

                if (draw)
                {
                    Gizmos.color = octant.IsIntransitable ? Color.red : Color.blue;
                    Gizmos.DrawWireCube(octant.Center, Vector3.one * code.GetSize(size));
                }

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