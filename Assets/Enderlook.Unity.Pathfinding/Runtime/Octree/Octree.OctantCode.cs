using System;
using System.Buffers.Binary;
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