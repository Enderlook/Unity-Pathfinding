using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.Profiling;

namespace Enderlook.Unity.Pathfinding2
{
    public class Test : MonoBehaviour
    {
        [SerializeField]
        public Configuration conf;

        private List<long> q = new List<long>();

        public void OnDrawGizmos()
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

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
            //heightField.DrawGizmos(r, false);

            Profiler.BeginSample("Enderlook.OpenHeightField");
            CompactOpenHeightField openHeightField = new CompactOpenHeightField(heightField, r, 1, 1);
            Profiler.EndSample();
            //openHeightField.DrawGizmos(r, false, true);

            Profiler.BeginSample("Enderlook.DistanceField");
            DistanceField distanceField = new DistanceField(openHeightField);
            Profiler.EndSample();
            //distanceField.DrawGizmos(r, openHeightField);

            Profiler.BeginSample("Enderlook.DistanceField2");
            DistanceField distanceField2 = distanceField.WithBlur(openHeightField, 1);
            Profiler.EndSample();
            //distanceField2.DrawGizmos(r, openHeightField);

            Profiler.BeginSample("Enderlook.RegionsField");
            RegionsField regions = new RegionsField(distanceField2, openHeightField, 0, 2);
            Profiler.EndSample();
            regions.DrawGizmos(r, openHeightField);

            Profiler.BeginSample("Enderlook.Contours");
            Contours contours = new Contours(regions, openHeightField, r);
            Profiler.EndSample();
            contours.DrawGizmos(r, openHeightField, regions);

            Profiler.BeginSample("Enderlook.Dispose");
            meshVoxelizer.Dispose();
            heightField.Dispose();
            openHeightField.Dispose();
            distanceField.Dispose();
            distanceField2.Dispose();
            regions.Dispose();
            contours.Dispose();
            Profiler.EndSample();

            long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            q.Add(elapsedMilliseconds);
            Debug.Log($"{q.Average()} {q.Count} {elapsedMilliseconds}");
        }
    }
}
