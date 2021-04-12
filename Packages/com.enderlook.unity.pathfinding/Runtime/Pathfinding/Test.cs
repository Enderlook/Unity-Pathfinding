using System;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    public class Test : MonoBehaviour
    {
        [SerializeField]
        public Configuration conf;

        public void OnDrawGizmos()
        {
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
            foreach (MeshFilter meshFilter in meshFilters)
            {
                Mesh mesh = meshFilter.sharedMesh;
                mesh.RecalculateBounds();
            }
            if (meshFilters.Length == 0)
                return;

            (int, int, int) resolution = (60, 12, 60);
            Bounds bounds = new Bounds(transform.position, new Vector3(10, 2f, 10));
            MeshVoxelizer meshVoxelizer = new MeshVoxelizer(resolution, bounds);

            foreach (MeshFilter meshFilter in meshFilters)
                meshVoxelizer.Enqueue(meshFilter);

            meshVoxelizer.Process().Complete();

            Span<bool> voxels = meshVoxelizer.Voxels;

            Vector3 voxelSize = meshVoxelizer.VoxelSize;

            Resolution r = new Resolution(resolution.Item1, resolution.Item2, resolution.Item3, bounds);
            HeightField heightField = new HeightField(voxels, r);
            //heightField.DrawGizmos(r, false);

            CompactOpenHeightField openHeighField = new CompactOpenHeightField(heightField, r, 1, 1);
            //openHeighField.DrawGizmos(r, false, true);

            DistanceField distanceField = new DistanceField(openHeighField);
            //distanceField.DrawGizmos(r, openHeighField);

            RegionsField regions = new RegionsField(distanceField, openHeighField, 0);
            //regions.DrawGizmos(r, openHeighField);

            Contours contours = new Contours(regions, openHeighField, r);
            //contours.DrawGizmos(r, openHeighField, regions);

            meshVoxelizer.Dispose();
            heightField.Dispose();
            openHeighField.Dispose();
            distanceField.Dispose();
            regions.Dispose();
            contours.Dispose();
        }
    }
}
