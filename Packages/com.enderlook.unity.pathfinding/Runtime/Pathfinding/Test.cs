using System;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Unity.Collections;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    public class Test : MonoBehaviour
    {
        [SerializeField]
        public Configuration conf;

        public void OnDrawGizmos()
        {
            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;

            {
                MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
                foreach (MeshFilter meshFilter in meshFilters)
                {
                    Mesh mesh = meshFilter.sharedMesh;
                    mesh.RecalculateBounds();
                }

                if (meshFilters.Length == 0)
                    return;

                (int, int, int) resolution = (20, 20, 30);
                MeshVoxelizer meshVoxelizer = new MeshVoxelizer(resolution, new Bounds(transform.position, new Vector3(5, 2, 3)));

                foreach (MeshFilter meshFilter in meshFilters)
                    meshVoxelizer.Enqueue(meshFilter);

                meshVoxelizer.Process().Complete();

                Span<bool> voxels = meshVoxelizer.Voxels;

                Vector3 voxelSize = meshVoxelizer.VoxelSize;

                HeightField heightField = new HeightField(voxels, resolution);
                //heightField.DrawGizmos(transform.position, voxelSize, false);

                new CompactOpenHeightField(heightField, 1, 1).DrawGizmosOfOpenHeightField(transform.position, voxelSize, true);

                /*OpenHeightField openHeightField = new OpenHeightField(heightField, 1, 1);
                //openHeightField.DrawGizmosOfOpenHeightField(transform.position, voxelSize, true);

                openHeightField.CalculateDistanceField();
                //openHeightField.DrawGizmosOfDistanceHeightField(transform.position, voxelSize);

                openHeightField.CalculateRegions(0);
                openHeightField.DrawGizmosOfRegions(transform.position, voxelSize);*/
            }
        }
    }
}
