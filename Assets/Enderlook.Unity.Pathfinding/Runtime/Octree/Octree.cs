using System;
using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal sealed partial class Octree
    {
        [SerializeField]
        private Vector3 center;

        [SerializeField]
        private float size;

        [SerializeField]
        private byte subdivisions;

        private Dictionary<LocationCode, InnerOctant> octants;

        internal int OctantsCount => octants.Count;

        public Octree(Vector3 center, float size, byte subdivisions)
        {
            this.center = center;
            this.size = size;
            this.subdivisions = subdivisions;
            if (subdivisions > 9)
                throw new ArgumentOutOfRangeException(nameof(subdivisions), "Must be a value from 1 to 10.", subdivisions.ToString());
        }

        internal void Reset(Vector3 center, float size, byte subdivisions)
        {
            if (subdivisions > 9)
                throw new ArgumentOutOfRangeException(nameof(subdivisions), "Must be a value from 1 to 10.", subdivisions.ToString());

            octantsBytes = null;

            this.center = center;
            this.size = size;
            this.subdivisions = subdivisions;

            Clear();
        }

        private void Clear() => octants.Clear();
    }
}