using System;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree
    {
        [Flags]
        public enum InnerOctantFlags : byte
        {
            /// <summary>
            /// Determines that the octant can't be traversable.
            /// </summary>
            IsIntransitable = 1 << 0,

            /// <summary>
            /// Determines that the octant deosn't have any child.<br/>
            /// This can be applied to branch nodes to determine that are leaf nodes.<br/>
            /// Leaf nodes are required to have this for perfomance reasons.
            /// </summary>
            IsLeaf = 1 << 2,
        }

        [System.Diagnostics.DebuggerDisplay("{Code} {Center} {Flags}")]
        private struct InnerOctant
        {
            public InnerOctantFlags Flags;

            public OctantCode Code;

            public Vector3 Center;

            public bool IsIntransitable {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (Flags & InnerOctantFlags.IsIntransitable) != 0;
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set {
                    if (value)
                        Flags |= InnerOctantFlags.IsIntransitable;
                    else
                        Flags &= InnerOctantFlags.IsIntransitable;
                }
            }

            public bool IsLeaf {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (Flags & InnerOctantFlags.IsLeaf) != 0;
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set {
                    if (value)
                        Flags |= InnerOctantFlags.IsLeaf;
                    else
                        Flags &= InnerOctantFlags.IsLeaf;
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

            public InnerOctant(OctantCode code, Vector3 center)
            {
                Center = center;
                Code = code;
                Flags = default;
            }

            public InnerOctant(OctantCode code, Vector3 center, InnerOctantFlags flags)
            {
                Center = center;
                Code = code;
                Flags = flags;
            }

            public InnerOctant(OctantCode code, InnerOctantFlags flags)
            {
                Code = code;
                Flags = flags;
                Center = default;
            }

            public InnerOctant(OctantCode code)
            {
                Code = code;
                Center = default;
                Flags = default;
            }
        }
    }
}