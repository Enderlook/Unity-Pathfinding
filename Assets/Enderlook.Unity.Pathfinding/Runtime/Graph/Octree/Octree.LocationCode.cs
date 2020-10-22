﻿using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree
    {
        [Serializable]
        private readonly struct LocationCode : IEquatable<LocationCode>, IComparable<LocationCode>
        {
            public static int SIZE = sizeof(uint);

            // https://geidav.wordpress.com/2014/08/18/advanced-octrees-2-node-representations/
            // A Morton 3D Code with includes depth

            public readonly uint Code;

            public int Depth {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Mathf.FloorToInt(Mathf.Log(Code, 2) / 3);// Alternative this may work: (31 - System.Numerics.BitOperations.LeadingZeroCount(Code)) / 3;
            }

            public LocationCode Parent {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => new LocationCode(Code >> 3);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public LocationCode(uint code) => Code = code;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public LocationCode GetChildTh(uint th)
            {
                Debug.Assert(th <= 8, th);
                return new LocationCode((Code << 3) | th);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public LocationCode(ReadOnlySpan<byte> source) => Code = BinaryPrimitives.ReadUInt32LittleEndian(source);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsMaxDepth(uint maxDepth) => Code == maxDepth;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float GetSize(float rootSize) => Mathf.Pow(.5f, Depth) * rootSize;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int CompareTo(LocationCode other) => Code.CompareTo(other.Code);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(LocationCode other) => Code == other.Code;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override bool Equals(object obj) => obj is LocationCode other && Equals(other);

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
            public void WriteBytes(Span<byte> destination) => BinaryPrimitives.WriteUInt32LittleEndian(destination, Code);
        }
    }
}