using System;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Determines to which types of nodes are connections allowed.
    /// </summary>
    [Flags]
    public enum ConnectionType
    {
        /// <summary>
        /// Allow connection between transitable nodes.
        /// </summary>
        Transitable = 1 << 0,

        /// <summary>
        /// Allow connection between intransitable nodes.
        /// </summary>
        Intransitable = 1 << 1,

        /// <summary>
        /// Allow connection between nodes which has ground.
        /// </summary>
        HasGround = 1 << 3,
    }
}