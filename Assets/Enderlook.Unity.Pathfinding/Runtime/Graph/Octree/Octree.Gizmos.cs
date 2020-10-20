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
            if (octants is null || octantsCount == 0 || octants.Length == 0)
                return;

            DrawGizmosChild(0, size);

            void DrawGizmosChild(int index, float size)
            {
                InnerOctant octant = octants[index];

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
                    Gizmos.DrawWireCube(octant.Center, Vector3.one * size);
                }

                if (!octant.HasChildren)
                    return;

                int childrenStartAtIndex = octant.ChildrenStartAtIndex;

                size /= 2;

                DrawGizmosChild(childrenStartAtIndex++, size);
                DrawGizmosChild(childrenStartAtIndex++, size);
                DrawGizmosChild(childrenStartAtIndex++, size);
                DrawGizmosChild(childrenStartAtIndex++, size);
                DrawGizmosChild(childrenStartAtIndex++, size);
                DrawGizmosChild(childrenStartAtIndex++, size);
                DrawGizmosChild(childrenStartAtIndex++, size);
                DrawGizmosChild(childrenStartAtIndex++, size);
            }
        }
    }
}