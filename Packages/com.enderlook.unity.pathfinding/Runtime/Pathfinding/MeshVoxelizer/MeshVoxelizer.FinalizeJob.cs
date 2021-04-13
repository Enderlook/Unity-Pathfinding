using Enderlook.Collections.Pooled;
using Enderlook.Unity.Jobs;

namespace Enderlook.Unity.Pathfinding2
{
    internal partial struct MeshVoxelizer
    {
        private struct FinalizeJob : IManagedJob
        {
            private PooledStack<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)> stack;

            public FinalizeJob(PooledStack<(bool[] voxels, int xMinMultiple, int yMinMultiple, int zMinMultiple, int xMaxMultiple, int yMaxMultiple, int zMaxMultiple)> stack)
                => this.stack = stack;

            public void Execute() => stack.Dispose();
        }
    }
}
