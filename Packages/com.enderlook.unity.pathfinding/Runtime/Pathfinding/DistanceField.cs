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
    internal readonly struct DistanceField
    {
        private readonly ushort maximumDistance;
        private readonly int spansCount;
        private readonly byte[] status;
        private readonly ushort[] distances;

        /* The status array must start with this value.
         * Currently in order to do that we are using Array.Empty method.
         * If you replace this value with any other than 0, you shall replcae Array.Empty with Array.Fill. */
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

            Span<CompactOpenHeightField.HeightSpan> spans = openHeighField.Spans;
            spansCount = spans.Length;
            status = ArrayPool<byte>.Shared.Rent(spansCount);
            try
            {
                Array.Clear(status, 0, spansCount);
                distances = ArrayPool<ushort>.Shared.Rent(spansCount);
                try
                {
                    maximumDistance = 0;

                    FindInitialBorders(ref handeling, spans);

                    CalculateDistances(ref maximumDistance, ref handeling, spans);
                }
                catch
                {
                    ArrayPool<ushort>.Shared.Return(distances);
                    throw;
                }
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(status);
                throw;
            }
        }

        private void FindInitialBorders(ref RawPooledQueue<int> handeling, Span<CompactOpenHeightField.HeightSpan> spans)
        {
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

        private void CalculateDistances(ref ushort maximumDistance, ref RawPooledQueue<int> handeling, Span<CompactOpenHeightField.HeightSpan> spans)
        {
            while (handeling.TryDequeue(out int i))
            {
                ref CompactOpenHeightField.HeightSpan span = ref spans[i];

                DistanceFieldCheckNeigbour(ref handeling, spans, i, span.Left);
                DistanceFieldCheckNeigbour(ref handeling, spans, i, span.Right);
                DistanceFieldCheckNeigbour(ref handeling, spans, i, span.Foward);
                DistanceFieldCheckNeigbour(ref handeling, spans, i, span.Backward);

                if (distances[i] > maximumDistance)
                    maximumDistance = distances[i];

                status[i] = STATUS_CLOSED;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DistanceFieldCheckNeigbour(ref RawPooledQueue<int> handeling, Span<CompactOpenHeightField.HeightSpan> spans, int i, int neighbour)
        {
            if (neighbour != CompactOpenHeightField.HeightSpan.NULL_SIDE)
            {
                ref CompactOpenHeightField.HeightSpan spanNeighbour = ref spans[neighbour];

                switch (status[i])
                {
                    case STATUS_OPEN:
                        handeling.Enqueue(neighbour);
                        status[neighbour] = STATUS_IN_PROGRESS;
                        distances[neighbour] = (ushort)(distances[i] + 1);
                        break;
                    case STATUS_IN_PROGRESS:
                        if (distances[i] + 1 < distances[neighbour])
                            distances[neighbour] = distances[i];
                        break;
                    case STATUS_CLOSED:
                        if (distances[i] + 1 < distances[neighbour])
                        {
                            distances[neighbour] = (ushort)(distances[i] + 1);
                            status[neighbour] = STATUS_IN_PROGRESS;
                            handeling.Enqueue(neighbour);
                        }
                        break;
                    default:
                        Debug.Assert(false, "Impossible state.");
                        goto case STATUS_CLOSED;
                }
            }
        }

        public void DrawGizmos(in Resolution resolution, in CompactOpenHeightField openHeightField)
        {
            if (maximumDistance == 0)
                throw new InvalidOperationException();

            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(resolution.Center, new Vector3(resolution.CellSize.x * resolution.Width, resolution.CellSize.y * resolution.Height, resolution.CellSize.z * resolution.Depth));
            Vector3 offset = (new Vector3(resolution.Width * (-resolution.CellSize.x), resolution.Height * (-resolution.CellSize.y), resolution.Depth * (-resolution.CellSize).z) * .5f) + (resolution.CellSize * .5f);
            offset.y -= resolution.CellSize.y / 2;
            offset += resolution.Center;

            Span<CompactOpenHeightField.HeightColumn> columns = openHeightField.Columns;
            Span<CompactOpenHeightField.HeightSpan> spans = openHeightField.Spans;
            int i = 0;
            for (int x = 0; x < resolution.Width; x++)
            {
                for (int z = 0; z < resolution.Depth; z++)
                {
                    Vector2 position_ = new Vector2(x * resolution.CellSize.x, z * resolution.CellSize.z);
                    ref CompactOpenHeightField.HeightColumn column = ref columns[i++];

                    if (!column.IsEmpty)
                    {
                        int j = column.First;

                        CompactOpenHeightField.HeightSpan heightSpan = spans[j];
                        if (heightSpan.Floor != -1)
                            Draw(resolution, heightSpan.Floor - .1f, distances[j] / (float)maximumDistance);
                        j++;

                        for (; j < column.Count - 1; j++)
                        {
                            heightSpan = spans[j];
                            Draw(resolution, heightSpan.Floor - .1f, distances[j] / (float)maximumDistance);
                        }

                        if (column.Count > 1) // Shouldn't this be 2?
                        {
                            Debug.Assert(j == column.Last - 1);
                            heightSpan = spans[j];
                            Draw(resolution, heightSpan.Floor, distances[j] / (float)maximumDistance);
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
    }
}