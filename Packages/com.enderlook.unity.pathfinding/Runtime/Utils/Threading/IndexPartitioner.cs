using System;

namespace Enderlook.Unity.Pathfinding.Utils
{
    /// <summary>
    /// Partition large tasks in chunks.
    /// </summary>
    internal readonly struct IndexPartitioner
    {
        private const int ChunksPerCore = 3;
        private static readonly int Chunks = Info.ProcessorCount * ChunksPerCore;
        private static readonly int ChunksCount = Chunks + 1;

        private readonly int fromInclusive;
        private readonly int toExclusive;

        public int PartsCount => ChunksCount;

        private int Parts => Chunks;

        public IndexPartitioner(int fromInclusive, int toExclusive)
        {
            this.fromInclusive = fromInclusive;
            this.toExclusive = toExclusive;
        }

        public (int fromInclusive, int toExclusive) this[int index] {
            get {
                int length = (toExclusive - fromInclusive) / Parts;
                int from = length * index;
                int to = index == Parts ? toExclusive : from + length;
                return (from, to);
            }
        }

        public Span<T> Slice<T>(Span<T> span, int index)
        {
            int length = (toExclusive - fromInclusive) / Parts;
            int from = length * index;
            if (index == Parts)
                length = toExclusive - from;
            return span.Slice(from, length);
        }
    }
}