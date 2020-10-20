using System;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [Serializable]
    public readonly struct MortonCode2D
    {
        // https://fgiesen.wordpress.com/2009/12/13/decoding-morton-codes/
        // https://www.forceflow.be/2013/10/07/morton-encodingdecoding-through-bit-interleaving-implementations/
        // http://asgerhoedt.dk/?p=276
        // TODO: Using the LUT method table can perfomance be increased.

        public readonly uint Code;

        public int Depth => (int)(1 / 3 * Mathf.Log(Code, 2));

        public MortonCode2D Parent2 => new MortonCode2D(Code >> 2);

        public int X => (int)Compact1By1(Code >> 0);

        public int Y => (int)Compact1By1(Code >> 1);

        public MortonCode2D(Vector2Int position) : this(position.x, position.y) { }

        public MortonCode2D(int x, int y) : this((Part1By1((uint)y) << 1) + Part1By1((uint)x)) { }

        public MortonCode2D(uint code) => Code = code;

        private static uint Part1By1(uint v)
        {
            // "Insert" a 0 bit after each of the 16 low bits of x
            v &= 0x0000ffff;                 // x = ---- ---- ---- ---- fedc ba98 7654 3210
            v = (v ^ (v << 8)) & 0x00ff00ff; // x = ---- ---- fedc ba98 ---- ---- 7654 3210
            v = (v ^ (v << 4)) & 0x0f0f0f0f; // x = ---- fedc ---- ba98 ---- 7654 ---- 3210
            v = (v ^ (v << 2)) & 0x33333333; // x = --fe --dc --ba --98 --76 --54 --32 --10
            v = (v ^ (v << 1)) & 0x55555555; // x = -f-e -d-c -b-a -9-8 -7-6 -5-4 -3-2 -1-0
            return v;
        }
        private static uint Compact1By1(uint v)
        {
            // Inverse of Part1By1 - "delete" all odd-indexed bits
            v &= 0x55555555;                 // x = -f-e -d-c -b-a -9-8 -7-6 -5-4 -3-2 -1-0
            v = (v ^ (v >> 1)) & 0x33333333; // x = --fe --dc --ba --98 --76 --54 --32 --10
            v = (v ^ (v >> 2)) & 0x0f0f0f0f; // x = ---- fedc ---- ba98 ---- 7654 ---- 3210
            v = (v ^ (v >> 4)) & 0x00ff00ff; // x = ---- ---- fedc ba98 ---- ---- 7654 3210
            v = (v ^ (v >> 8)) & 0x0000ffff; // x = ---- ---- ---- ---- fedc ba98 7654 3210
            return v;
        }
    }
}