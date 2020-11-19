using Enderlook.Unity.Attributes;
using Enderlook.Unity.Pathfinding.Algorithms;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [AddComponentMenu("Enderlook/Pathfinding/Navigation Volume"), DefaultExecutionOrder(ExecutionOrder.NavigationAgent)]
    public sealed class NavigationVolume : MonoBehaviour
    {
#pragma warning disable CS0649
        [SerializeField, Tooltip("Defines which GameObjects are used to determine the path area.")]
        private CollectionType collectionType;

        [SerializeField, ShowIf(nameof(collectionType), typeof(CollectionType), CollectionType.Volume, true), Tooltip("Objects in range contribute to the path area.")]
        private float collectionSize;

        [SerializeField, Tooltip("Define the layers of which GameObject are be included.")]
        private LayerMask filterInclude;

        [SerializeField, Tooltip("Defines the layers of which GameObjects count as ground.")]
        private LayerMask filterGround;

        [SerializeField, Tooltip("Determines the geometry used for path areas.")]
        private GeometryType geometryType;

        [SerializeField, ShowIf(nameof(geometryType), typeof(GeometryType), GeometryType.PhysicsColliders, true), Tooltip("Whenever it should include trigger colliders for path areas.")]
        private bool includeTriggerColliders;

        [SerializeField, UnityEngine.Range(0, 9), Tooltip("Defiens the level of detail of the path area.")]
        private byte subdivisions = 1;

        [SerializeField, Tooltip("Determines which types of connections are produced.")]
        private ConnectionType connectionType = ConnectionType.Transitable;

        [SerializeField, Tooltip("If present, this content will be read as navigation information. Otherwise it's produced at runtime.")]
        internal NavigationBaked bakedContent;

        private Octree graph;
#pragma warning restore CS0649

#if UNITY_EDITOR
        /// <summary>
        /// Only use in Editor.
        /// </summary>
        internal Octree Graph {
            get {
                CheckInitialize();
                return graph;
            }
        }

        private void CheckInitialize()
        {
            if (graph is null)
                Initialize();
        }
#endif

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void Awake() => Initialize();

        private void Initialize()
        {
            if (bakedContent != null)
                graph = new Octree(bakedContent.content);
            else
            {
                graph = new Octree(transform.position, collectionSize, subdivisions, filterInclude, filterGround, includeTriggerColliders, connectionType);
                Bake();
            }
        }

#if UNITY_EDITOR
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnDrawGizmos()
        {
            if ((collectionType & CollectionType.Volume) == 0)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(transform.position, Vector3.one * collectionSize);
            }

#if UNITY_EDITOR
            CheckInitialize();
#endif
            graph.DrawGizmos();
        }

        /// <summary>
        /// Only use in Editor.
        /// </summary>
        internal void LoadBakedContent() => graph = new Octree(bakedContent.content);

        internal void CheckAndBake()
        {
            if (graph is null)
                graph = new Octree(transform.position, collectionSize, subdivisions, filterInclude, filterGround, includeTriggerColliders, connectionType);

            Bake();
        }
#endif

        internal void Save() => bakedContent.content = graph.SaveAs();

        internal void Bake()
        {
            if (geometryType == GeometryType.RenderMeshes)
                throw new NotImplementedException();

            if (collectionType == CollectionType.Children)
                throw new NotImplementedException();

            Clear();
            graph.SubdivideFromObstacles();
            graph.CalculateConnections();
        }

        internal void Clear() => graph.Reset(transform.position, collectionSize, subdivisions, filterInclude, filterGround, includeTriggerColliders, connectionType);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CalculatePathSync(Path<Vector3> path, Vector3 from, Vector3 to)
        {
#if UNITY_EDITOR
            CheckInitialize();
#endif
            PathCalculator.CalculatePathSingleThread<Vector3, Octree.OctantCode, HashSet<Octree.OctantCode>.Enumerator, Octree, PathBuilder<Octree.OctantCode, Vector3>, Path<Vector3>>(graph, path, from, to);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CalculatePath(Path<Vector3> path, Vector3 from, Vector3 to)
        {
#if UNITY_EDITOR
            CheckInitialize();
#endif
            PathCalculator.CalculatePathJob<Vector3, Octree.OctantCode, HashSet<Octree.OctantCode>.Enumerator, Octree, PathBuilder<Octree.OctantCode, Vector3>, Path<Vector3>>(graph, path, from, to);
        }

#if !UNITY_EDITOR
        internal void DO_NOT_EXECUTE_ME()
        {
            // Unity's IL2CPP doesn't generate AOT code for
            // generic virtual methods nor reflection instance activators (new() generic constrain),
            // so we must to explictly show to the compiler that we actually need them
            // when using that backend.

            PathBuilder<Octree.OctantCode, Vector3> builder = new PathBuilder<Octree.OctantCode, Vector3>();

            throw new InvalidOperationException("Don't execute this!!!");
        }
#endif
    }
}