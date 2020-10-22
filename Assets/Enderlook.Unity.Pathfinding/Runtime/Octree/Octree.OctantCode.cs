using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree
    {
        [Serializable, System.Diagnostics.DebuggerDisplay("{Code}")]
        private readonly struct OctantCode : IEquatable<OctantCode>, IComparable<OctantCode>
        {
            public const uint DIL_X = 0b001_001_001_001_001_001_001_001_001_00000;
            public const uint DIL_Y = 0b010_010_010_010_010_010_010_010_010_00000;
            public const uint DIL_Z = 0b100_100_100_100_100_100_100_100_100_00000;
            public const uint DIL_XYZ = DIL_X | DIL_Y | DIL_Z;

            public const int SIZE = sizeof(uint);

            // https://geidav.wordpress.com/2014/08/18/advanced-octrees-2-node-representations/
            // A Morton 3D Code with includes depth

            public readonly uint Code;

            public int Depth {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Mathf.FloorToInt(Mathf.Log(Code, 2) / 3);// Alternative this may work: (31 - System.Numerics.BitOperations.LeadingZeroCount(Code)) / 3;
            }

            public OctantCode Parent {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => new OctantCode(Code >> 3);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public OctantCode(uint code) => Code = code;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public OctantCode GetChildTh(uint th)
            {
                Debug.Assert(th <= 8, th);
                return new OctantCode((Code << 3) | th);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public OctantCode(ReadOnlySpan<byte> source) => Code = BinaryPrimitives.ReadUInt32LittleEndian(source);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsMaxDepth(uint maxDepth) => Code == maxDepth;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float GetSize(float rootSize) => Mathf.Pow(.5f, Depth) * rootSize;

            /*[MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Vector3 GetCenter(Vector3 rootCenter, float rootSize)
            {
                int depth = Depth;
                if (depth == 0)
                    return rootCenter;

                Vector3 center = rootCenter;
                float size = rootSize * .5f;

                Span<OctantCode> stack = stackalloc OctantCode[depth + 1];
                stack[0] = this;

                for (int i = 1; i < stack.Length; i++)
                    stack[i] = stack[i - 1].Parent;

                Debug.Assert(stack[stack.Length - 1] == new OctantCode(1));

                // TODO: some math would allow to avoid this brute force check

                for (int i = stack.Length - 1; i > 1; i--)
                {
                    OctantCode parent = stack[i];
                    OctantCode current = stack[i - 1];
                    for (uint j = 0; j < 8; j++)
                    {
                        if (parent.GetChildTh(j) == current)
                        {
                            size *= .5f;
                            center += ChildrenPositions.Childs[j] * size;
                            break;
                        }
                    }
                }

                return center;
            }*/

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int CompareTo(OctantCode other) => Code.CompareTo(other.Code);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(OctantCode other) => Code == other.Code;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override bool Equals(object obj) => obj is OctantCode other && Equals(other);

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
            public static bool operator ==(OctantCode a, OctantCode b) => a.Equals(b);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator !=(OctantCode a, OctantCode b) => !a.Equals(b);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteBytes(Span<byte> destination) => BinaryPrimitives.WriteUInt32LittleEndian(destination, Code);
        }
    }
}