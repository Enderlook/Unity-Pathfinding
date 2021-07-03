using Enderlook.Collections.Pooled.LowLevel;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    /// <summary>
    /// Stores the distance field of a <see cref="CompactOpenHeightField"/>.
    /// </summary>
    internal readonly struct DistanceField : IDisposable
    {
        public readonly ushort MaximumDistance;
        private readonly int spansCount;
        private readonly ushort[] distances;

        public ReadOnlySpan<ushort> Distances => distances.AsSpan(0, spansCount);

        /* The status array must start with this value.
         * Currently in order to do that we are using Array.Empty method.
         * If you replace this value with any other than 0, you shall replicate it by replacing Array.Empty with Array.Fill. */
        private const int STATUS_OPEN = 0;
        private const int STATUS_IN_PROGRESS = 1;
        private const int STATUS_CLOSED = 2;

        /// <summary>
        /// Calculates the distance field of the specified open height field.
        /// </summary>
        /// <param name="openHeighField">Open heigh field whose distance field is being calculated.</param>
        public DistanceField(in CompactOpenHeightField openHeighField)
        {
            RawPooledQueue<int> handeling = RawPooledQueue<int>.Create();

            ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans = openHeighField.Spans;
            spansCount = spans.Length;
            byte[] status = ArrayPool<byte>.Shared.Rent(spansCount);
            try
            {
                Debug.Assert(STATUS_OPEN == 0, $"If this fail you must change the next line to perfom Array.Fill and set the content of the array to {nameof(STATUS_OPEN)}.");
                Array.Clear(status, 0, spansCount);
                distances = ArrayPool<ushort>.Shared.Rent(spansCount);
                try
                {
                    MaximumDistance = 0;
                    FindInitialBorders(ref handeling, status, spans);
                    CalculateDistances(ref MaximumDistance, ref handeling, status, spans);
                }
                catch
                {
                    ArrayPool<ushort>.Shared.Return(distances);
                    throw;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(status);
            }
        }

        private void FindInitialBorders(ref RawPooledQueue<int> handeling, byte[] status, ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans)
        {
            ushort[] distances = this.distances;
            if (unchecked((uint)spans.Length > (uint)distances.Length))
            {
                Debug.Assert(false, "Index out of range.");
                return;
            }

            for (int i = 0; i < spans.Length; i++)
            {
                if (spans[i].IsBorder)
                {
                    status[i] = STATUS_IN_PROGRESS;
                    distances[i] = 0;
                    handeling.Enqueue(i);
                }
            }
        }

        private void CalculateDistances(ref ushort maximumDistance, ref RawPooledQueue<int> handeling, byte[] status, ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans)
        {
            ushort[] distances = this.distances;
            while (handeling.TryDequeue(out int i))
            {
                ref readonly CompactOpenHeightField.HeightSpan span = ref spans[i];

                DistanceFieldCheckNeigbour(ref handeling, status, spans, i, span.Left);
                DistanceFieldCheckNeigbour(ref handeling, status, spans, i, span.Forward);
                DistanceFieldCheckNeigbour(ref handeling, status, spans, i, span.Right);
                DistanceFieldCheckNeigbour(ref handeling, status, spans, i, span.Backward);

                if (distances[i] > maximumDistance)
                    maximumDistance = distances[i];

                status[i] = STATUS_CLOSED;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DistanceFieldCheckNeigbour(ref RawPooledQueue<int> handeling, byte[] status, ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, int i, int neighbour)
        {
            if (neighbour != CompactOpenHeightField.HeightSpan.NULL_SIDE)
            {
                ref readonly CompactOpenHeightField.HeightSpan spanNeighbour = ref spans[neighbour];

                ref byte statusNeighbour = ref status[neighbour];
                switch (statusNeighbour)
                {
                    case STATUS_OPEN:
                        handeling.Enqueue(neighbour);
                        statusNeighbour = STATUS_IN_PROGRESS;
                        distances[neighbour] = (ushort)(distances[i] + 1);
                        break;
                    case STATUS_IN_PROGRESS:
                    {
                        ref ushort distanceI = ref distances[i];
                        ref ushort distanceNeighbour = ref distances[neighbour];
                        if (distanceI + 1 < distanceNeighbour)
                            distanceNeighbour = distanceI;
                        break;
                    }
                    case STATUS_CLOSED:
                    {
                        ref ushort distanceI = ref distances[i];
                        ref ushort distanceNeighbour = ref distances[neighbour];
                        if (distanceI + 1 < distanceNeighbour)
                        {
                            distanceNeighbour = (ushort)(distanceI + 1);
                            statusNeighbour = STATUS_IN_PROGRESS;
                            handeling.Enqueue(neighbour);
                        }
                        break;
                    }
                    default:
                        Debug.Assert(false, "Impossible state.");
                        goto case STATUS_CLOSED;
                }
            }
        }

        public void DrawGizmos(in Resolution resolution, in CompactOpenHeightField openHeightField)
        {
            if (MaximumDistance == 0)
                throw new InvalidOperationException();

            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(resolution.Center, new Vector3(resolution.CellSize.x * resolution.Width, resolution.CellSize.y * resolution.Height, resolution.CellSize.z * resolution.Depth));
            Vector3 offset = (new Vector3(resolution.Width * (-resolution.CellSize.x), resolution.Height * (-resolution.CellSize.y), resolution.Depth * (-resolution.CellSize).z) * .5f) + (resolution.CellSize * .5f);
            offset.y -= resolution.CellSize.y / 2;
            offset += resolution.Center;

            ushort[] distances = this.distances;
            ReadOnlySpan<CompactOpenHeightField.HeightColumn> columns = openHeightField.Columns;
            ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans = openHeightField.Spans;
            int i = 0;
            for (int x = 0; x < resolution.Width; x++)
            {
                for (int z = 0; z < resolution.Depth; z++)
                {
                    Vector2 position_ = new Vector2(x * resolution.CellSize.x, z * resolution.CellSize.z);
                    ref readonly CompactOpenHeightField.HeightColumn column = ref columns[i++];

                    if (!column.IsEmpty)
                    {
                        int j = column.First;

                        CompactOpenHeightField.HeightSpan heightSpan = spans[j];
                        if (heightSpan.Floor != -1)
                            Draw(resolution, heightSpan.Floor - .1f, distances[j] / (float)MaximumDistance);
                        j++;

                        for (; j < column.Last - 1; j++)
                        {
                            heightSpan = spans[j];
                            Draw(resolution, heightSpan.Floor - .1f, distances[j] / (float)MaximumDistance);
                        }

                        if (column.Count > 1) // Shouldn't this be 2?
                        {
                            Debug.Assert(j == column.Last - 1);
                            heightSpan = spans[j];
                            Draw(resolution, heightSpan.Floor, distances[j] / (float)MaximumDistance);
                        }
                    }

                    void Draw(in Resolution resolution_, float y, float lerp)
                    {
                        if (lerp == 0)
                            Gizmos.color = Color.black;
                        else
                            Gizmos.color = Color.Lerp(Color.red, Color.green, lerp);
                        Vector3 position = new Vector3(position_.x, resolution_.CellSize.y * y, position_.y);
                        Vector3 center_ = offset + position;
                        Vector3 size = new Vector3(resolution_.CellSize.x, resolution_.CellSize.y * .1f, resolution_.CellSize.z);
                        Gizmos.DrawCube(center_, size);
                    }
                }
            }
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose() => ArrayPool<ushort>.Shared.Return(distances);
    }
}