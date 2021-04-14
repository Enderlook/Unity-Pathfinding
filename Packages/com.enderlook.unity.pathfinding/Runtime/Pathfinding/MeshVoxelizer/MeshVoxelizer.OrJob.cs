using Enderlook.Collections.Pooled;
using Enderlook.Unity.Jobs;

using System.Buffers;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    internal partial struct MeshVoxelizer
    {
        private struct OrJob : IManagedJob
        {
            private bool[] destination;
            private (int x, int y, int z) resolution;
            private PooledStack<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)> stack;

            public OrJob(bool[] destination, (int x, int y, int z) resolution, PooledStack<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)> stack)
            {
                this.stack = stack;
                this.destination = destination;
                this.resolution = resolution;
            }

            public void Execute()
            {
                (bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple) tuple;

                lock (stack)
                    tuple = stack.Pop();

                try
                {
                    int index = resolution.z * (resolution.y * tuple.xMinMultiple);
                    for (int x = tuple.xMinMultiple; x < tuple.xMaxMultiple; x++)
                    {
                        index += resolution.z * tuple.yMinMultiple;
                        for (int y = tuple.yMinMultiple; y < tuple.yMaxMultiple; y++)
                        {
                            index += tuple.zMinMultiple;
                            for (int z = tuple.zMinMultiple; z < tuple.zMaxMultiple; z++)
                            {
                                Debug.Assert(index == GetIndex(ref resolution, x, y, z));
                                destination[index] |= tuple.voxels[index];
                                index++;
                            }
                            index += resolution.z - tuple.zMaxMultiple;
                        }
                        index += resolution.z * (resolution.y - tuple.yMaxMultiple);
                    }
                }
                finally
                {
                    ArrayPool<bool>.Shared.Return(tuple.voxels);
                }
            }
        }
    }
}
