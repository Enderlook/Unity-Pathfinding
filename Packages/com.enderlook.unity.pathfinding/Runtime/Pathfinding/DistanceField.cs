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
            status = ArrayPool<byte>.Shared.Rent(spans.Length);
            try
            {
                distances = ArrayPool<ushort>.Shared.Rent(spans.Length);
                try
                {
                    Array.Clear(status, 0, spans.Length);
                    Array.Clear(distances, 0, spans.Length);

                    // Find initial borders.
                    for (int i = 0; i < spans.Length; i++)
                    {
                        ref CompactOpenHeightField.HeightSpan span = ref spans[i];
                        // TODO: Is this actually faster than 4 if statements?
                        bool isBorder = (span.Left | span.Foward | span.Right | span.Backward) == -1;
                        if (isBorder)
                        {
                            // A border is any span with less than 4 neighbours.
                            status[i] = STATUS_IN_PROGRESS;
                            distances[i] = 0; // We zeroed it because ArrayPool doesn't guaranted zeroed arrays.
                            handeling.Enqueue(i);
                        }
                    }

                    maximumDistance = 0;
                    while (handeling.TryDequeue(out int i))
                    {
                        ref CompactOpenHeightField.HeightSpan span = ref spans[i];

                        DistanceFieldCheckNeigbour(spans, ref handeling, i, span.Left);
                        DistanceFieldCheckNeigbour(spans, ref handeling, i, span.Right);
                        DistanceFieldCheckNeigbour(spans, ref handeling, i, span.Foward);
                        DistanceFieldCheckNeigbour(spans, ref handeling, i, span.Backward);

                        ushort distance = distances[i];
                        if (distance > maximumDistance)
                            maximumDistance = distance;

                        status[i] = STATUS_CLOSED;
                    }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DistanceFieldCheckNeigbour(Span<CompactOpenHeightField.HeightSpan> spans, ref RawPooledQueue<int> handeling, int i, int neighbour)
        {
            if (neighbour != CompactOpenHeightField.HeightSpan.NULL_SIDE)
            {
                ref CompactOpenHeightField.HeightSpan spanNeighbour = ref spans[neighbour];

                switch (distances[neighbour])
                {
                    case STATUS_OPEN:
                        handeling.Enqueue(neighbour);
                        status[neighbour] = STATUS_IN_PROGRESS;
                        distances[neighbour] = (ushort)(distances[i] + 1);
                        break;
                    case STATUS_IN_PROGRESS:
                    {
                        ushort currentDistance = distances[i];
                        ref ushort neighbourDistance = ref distances[neighbour];
                        if (currentDistance + 1 < neighbourDistance)
                            neighbourDistance = currentDistance;
                        break;
                    }
                    case STATUS_CLOSED:
                    {
                        ushort currentDistance = distances[i];
                        ref ushort neighbourDistance = ref distances[neighbour];
                        if (currentDistance + 1 < neighbourDistance)
                        {
                            neighbourDistance = (ushort)(currentDistance + 1);
                            status[neighbour] = STATUS_IN_PROGRESS;
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

        public void DrawGizmosOfDistanceHeightField(in Resolution resolution, in CompactOpenHeightField openHeightField)
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