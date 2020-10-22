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
            IsIntransitable = 1 << 0,
        }

        private struct InnerOctant
        {
            public InnerOctantFlags Flags;

            public LocationCode Code;

            public Vector3 Center;

            public bool IsIntransitable {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (Flags & InnerOctantFlags.IsIntransitable) != 0;
                set {
                    if (value)
                        Flags |= InnerOctantFlags.IsIntransitable;
                    else
                        Flags &= InnerOctantFlags.IsIntransitable;
                }
            }

            public bool IsTransitable {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => !IsIntransitable;
            }

            public int Depth {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Code.Depth;
            }

            public LocationCode Parent {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Code.Parent;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public LocationCode GetChildTh(uint th) => Code.GetChildTh(th);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsMaxDepth(uint maxDepth) => Code.IsMaxDepth(maxDepth);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float GetSize(float rootSize) => Code.GetSize(rootSize);

            public InnerOctant(LocationCode code, Vector3 center)
            {
                Center = center;
                Code = code;
                Flags = default;
            }

            public InnerOctant(LocationCode code, Vector3 center, InnerOctantFlags flags)
            {
                Center = center;
                Code = code;
                Flags = flags;
            }

            public InnerOctant(LocationCode code, InnerOctantFlags flags)
            {
                Code = code;
                Flags = flags;
                Center = default;
            }

            public InnerOctant(LocationCode code)
            {
                Code = code;
                Center = default;
                Flags = default;
            }
        }
    }
}