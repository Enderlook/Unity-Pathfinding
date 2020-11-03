using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    public class NavigationMesh : MonoBehaviour
    {
#pragma warning disable CS0649
        [SerializeField, Tooltip("Determines the region where the mesh is calculated.")]
        private Vector3 bound;

        [SerializeField, Tooltip("Determines which layers can contribute to the mesh.")]
        private LayerMask contributeLayers;
#pragma warning restore CS0649

        public MeshFilter filter;

        public GameObject p;

        private void OnDrawGizmos()
        {
            Vector2[] points = new Vector2[p.transform.childCount];
            int i = 0;
            foreach (Transform c in p.transform)
                points[i++] = new Vector2(c.position.x, c.position.z);

            List<Vector2> vertices = new List<Vector2>();
            ConvexHull.CalculateConvexHull(points, vertices);

            Vector3 a;
            Vector3 b = ToVector3(vertices[0]);
            for (int j = 1; j < vertices.Count; j++)
            {
                a = b;
                b = ToVector3(vertices[j]);
                Gizmos.DrawLine(a, b);
            }
            Gizmos.DrawLine(b, ToVector3(vertices[0]));

            vertices.Clear();
            ConvexHull.ChansAlgorithm.CalculateConvexHull(points);

            Gizmos.color = Color.blue;
            b = ToVector3(vertices[0]);
            for (int j = 1; j < vertices.Count; j++)
            {
                a = b;
                b = ToVector3(vertices[j]);
                Gizmos.DrawLine(a, b);
            }
            Gizmos.DrawLine(b, ToVector3(vertices[0]));
        }

        private Vector3 ToVector3(Vector2 vector2) => new Vector3(vector2.x, 0, vector2.y);
    }
}

