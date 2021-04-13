using Enderlook.Collections.Pooled;
using Enderlook.Unity.Jobs;

using System.Buffers;

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
                    for (int x = tuple.xMinMultiple; x < tuple.xMaxMultiple; x++)
                    {
                        for (int y = tuple.yMinMultiple; y < tuple.yMaxMultiple; y++)
                        {
                            for (int z = tuple.zMinMultiple; z < tuple.zMaxMultiple; z++)
                            {
                                int i = GetIndex(ref resolution, x, y, z);
                                destination[i] |= tuple.voxels[i];
                            }
                        }
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
