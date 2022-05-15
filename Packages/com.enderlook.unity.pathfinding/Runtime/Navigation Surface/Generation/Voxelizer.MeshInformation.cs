using Enderlook.Unity.Pathfinding.Utils;

using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Generation
{
    internal partial struct Voxelizer
    {
        private struct MeshInformation : IMinMax
        {
            public ArraySlice<Vector3> Vertices;
            public int[] Triangles;
            public Vector2[] UV;
            public Quaternion Rotation;
            public Vector3 LocalScale;
            public Vector3 WorldPosition;

            public Vector3 Min { get; set; }
            public Vector3 Max { get; set; }

            public MeshInformation(List<Vector3> vertices, int[] triangles, Vector2[] uv, Quaternion rotation, Vector3 localScale, Vector3 position)
            {
                Triangles = triangles;
                Rotation = rotation;
                LocalScale = localScale;
                WorldPosition = position;
                UV = uv;
                Vertices = new ArraySlice<Vector3>(vertices.Count, false);
                vertices.CopyTo(Vertices.Array);
                Min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                Max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            }

            public void Dispose() => Vertices.Dispose();
        }
    }
}