using Enderlook.Utils;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Profiling;

namespace Enderlook.Unity.Pathfinding2
{
    public class Test : MonoBehaviour
    {
        [SerializeField]
        public Configuration conf;

        private List<long> q = new List<long>();
        private List<long> q2 = new List<long>();

        private int work;
        private HeightField heightField;
        private CompactOpenHeightField openHeightField;
        private DistanceField distanceField;
        private DistanceField distanceField2;
        private RegionsField regions;
        private Contours contours;

        (int, int, int) resolution = (60, 12, 60);
        private MeshGenerationOptions options;
        private Bounds bounds;

        private void OnDrawGizmos()
        {
            if (work == 0)
            {
                work = 1;
                GenerateAsync().GetAwaiter().OnCompleted(() =>
                {
                    work = 2;
                });
            }

            if (work == 1)
            {
                Debug.Log($"Working {options.Percentage}");
                return;
            }

            Resolution r = new Resolution(resolution.Item1, resolution.Item2, resolution.Item3, bounds);
            //heightField.DrawGizmos(r, false);
            openHeightField.DrawGizmos(r, false, true);
            //distanceField.DrawGizmos(r, openHeightField);
            //distanceField2.DrawGizmos(r, openHeightField);
            //regions.DrawGizmos(r, openHeightField);
            //contours.DrawGizmos(r, openHeightField, regions);
        }

        public async ValueTask GenerateAsync()
        {
            options = new MeshGenerationOptions();
            bounds = new Bounds(transform.position, new Vector3(10, 2f, 10));
            options.Resolution = new Resolution(resolution.Item1, resolution.Item2, resolution.Item3, bounds);
            options.UseMultithreading = true;

            options.PushTask(7, "All");

            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
            foreach (MeshFilter meshFilter in meshFilters)
            {
                Mesh mesh = meshFilter.sharedMesh;
                mesh.RecalculateBounds();
            }
            if (meshFilters.Length == 0)
                return;

            MeshVoxelizer meshVoxelizer = new MeshVoxelizer(options);

            foreach (MeshFilter meshFilter in meshFilters)
                meshVoxelizer.Enqueue(meshFilter);

            Profiler.BeginSample("Enderlook.MeshVoxelizer");
            await meshVoxelizer.Process();
            Profiler.EndSample();

            Memory<bool> voxels = meshVoxelizer.Voxels;

            Utility.UseMultithreading = false;

            Resolution r = new Resolution(resolution.Item1, resolution.Item2, resolution.Item3, bounds);
            heightField = await HeightField.Create(voxels, options);
            openHeightField = new CompactOpenHeightField(heightField, options);
            return;
            distanceField = new DistanceField(openHeightField, r);
            distanceField2 = distanceField.WithBlur(openHeightField, 1);
            regions = new RegionsField(distanceField, openHeightField, r, 0, 2);
            contours = new Contours(regions, openHeightField, r, 0);

            /*Profiler.BeginSample("Enderlook.Dispose");
            heightField.Dispose();
            openHeightField.Dispose();
            distanceField.Dispose();
            distanceField2.Dispose();
            regions.Dispose();
            contours.Dispose();
            Profiler.EndSample();*/
        }

        private void Time(Action a)
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            a();
            long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            q.Add(elapsedMilliseconds);
            Debug.Log($"{q.Average()} {q.Count} {elapsedMilliseconds}");
        }

        private void Compare(Action a, Action b)
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            a();
            long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            q.Add(elapsedMilliseconds);
            stopwatch.Stop();
            stopwatch.Reset();
            stopwatch.Start();
            b();
            long elapsedMilliseconds2 = stopwatch.ElapsedMilliseconds;
            q2.Add(elapsedMilliseconds2);
            double v = q.Average();
            double v1 = q2.Average();
            Debug.Log($"{v}/{v1} ({(v1 == 0 ? 0 : v/v1)}) {q.Count} {elapsedMilliseconds}/{elapsedMilliseconds2} ({(elapsedMilliseconds2 == 0 ? 0 : elapsedMilliseconds/elapsedMilliseconds2)})");
        }
    }
}
