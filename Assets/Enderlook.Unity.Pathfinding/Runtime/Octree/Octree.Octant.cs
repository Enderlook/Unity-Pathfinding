using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree
    {
        [System.Diagnostics.DebuggerDisplay("{Code} {Center} {Flags}")]
        private partial struct Octant
        {
            public StatusFlags Flags;

            public OctantCode Code;

            public Vector3 Center;

            public bool IsIntransitable {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (Flags & StatusFlags.IsIntransitable) != 0;
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set {
                    if (value)
                        Flags |= StatusFlags.IsIntransitable;
                    else
                        Flags &= StatusFlags.IsIntransitable;
                }
            }

            public bool IsLeaf {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (Flags & StatusFlags.IsLeaf) != 0;
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set {
                    if (value)
                        Flags |= StatusFlags.IsLeaf;
                    else
                        Flags &= StatusFlags.IsLeaf;
                }
            }

            public int Depth {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Code.Depth;
            }

            public OctantCode Parent {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Code.Parent;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public OctantCode GetChildTh(uint th) => Code.GetChildTh(th);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsMaxDepth(uint maxDepth) => Code.IsMaxDepth(maxDepth);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float GetSize(float rootSize) => Code.GetSize(rootSize);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Contains(Vector3 position, float rootSize)
            {
                float size = GetSize(rootSize) * .5f;

                float xMin = Center.x - size;
                float xMax = Center.x + size;

                float yMin = Center.y - size;
                float yMax = Center.y + size;

                float zMax = Center.z + size;
                float zMin = Center.z - size;

                return xMin <= position.x && position.x <= xMax &&
                       yMin <= position.y && position.y <= yMax &&
                       zMin <= position.z && position.z <= zMax;
            }

            public Octant(OctantCode code, Vector3 center)
            {
                Center = center;
                Code = code;
                Flags = default;
            }

            public Octant(OctantCode code, Vector3 center, StatusFlags flags)
            {
                Center = center;
                Code = code;
                Flags = flags;
            }

            public Octant(OctantCode code, StatusFlags flags)
            {
                Code = code;
                Flags = flags;
                Center = default;
            }

            public Octant(OctantCode code)
            {
                Code = code;
                Center = default;
                Flags = default;
            }
        }
    }
}