using Enderlook.Collections.Pooled.LowLevel;

using System;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    internal partial struct OpenHeightField
    {
        public void CalculateDistanceField()
        {
            // TODO: In the future we may use an struct based pooled queue to reduce allocations.
            RawPooledQueue<(int xz, int y)> handeling = RawPooledQueue<(int xz, int y)>.Create();

            // Find initial borders.
            int length = resolution.x * resolution.z;
            for (int i = 0; i < length; i++)
            {
                HeightColumn column = columns[i];
                Span<HeightSpan> spans = column.AsSpan();

                for (int j = 0; j < spans.Length; j++)
                {
                    ref HeightSpan span = ref spans[j];
                    // TODO: Is this actually faster than 4 if statements?
                    bool isBorder = (span.Left | span.Foward | span.Rigth | span.Backward) == -1;
                    if (isBorder)
                    {
                        // A border is any span with less than 4 neighbours.
                        span.Status = SpanStatus.InProgress;
                        Debug.Assert(span.Distance == 0, "If this is not 0 we should zeroed it manually here.");
                        handeling.Enqueue((i, j));
                    }
                }
            }

            Debug.Assert(maximumDistance == 0, "If this is not 0 we should zeroed it manually here.");
            while (handeling.TryDequeue(out (int xz, int y) value))
            {
                (int xz, int y) = value;
                HeightColumn column = columns[xz];
                Span<HeightSpan> spans = column.AsSpan();
                ref HeightSpan span = ref spans[y];

                DistanceFieldCheckNeigbour(ref handeling, xz, ref span, span.Left, -resolution.z);
                DistanceFieldCheckNeigbour(ref handeling, xz, ref span, span.Rigth, resolution.z);
                DistanceFieldCheckNeigbour(ref handeling, xz, ref span, span.Foward, 1);
                DistanceFieldCheckNeigbour(ref handeling, xz, ref span, span.Backward, -1);

                if (span.Distance > maximumDistance)
                    maximumDistance = span.Distance;

                span.Status = SpanStatus.Closed;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DistanceFieldCheckNeigbour(ref RawPooledQueue<(int xz, int y)> handeling, int xz, ref HeightSpan span, int neighbourY, int neighbourXZDifference)
        {
            if (neighbourY != -1)
            {
                int neighbourXZ = xz + neighbourXZDifference;
                Debug.Assert(neighbourXZ >= 0);
                Debug.Assert(neighbourXZ < resolution.x * resolution.z);

                HeightColumn columnNeighbour = columns[neighbourXZ];
                Span<HeightSpan> spansNeighbour = columnNeighbour.AsSpan();
                ref HeightSpan spanNeighbour = ref spansNeighbour[neighbourY];

                switch (spanNeighbour.Status)
                {
                    case SpanStatus.Open:
                        handeling.Enqueue((neighbourXZ, neighbourY));
                        spanNeighbour.Status = SpanStatus.InProgress;
                        spanNeighbour.Distance = span.Distance + 1;
                        break;
                    case SpanStatus.InProgress:
                        if (span.Distance + 1 < spanNeighbour.Distance)
                            spanNeighbour.Distance = span.Distance;
                        break;
                    case SpanStatus.Closed:
                        if (span.Distance + 1 < spanNeighbour.Distance)
                        {
                            spanNeighbour.Distance = span.Distance + 1;
                            spanNeighbour.Status = SpanStatus.InProgress;
                            handeling.Enqueue((neighbourXZ, neighbourY));
                        }
                        break;
                    default:
                        Debug.Assert(false, "Impossible state.");
                        goto case SpanStatus.Closed;
                }
            }
        }

        public void DrawGizmosOfDistanceHeighField(Vector3 center, Vector3 cellSize)
        {
            if (maximumDistance == 0)
                throw new InvalidOperationException();

            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(center, new Vector3(cellSize.x * resolution.x, cellSize.y * resolution.y, cellSize.z * resolution.z));
            Vector3 offset = (new Vector3(resolution.x * (-cellSize.x), resolution.y * (-cellSize.y), resolution.z * (-cellSize).z) * .5f) + (cellSize * .5f);
            offset.y -= cellSize.y / 2;
            offset += center;

            int i = 0;
            for (int x = 0; x < resolution.x; x++)
            {
                for (int z = 0; z < resolution.z; z++)
                {
                    Vector2 position_ = new Vector2(x * cellSize.x, z * cellSize.z);
                    ReadOnlySpan<HeightSpan> heightSpans = columns[i++].AsSpan();

                    if (heightSpans.Length > 0)
                    {
                        int j = 0;

                        HeightSpan heightSpan = heightSpans[j++];
                        if (heightSpan.Floor != -1)
                            Draw(heightSpan.Floor - .1f, heightSpan.Distance / (float)maximumDistance);

                        for (; j < heightSpans.Length - 1; j++)
                        {
                            heightSpan = heightSpans[j];
                            Draw(heightSpan.Floor - .1f, heightSpan.Distance / (float)maximumDistance);
                        }

                        if (heightSpans.Length > 1) // Shouldn't this be 2?
                        {
                            Debug.Assert(j == heightSpans.Length - 1);
                            heightSpan = heightSpans[j];
                            Draw(heightSpan.Floor, heightSpan.Distance / (float)maximumDistance);
                        }
                    }

                    void Draw(float y, float lerp)
                    {
                        Gizmos.color = Color.Lerp(Color.red, Color.green, lerp);
                        Vector3 position = new Vector3(position_.x, cellSize.y * y, position_.y);
                        Vector3 center_ = offset + position;
                        Vector3 size = new Vector3(cellSize.x, cellSize.y * .1f, cellSize.z);
                        Gizmos.DrawCube(center_, size);
                    }
                }
            }
        }
    }
}