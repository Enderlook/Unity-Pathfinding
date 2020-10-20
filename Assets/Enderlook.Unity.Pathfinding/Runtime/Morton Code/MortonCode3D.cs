using System;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [Serializable]
    public readonly struct MortonCode3D
    {
        // https://fgiesen.wordpress.com/2009/12/13/decoding-morton-codes/
        // https://www.forceflow.be/2013/10/07/morton-encodingdecoding-through-bit-interleaving-implementations/
        // http://asgerhoedt.dk/?p=276
        // TODO: Using the LUT method table can perfomance be increased.

        public readonly uint Code;

        public int Depth => (int)(1 / 3 * Mathf.Log(Code, 2));

        public MortonCode3D Parent3 => new MortonCode3D(Code >> 3);

        public int X => (int)Compact1By2(Code >> 0);

        public int Y => (int)Compact1By2(Code >> 1);

        public int Z => (int)Compact1By2(Code >> 3);

        public MortonCode3D(Vector3Int position) : this(position.x, position.y, position.z) { }

        public MortonCode3D(int x, int y, int z) : this((Part1By2((uint)z) << 2) + (Part1By2((uint)y) << 1) + Part1By2((uint)x)) { }

        public MortonCode3D(uint code) => Code = code;

        private static uint Part1By2(uint x)
        {
            // "Insert" two 0 bits after each of the 10 low bits of x
            x &= 0x000003ff;                  // x = ---- ---- ---- ---- ---- --98 7654 3210
            x = (x ^ (x << 16)) & 0xff0000ff; // x = ---- --98 ---- ---- ---- ---- 7654 3210
            x = (x ^ (x << 8)) & 0x0300f00f;  // x = ---- --98 ---- ---- 7654 ---- ---- 3210
            x = (x ^ (x << 4)) & 0x030c30c3;  // x = ---- --98 ---- 76-- --54 ---- 32-- --10
            x = (x ^ (x << 2)) & 0x09249249;  // x = ---- 9--8 --7- -6-- 5--4 --3- -2-- 1--0
            return x;
        }

        private static uint Compact1By2(uint v)
        {
            // Inverse of Part1By2 - "delete" all bits not at positions divisible by 3
            v &= 0x09249249;                  // x = ---- 9--8 --7- -6-- 5--4 --3- -2-- 1--0
            v = (v ^ (v >> 2)) & 0x030c30c3;  // x = ---- --98 ---- 76-- --54 ---- 32-- --10
            v = (v ^ (v >> 4)) & 0x0300f00f;  // x = ---- --98 ---- ---- 7654 ---- ---- 3210
            v = (v ^ (v >> 8)) & 0xff0000ff;  // x = ---- --98 ---- ---- ---- ---- 7654 3210
            v = (v ^ (v >> 16)) & 0x000003ff; // x = ---- ---- ---- ---- ---- --98 7654 3210
            return v;
        }
    }
}