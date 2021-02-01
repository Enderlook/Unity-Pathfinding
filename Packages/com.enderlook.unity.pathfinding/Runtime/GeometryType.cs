namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Determines the type of geometry used by path areas.
    /// </summary>
    public enum GeometryType
    {
        /// <summary>
        /// Use geometry from Colliders and Terrains.
        /// </summary>
        PhysicsColliders,

        /// <summary>
        /// Use geometry from Render Meshes and Terrains.
        /// </summary>
        RenderMeshes,
    }
}