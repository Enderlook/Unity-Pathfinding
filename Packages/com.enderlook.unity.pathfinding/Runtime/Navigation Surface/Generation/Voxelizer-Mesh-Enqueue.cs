using Enderlook.Unity.Jobs;
using Enderlook.Unity.Pathfinding.Utils;

using System;
using System.Buffers;
using System.Collections.Generic;
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
            information.EnsureCapacity(meshFilters.Length);

            if (options.ShouldUseTimeSlice)
                return EnqueueAsync(meshFilters);
            else
                return EnqueueSync(meshFilters);
        }

        private async ValueTask<Voxelizer> EnqueueAsync(MeshFilter[] meshFilters)
        {
            information.EnsureCapacity(meshFilters.Length);
            options.PushTask(meshFilters.Length, "Enqueuing Meshes");
            {
                foreach (MeshFilter meshFilter in meshFilters)
                {
                    Enqueue(meshFilter);
                    await options.StepTaskAndYield();
                }
            }
            options.PopTask();
            return this;
        }

        private ValueTask<Voxelizer> EnqueueSync(MeshFilter[] meshFilters)
        {
            information.EnsureCapacity(meshFilters.Length);
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

            information.Add(new InformationElement(vertices, triangles, mesh.uv, transform.rotation, transform.localScale, transform.position));
        }

        private struct InformationElement
        {
            public ArraySlice<Vector3> Vertices;
            public int[] Triangles;
            public Vector2[] UV;
            public Vector3 Min;
            public Vector3 Max;
            public Quaternion Rotation;
            public Vector3 LocalScale;
            public Vector3 WorldPosition;

            public InformationElement(List<Vector3> vertices, int[] triangles, Vector2[] uv, Quaternion rotation, Vector3 localScale, Vector3 position)
            {
                Triangles = triangles;
                Rotation = rotation;
                LocalScale = localScale;
                WorldPosition = position;
                UV = uv;
                int count = vertices.Count;
                Vector3[] array = ArrayPool<Vector3>.Shared.Rent(count);
                vertices.CopyTo(array);
                Vertices = new ArraySlice<Vector3>(array, count);
                Min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                Max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            }

            public void Dispose()
                => ArrayPool<Vector3>.Shared.Return(Vertices.Array);
        }
    }
}