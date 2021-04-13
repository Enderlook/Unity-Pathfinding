using System;

using UnityEngine;
using UnityEngine.Profiling;

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

            Profiler.BeginSample("Enderlook.MeshVoxelizer");
            meshVoxelizer.Process().Complete();
            Profiler.EndSample();

            Span<bool> voxels = meshVoxelizer.Voxels;

            Vector3 voxelSize = meshVoxelizer.VoxelSize;

            Resolution r = new Resolution(resolution.Item1, resolution.Item2, resolution.Item3, bounds);
            Profiler.BeginSample("Enderlook.HeightField");
            HeightField heightField = new HeightField(voxels, r);
            Profiler.EndSample();
            heightField.DrawGizmos(r, false);

            /*Profiler.BeginSample("Enderlook.OpenHeightField");
            CompactOpenHeightField openHeighField = new CompactOpenHeightField(heightField, r, 1, 1);
            Profiler.EndSample();
            //openHeighField.DrawGizmos(r, false, true);

            Profiler.BeginSample("Enderlook.DistanceField");
            DistanceField distanceField = new DistanceField(openHeighField);
            Profiler.EndSample();
            //distanceField.DrawGizmos(r, openHeighField);

            Profiler.BeginSample("Enderlook.RegionsField");
            RegionsField regions = new RegionsField(distanceField, openHeighField, 0);
            Profiler.EndSample();
            //regions.DrawGizmos(r, openHeighField);

            Profiler.BeginSample("Enderlook.Contours");
            Contours contours = new Contours(regions, openHeighField, r);
            Profiler.EndSample();
            //contours.DrawGizmos(r, openHeighField, regions);*/

            Profiler.BeginSample("Enderlook.Dispose");
            meshVoxelizer.Dispose();
            heightField.Dispose();
            /*openHeighField.Dispose();
            distanceField.Dispose();
            regions.Dispose();
            contours.Dispose();*/
            Profiler.EndSample();
        }
    }
}
