using System;

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

            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
            foreach (MeshFilter meshFilter in meshFilters)
            {
                Mesh mesh = meshFilter.sharedMesh;
                mesh.RecalculateBounds();
            }

            if (meshFilters.Length == 0)
                return;

            (int, int, int) resolution = (25, 5, 15);
            Bounds bounds = new Bounds(transform.position, new Vector3(5, 1, 3));
            MeshVoxelizer meshVoxelizer = new MeshVoxelizer(resolution, bounds);

            foreach (MeshFilter meshFilter in meshFilters)
                meshVoxelizer.Enqueue(meshFilter);

            meshVoxelizer.Process().Complete();

            Span<bool> voxels = meshVoxelizer.Voxels;

            Vector3 voxelSize = meshVoxelizer.VoxelSize;

            Resolution r = new Resolution(resolution.Item1, resolution.Item2, resolution.Item3, bounds);
            HeightField heightField = new HeightField(voxels, r);
            //heightField.DrawGizmos(r, false);

            //OpenHeightField openHeightField = new OpenHeightField(heightField, resolution, 1, 1);
            //openHeightField.DrawGizmosOfOpenHeightField(transform.position, voxelSize, true);

            //openHeightField.CalculateDistanceField();
            //openHeightField.DrawGizmosOfDistanceHeightField(transform.position, voxelSize);

            /*openHeightField.CalculateRegions(0);
            openHeightField.DrawGizmosOfRegions(transform.position, voxelSize);*/

            CompactOpenHeightField openHeighField = new CompactOpenHeightField(heightField, r, 1, 1);
            //openHeighField.DrawGizmos(r, true);

            DistanceField distanceField = new DistanceField(openHeighField);
            //distanceField.DrawGizmos(r, openHeighField);

            RegionsField regions = new RegionsField(distanceField, openHeighField, 0);
            //regions.DrawGizmos(r, openHeighField);

            Contours contours = new Contours(regions, openHeighField, r);
            contours.DrawGizmos(r, openHeighField, regions);
        }
    }
}
