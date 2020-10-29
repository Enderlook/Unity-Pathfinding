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

            public bool HasGround {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (Flags & StatusFlags.HasGround) != 0;
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set {
                    if (value)
                        Flags |= StatusFlags.HasGround;
                    else
                        Flags &= StatusFlags.HasGround;
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
                Vector3 difference = Center - position;
                float size = GetSize(rootSize) * .5f;
                return (Mathf.Abs(difference.x) < size) && (Mathf.Abs(difference.y) < size) && (Mathf.Abs(difference.z) < size);
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