using System;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    /// <summary>
    /// Represent the configuration of a navigation mesh generation.
    /// </summary>
    [Serializable]
    public struct Configuration
    {
        /// <summary>
        /// Center position of the navigation mesh.
        /// </summary>
        public Vector3 Center;

        /// <summary>
        /// The volume in cells to generate.
        /// </summary>
        public Vector3Int Volume;

        /// <summary>
        /// Determines the layer used when testing for collision of obstacles.
        /// </summary>
        public LayerMask ObstaclesMask;

        /// <summary>
        /// Determines the width (X-axis) and depth (Z-axis) of each cell.
        /// </summary>
        public float CellSize;

        /// <summary>
        /// Determines the height (Y-axis) of each cell.
        /// </summary>
        public float CellHeight;

        /// <summary>
        /// Minimum floor to ceiling height that is required for a floor area to be considered traversable.<br/>
        /// This value can be thought as the agent height.
        /// </summary>
        public float MinimalTraversableHeight;

        /// <summary>
        /// Represent the maximum ledge height that is considered to still be traversable.<br/>
        /// This value can be thought as the agent jump height.
        /// </summary>
        public float MaximumTraversableStep;

        /// <summary>
        /// Maximum slope that is considered traversable. (In degrees).<br/>
        /// This value can be thougth as the maximum slope that an agent can climb.
        /// </summary>
        public float MaximumTraversableSlope;

        /// <summary>
        /// Represents the closest any part of a mesh can get to an obstruction in the source geometry.
        /// </summary>
        public float TraversableAreaBorderSize;

        /// <summary>
        /// Minimum region size for unconnected (island) regions.<br/>
        /// Regions that are not connected to any other region and are smaller than this size will be culled before mesh generation. (They will no longer be considered traversable).
        /// </summary>
        public float MinimumUnconnectedRegionSize;
    }
}