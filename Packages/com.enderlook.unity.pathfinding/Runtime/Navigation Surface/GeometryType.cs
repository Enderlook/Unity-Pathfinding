using System;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Determines the type of geometry used by path areas.
    /// </summary>
    [Flags]
    public enum GeometryType
    {
        /// <summary>
        /// Collect meshes form the rendered geometry.
        /// </summary>
        RenderMeshes,

        /// <summary>
        /// Collect geometry from the 3D physics non-trigger collision representation.
        /// </summary>
        PhysicsColliders,

        /// <summary>
        /// Collect geometry from the 3D physicst trigger collision representation.
        /// </summary>
        PhysicsTriggerColliders,
    }
}