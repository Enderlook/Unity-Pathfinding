using System;
using System.Runtime.CompilerServices;

namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree
    {
        [Serializable, System.Diagnostics.DebuggerDisplay("{Code}")]
        private readonly struct VertexCode : IEquatable<VertexCode>
        {
            public const int SIZE = sizeof(uint);

            public readonly uint Code;

            public VertexCode(uint code) => Code = code;

            /// <summary>
            /// Get all the octants code which has this vertex at the given depth.
            /// </summary>
            /// <param name="depth">Depth of the octants code to find.</param>
            /// <param name="leaves">Octants of the given depth which has this vertex.</param>
            /// <param name="maxDepth">The maximum depth of this octree.</param>
            public void GetLeavesCode(int depth, Span<OctantCode> leaves, int maxDepth)
            {
                // Removes trailing 0's
                uint dc = Code >> 3 * (maxDepth - depth);
                for (uint i = 0; i < 8; i++)
                {
                    // Dilated integer substraction dc - i
                    leaves[(int)i] = new OctantCode(
                        (((dc & OctantCode.DIL_X) - (i & OctantCode.DIL_X)) & OctantCode.DIL_X) |
                        (((dc & OctantCode.DIL_Y) - (i & OctantCode.DIL_Y)) & OctantCode.DIL_Y) |
                        (((dc & OctantCode.DIL_Z) - (i & OctantCode.DIL_Z)) & OctantCode.DIL_Z)
                    );
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(VertexCode other) => Code == other.Code;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override bool Equals(object obj) => obj is VertexCode other && Equals(other);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int GetHashCode()
            {
                uint code = Code;
                unsafe
                {
                    return *(int*)&code;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator ==(VertexCode a, VertexCode b) => a.Equals(b);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator !=(VertexCode a, VertexCode b) => !a.Equals(b);
        }
    }
}