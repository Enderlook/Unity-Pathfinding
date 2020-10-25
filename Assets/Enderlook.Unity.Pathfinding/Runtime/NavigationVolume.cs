using Enderlook.Unity.Attributes;

using System;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [AddComponentMenu("Enderlook/Pathfinding/Navigation Volume")]
    public sealed class NavigationVolume : MonoBehaviour
    {
#pragma warning disable CS0649
        [SerializeField, Tooltip("Defines which GameObjects are used to determine the path area.")]
        private CollectionType collectionType;

        [SerializeField, ShowIf(nameof(collectionType), typeof(CollectionType), CollectionType.Volume, true), Tooltip("Objects in range contribute to the path area.")]
        private float collectionSize;

        [SerializeField, Tooltip("Define the layers of which GameObject are be included.")]
        private LayerMask filterInclude;

        [SerializeField, Tooltip("Determines the geometry used for path areas.")]
        private GeometryType geometryType;

        [SerializeField, ShowIf(nameof(geometryType), typeof(GeometryType), GeometryType.PhysicsColliders, true), Tooltip("Whenever it should include trigger colliders for path areas.")]
        private bool includeTriggerColliders;

        [SerializeField, UnityEngine.Range(0, 9), Tooltip("Defiens the level of detail of the path area.")]
        private byte subdivisions = 1;

        [SerializeField, Tooltip("Determines which types of connections are produced.")]
        private ConnectionType connectionType = ConnectionType.Transitable;

        [SerializeField, HideInInspector]
        private Octree graph;
#pragma warning restore CS0649

#if UNITY_EDITOR
        /// <summary>
        /// Only use in Editor.
        /// </summary>
        internal Octree Graph => graph;
#endif

#if UNITY_EDITOR
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnDrawGizmos()
        {
            if ((collectionType & CollectionType.Volume) == 0)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(transform.position, Vector3.one * collectionSize);
            }

            graph.DrawGizmos();
        }
#endif

        internal void Bake()
        {
            if (geometryType == GeometryType.RenderMeshes)
                throw new NotImplementedException();

            if (collectionType == CollectionType.Children)
                throw new NotImplementedException();

            Clear();
            graph.SubdivideFromObstacles(filterInclude, includeTriggerColliders);
            graph.CalculateConnections(connectionType);
        }

        internal void Clear() => graph.Reset(transform.position, collectionSize, subdivisions);

        public void CalculatePath<TBuilder, TWatchdog>(Vector3 from, Vector3 to, TBuilder builder, TWatchdog watchdog)
            where TBuilder : IPathBuilder<Octree.OctantCode, Vector3>
            where TWatchdog : IWatchdog
            => graph.CalculatePath(from, to, builder, watchdog);

        public void CalculatePath<TBuilder>(Vector3 from, Vector3 to, TBuilder builder)
            where TBuilder : IPathBuilder<Octree.OctantCode, Vector3>
            => graph.CalculatePath(from, to, builder);
    }
}