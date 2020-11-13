using Enderlook.Collections;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree
    {
        /* Octree Representation Data Structures
         *  https://geidav.wordpress.com/2014/08/18/advanced-octrees-2-node-representations/
         * 
         * Implementation of Hashed Octree, Morton Code, and optimized dual generation by static strategy
         *  http://www.cs.jhu.edu/~misha/ReadingSeminar/Papers/Lewiner10.pdf
         * 
         * Simple 3D and 2D Morton Code
         *  https://fgiesen.wordpress.com/2009/12/13/decoding-morton-codes/
         * 
         * Efficient implementation of Morton Code
         *  https://www.forceflow.be/2013/10/07/morton-encodingdecoding-through-bit-interleaving-implementations/
         * 
         * Operations with Morton Code
         *  http://asgerhoedt.dk/?p=276
         *  http://codervil.blogspot.com/2015/10/octree-node-identifiers.html
         * 
         * Others
         *  https://ascane.github.io/assets/portfolio/pathfinding3d-report.pdf
         *  https://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.456.2800&rep=rep1&type=pdf
         *  https://arxiv.org/pdf/1712.00408.pdf
         *  https://www.researchgate.net/publication/321487917_Binarized_octree_generation_for_Cartesian_adaptive_mesh_refinement_around_immersed_geometries
         */

        private Vector3 center;

        private float size;

        private byte subdivisions;

        private LayerMask filterInclude;

        private QueryTriggerInteraction query;

        private ConnectionType connectionType;

        private int MaxDepth => subdivisions + 1;

        public Octree(Vector3 center, float size, byte subdivisions, LayerMask filterInclude, bool includeTriggerColliders, ConnectionType connectionType)
        {
            if (subdivisions > 9)
                throw new ArgumentOutOfRangeException(nameof(subdivisions), "Must be a value from 1 to 10.", subdivisions.ToString());

            this.center = center;
            this.size = size;
            this.subdivisions = subdivisions;
            this.filterInclude = filterInclude;
            query = includeTriggerColliders ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            this.connectionType = connectionType;
            distances = new ConcurrentDictionary<(OctantCode, OctantCode), float>();
            lineOfSigths = new ConcurrentDictionary<(Vector3, Vector3), bool>();
            kdTree = new Vector3Tree<OctantCode>(new D3TreeFloat<OctantCode>());
        }

        internal void Reset(Vector3 center, float size, byte subdivisions, LayerMask filterInclude, bool includeTriggerColliders, ConnectionType connectionType)
        {
            if (subdivisions > 9)
                throw new ArgumentOutOfRangeException(nameof(subdivisions), "Must be a value from 1 to 10.", subdivisions.ToString());

            this.center = center;
            this.size = size;
            this.subdivisions = subdivisions;
            this.filterInclude = filterInclude;
            query = includeTriggerColliders ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            this.connectionType = connectionType;

            ClearCollections();
        }

        private void ClearCollections()
        {
            if (octants is null)
                octants = new Dictionary<OctantCode, Octant>();
            else
                octants.Clear();

            if (connections is null)
                connections = new Dictionary<OctantCode, HashSet<OctantCode>>();
            else
                connections.Clear();

            if (distances is null)
                distances = new ConcurrentDictionary<(OctantCode, OctantCode), float>();
            else
                distances.Clear();

            if (lineOfSigths is null)
                lineOfSigths = new ConcurrentDictionary<(Vector3, Vector3), bool>();
            else
                lineOfSigths.Clear();

            if (kdTree.IsDefault)
                kdTree = new Vector3Tree<OctantCode>(new D3TreeFloat<OctantCode>());
            else
                kdTree.Clear();
        }
    }
}