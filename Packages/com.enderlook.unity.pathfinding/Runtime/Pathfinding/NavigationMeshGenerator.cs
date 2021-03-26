using UnityEngine;

using Enderlook.Unity.Utils;

namespace Enderlook.Unity.Pathfinding2
{
    /// <summary>
    /// Navigation mesh generator.
    /// </summary>
    /*public partial class NavigationMeshGenerator
    {
        private static readonly Collider[] colliderArraySentinel = new Collider[1];
        private Configuration configuration;

        public NavigationMeshGenerator(Configuration configuration) => this.configuration = configuration;

        public void Test()
        {
            foreach (MeshFilter filter in Object.FindObjectsOfType<MeshFilter>())
            {
                if (!filter.gameObject.IsContainedIn(configuration.ObstaclesMask))
                    continue;
                if (!filter.gameObject.activeSelf)
                    continue;
                if (!filter.TryGetComponent(out MeshRenderer renderer) || !renderer.enabled)
                    continue;
            }



            using (HeightField heightField = HeightField.Create(configuration.ObstaclesMask, configuration.Volume, new Vector3(configuration.CellSize, configuration.CellHeight, configuration.CellSize), configuration.Center))
                heightField.DrawGizmos(false);
        }
    }*/
}