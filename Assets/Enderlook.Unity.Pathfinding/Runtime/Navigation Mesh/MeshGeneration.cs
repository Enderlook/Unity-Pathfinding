using Enderlook.Unity.Extensions;

using System;

using UnityEngine;

using UnityObject = UnityEngine.Object;

namespace Enderlook.Unity.Pathfinding
{
    internal static class MeshGeneration
    {
        public static Memory<MeshFilter> GetMeshes(Bounds boundingRegion, LayerMask allowedLayers)
        {
            MeshFilter[] meshFilters = UnityObject.FindObjectsOfType<MeshFilter>();
            int index = 0;
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                GameObject gameObject = meshFilter.gameObject;

                if (!gameObject.LayerMatchTest(allowedLayers))
                    continue;

                if (!gameObject.activeInHierarchy)
                    continue;

                Transform transform = gameObject.transform;

                Mesh mesh = meshFilter.sharedMesh;

                Bounds bounds = mesh.bounds;
                bounds.center += meshFilter.gameObject.transform.position;
                Vector3 lossyScale = transform.lossyScale;
                bounds.size = new Vector3(bounds.size.x * lossyScale.x, bounds.size.y * lossyScale.y, bounds.size.z * lossyScale.z);

                if (!bounds.Intersects(boundingRegion))
                    continue;

                meshFilters[index++] = meshFilter;
            }

            return meshFilters.AsMemory(0, index);
        }
    }
}