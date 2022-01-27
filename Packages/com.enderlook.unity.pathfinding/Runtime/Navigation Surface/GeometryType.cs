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
        RenderMeshes = 1 << 1,

        /// <summary>
        /// Collect geometry from the 3D physics non-trigger collision representation.
        /// </summary>
        PhysicsColliders = 1 << 2,

        /// <summary>
        /// Collect geometry from the 3D physics trigger collision representation.
        /// </summary>
        PhysicsTriggerColliders = 1 << 3,
    }
}