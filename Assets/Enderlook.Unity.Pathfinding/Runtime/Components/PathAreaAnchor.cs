using Enderlook.Unity.Attributes;

using System;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [AddComponentMenu("Enderlook/Pathfinding/PathAreaManagerAnchor")]
    public class PathAreaAnchor : MonoBehaviour
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

        [SerializeField, Min(1), Tooltip("Defiens the level of detail of the path area.")]
        private byte subdivisions = 1;

        [SerializeField, HideInInspector]
        private Octree graph;
#pragma warning restore CS0649

#if UNITY_EDITOR
        /// <summary>
        /// Only use in Editor.
        /// </summary>
        internal Octree.DrawMode DrawMode {
            get => graph.drawMode;
            set => graph.drawMode = value;
        }

        /// <summary>
        /// Only use in Editor.
        /// </summary>
        internal int SerializedOctansCount => graph.SerializedOctansCount;
#endif

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

        internal void Bake()
        {
            if (geometryType == GeometryType.RenderMeshes)
                throw new NotImplementedException();

            if (collectionType == CollectionType.Children)
                throw new NotImplementedException();

            Clear();
            graph.SubdivideFromObstacles(filterInclude, includeTriggerColliders);
        }

        internal void Clear() => graph.Reset(transform.position, collectionSize, subdivisions);
    }
}