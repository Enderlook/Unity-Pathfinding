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

            DrawGizmosChild(0, center, size);

            void DrawGizmosChild(int index, Vector3 center, float size)
            {
                InnerOctant node = octants[index];

                bool draw = false;

                if ((drawMode & DrawMode.Transitable) != 0 && !node.IsIntransitable)
                {
                    Gizmos.color = Color.green;
                    draw = true;
                }
                else if ((drawMode & DrawMode.Intransitable) != 0 && node.IsIntransitable)
                {
                    Gizmos.color = Color.red;
                    draw = true;
                }

                if (draw)
                {
                    Gizmos.color = node.IsIntransitable ? Color.red : Color.blue;
                    Gizmos.DrawWireCube(center, Vector3.one * size);
                }

                if (!node.HasChildren)
                    return;

                int childrenStartAtIndex = node.ChildrenStartAtIndex;

                size /= 2;

                DrawGizmosChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir0 * size * .5f), size);
                DrawGizmosChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir1 * size * .5f), size);
                DrawGizmosChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir2 * size * .5f), size);
                DrawGizmosChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir3 * size * .5f), size);
                DrawGizmosChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir4 * size * .5f), size);
                DrawGizmosChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir5 * size * .5f), size);
                DrawGizmosChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir6 * size * .5f), size);
                DrawGizmosChild(childrenStartAtIndex++, center + (DirectionsHelper.Dir7 * size * .5f), size);
            }
        }
    }
}