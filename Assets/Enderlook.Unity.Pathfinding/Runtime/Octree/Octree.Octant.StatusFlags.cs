using System;

namespace Enderlook.Unity.Pathfinding
{
    public sealed partial class Octree
    {
        private partial struct Octant
        {
            [Flags]
            public enum StatusFlags : byte
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
        }
    }
}