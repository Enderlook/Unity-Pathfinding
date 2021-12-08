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

        /// <summary>
        /// Number of parts that spans are splitted into.
        /// </summary>
        public int PartsCount => ChunksCount;

        private int Parts => Chunks;

        /// <summary>
        /// Construct a new partitioner.
        /// </summary>
        /// <param name="fromInclusive">Initial inclusive index.</param>
        /// <param name="toExclusive">Final exclusive index.</param>
        public IndexPartitioner(int fromInclusive, int toExclusive)
        {
            this.fromInclusive = fromInclusive;
            this.toExclusive = toExclusive;
        }

        /// <summary>
        /// Get the owned range from the specified <paramref name="index"/>.
        /// </summary>
        /// <param name="index">Requested chunk.</param>
        /// <returns>Owned range of the specified index.</returns>
        public (int fromInclusive, int toExclusive) this[int index] {
            get {
                int length = (toExclusive - fromInclusive) / Parts;
                int from = length * index;
                int to = index == Parts ? toExclusive : from + length;
                return (from, to);
            }
        }

        /// <summary>
        /// Extract the owned portion of the <paramref name="span"/> by the given <paramref name="index"/>.
        /// </summary>
        /// <typeparam name="T">Type of element in <paramref name="span"/>.</typeparam>
        /// <param name="span"><see cref="Span{T}"/> of elements to split.</param>
        /// <param name="index">Requested chunk index.</param>
        /// <returns>Owned slice of the <paramref name="span"/>.</returns>
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