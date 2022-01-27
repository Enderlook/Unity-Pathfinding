using Enderlook.Unity.Jobs;
using Enderlook.Unity.Pathfinding.Utils;

using System.Threading.Tasks;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Generation
{
    internal partial struct Voxelizer
    {
        /// <summary>
        /// Enqueues meshes to be voxelized.
        /// </summary>
        /// <param name="meshFilters">Meshes to voxelize.</param>
        public ValueTask<Voxelizer> Enqueue(MeshFilter[] meshFilters)
        {
            meshInformations.EnsureCapacity(meshFilters.Length);

            if (options.ShouldUseTimeSlice)
                return EnqueueAsync(meshFilters);
            else
                return EnqueueSync(meshFilters);
        }

        private async ValueTask<Voxelizer> EnqueueAsync(MeshFilter[] meshFilters)
        {
            meshInformations.EnsureCapacity(meshFilters.Length);
            options.PushTask(meshFilters.Length, "Enqueuing Meshes");
            {
                TimeSlicer timeSlicer = options.TimeSlicer;
                foreach (MeshFilter meshFilter in meshFilters)
                {
                    Enqueue(meshFilter);
                    options.StepTask();
                    await timeSlicer.Yield();
                }
            }
            options.PopTask();
            return this;
        }

        private ValueTask<Voxelizer> EnqueueSync(MeshFilter[] meshFilters)
        {
            meshInformations.EnsureCapacity(meshFilters.Length);
            options.PushTask(meshFilters.Length, "Enqueuing Meshes");
            {
                foreach (MeshFilter meshFilter in meshFilters)
                {
                    Enqueue(meshFilter);
                    options.StepTask();
                }
            }
            options.PopTask();
            return new ValueTask<Voxelizer>(this);
        }

        private void Enqueue(MeshFilter meshFilter)
        {
            GameObject gameObject = meshFilter.gameObject;
            if (!gameObject.activeInHierarchy || (includeLayers & 1 << gameObject.layer) == 0)
                return;

            // TODO: On Unity 2020 use Mesh.AcquireReadOnlyMeshData for zero-allocation.
            Transform transform = meshFilter.transform;
            Mesh mesh = meshFilter.sharedMesh;

            vertices.Clear();
            mesh.GetVertices(vertices);
            int[] triangles = mesh.triangles;

            meshInformations.Add(new MeshInformation(vertices, triangles, mesh.uv, transform.rotation, transform.localScale, transform.position));
        }
    }
}