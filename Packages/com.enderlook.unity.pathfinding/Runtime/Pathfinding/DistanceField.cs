using Enderlook.Collections.Pooled.LowLevel;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

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
        /// <param name="openHeightField">Open heigh field whose distance field is being calculated.</param>
        /// <param name="options">Stores configuration information.</param>
        /// <return>New distance field.</return>
        public static async ValueTask<DistanceField> Create(CompactOpenHeightField openHeightField, MeshGenerationOptions options)
        {
            options.Validate();
            openHeightField.DebugAssert(nameof(openHeightField), options.Resolution_, $"{nameof(options)}.{nameof(options.Resolution_)}");

            int spansCount = openHeightField.SpansCount;
            byte[] status = ArrayPool<byte>.Shared.Rent(spansCount);
            try
            {
                Debug.Assert(STATUS_OPEN == 0, $"If this fail you must change the next line to perfom Array.Fill and set the content of the array to {nameof(STATUS_OPEN)}.");
                Array.Clear(status, 0, spansCount);
                ushort[] distances = ArrayPool<ushort>.Shared.Rent(spansCount);
                try
                {
                    ushort maximumDistance;
                    options.PushTask(2, "Distance Field");
                    {
                        RawPooledQueue<int> handeling = RawPooledQueue<int>.Create();
                        handeling = await FindInitialBorders(distances, handeling, status, openHeightField, options);
                        if (options.StepTaskAndCheckIfMustYield())
                            await options.Yield();
                        maximumDistance = await CalculateDistances(distances, handeling, status, openHeightField, options);
                        handeling.Dispose();
                        if (options.StepTaskAndCheckIfMustYield())
                            await options.Yield();
                    }
                    options.PopTask();
                    return new DistanceField(spansCount, distances, maximumDistance);
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

        private DistanceField(int spansCount, ushort[] distances, ushort maximumDistance)
        {
            this.spansCount = spansCount;
            this.distances = distances;
            MaximumDistance = maximumDistance;
        }

        /// <summary>
        /// Debug assert that this instance is valid.
        /// </summary>
        /// <param name="parameterName">Name of the instance.</param>
        [System.Diagnostics.Conditional("Debug")]
        public void DebugAssert(string parameterName, in Resolution resolution, string resolutionParameterName)
        {
            Debug.Assert(!(distances is null), $"{parameterName} is default");

            if (!(distances is null))
                Debug.Assert(spansCount == resolution.Cells2D, $"{parameterName} is not valid for the passed resolution {resolutionParameterName}.");
        }

        /// <summary>
        /// Creawtes a new distance field blurred.
        /// </summary>
        /// <param name="openHeightField">Open heigh field whose distance field is being calculated.</param>
        /// <param name="options">Stores configuration information.</param>
        /// <returns>New blurred distance field.</returns>
        public async ValueTask<DistanceField> WithBlur(CompactOpenHeightField openHeightField, MeshGenerationOptions options)
        {
            options.Validate();
            Debug.Assert(options.DistanceBlurThreshold > 0);

            int threshold = options.DistanceBlurThreshold * 2;

            ushort[] distances = this.distances;

            ushort newMaximumDistance = default;
            ushort[] newDistances = ArrayPool<ushort>.Shared.Rent(spansCount);
            try
            {
                options.PushTask(spansCount, "Blur Distance Field");
                {
                    if (options.UseMultithreading)
                        Parallel.For(0, spansCount, i =>
                        {
                            Blur(threshold, openHeightField.Spans, distances, ref newMaximumDistance, newDistances, i);
                            options.StepTask();
                        });
                    else
                        newMaximumDistance = await WithBlurSingleThread(openHeightField, threshold, distances, newMaximumDistance, newDistances, options);
                }
                options.PopTask();
                return new DistanceField(spansCount, newDistances, newMaximumDistance);
            }
            catch
            {
                ArrayPool<ushort>.Shared.Return(newDistances);
                throw;
            }
        }

        private static async ValueTask<ushort> WithBlurSingleThread(CompactOpenHeightField openHeightField, int threshold, ushort[] distances, ushort newMaximumDistance, ushort[] newDistances, MeshGenerationOptions options)
        {
            int spansCount = openHeightField.SpansCount;
            for (int i = 0; i < spansCount; i++)
            {
                Blur(threshold, openHeightField.Spans, distances, ref newMaximumDistance, newDistances, i);
                if (options.StepTaskAndCheckIfMustYield())
                    await options.Yield();
            }
            return newMaximumDistance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Blur(int threshold, ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, ushort[] distances, ref ushort newMaximumDistance, ushort[] newDistances, int i)
        {
            ref readonly CompactOpenHeightField.HeightSpan span = ref spans[i];
            ushort distance = distances[i];
            if (distance <= threshold)
                newDistances[i] = distance;
            else
            {
                int accumulatedDistance = distance;
                int doubleDistance = distance * 2;

                AcummulateDistance<Side.Left>(spans, span, ref accumulatedDistance, distance, doubleDistance, distances);
                AcummulateDistance<Side.Forward>(spans, span, ref accumulatedDistance, distance, doubleDistance, distances);
                AcummulateDistance<Side.Right>(spans, span, ref accumulatedDistance, distance, doubleDistance, distances);
                AcummulateDistance<Side.Backward>(spans, span, ref accumulatedDistance, distance, doubleDistance, distances);

                distance = (ushort)((accumulatedDistance + 5) / 9);
                newDistances[i] = distance;
            }
            newMaximumDistance = Math.Max(newMaximumDistance, distance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AcummulateDistance<T>(
            ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans,
            in CompactOpenHeightField.HeightSpan span,
            ref int accumulatedDistance,
            int distance,
            int doubleDistance,
            ushort[] distances)
        {
            Side.DebugAssert<T>();

            int j = span.GetSide<T>();
            if (j == CompactOpenHeightField.HeightSpan.NULL_SIDE)
                accumulatedDistance += doubleDistance;
            else
            {
                ref readonly CompactOpenHeightField.HeightSpan neighbourSpan = ref spans[j];
                int k;
                if (typeof(T) == typeof(Side.Left))
                    k = neighbourSpan.Right;
                else if (typeof(T) == typeof(Side.Right))
                    k = neighbourSpan.Forward;
                else if (typeof(T) == typeof(Side.Forward))
                    k = neighbourSpan.Backward;
                else
                {
                    Debug.Assert(typeof(T) == typeof(Side.Backward));
                    k = neighbourSpan.Left;
                }

                if (k == CompactOpenHeightField.HeightSpan.NULL_SIDE)
                    accumulatedDistance += distance;
                else
                    accumulatedDistance += distances[j];
            }
        }

        private static async ValueTask<RawPooledQueue<int>> FindInitialBorders(ushort[] distances, RawPooledQueue<int> handeling, byte[] status, CompactOpenHeightField openHeightField, MeshGenerationOptions options)
        {
            try
            {
                int length = openHeightField.SpansCount;
                if (unchecked((uint)length > (uint)distances.Length))
                {
                    Debug.Assert(false, "Index out of range.");
                    return handeling;
                }

                options.PushTask(length, "Find Initial Borders");
                {
                    for (int i = 0; i < length; i++)
                    {
                        if (openHeightField.Span(i).IsBorder)
                        {
                            status[i] = STATUS_IN_PROGRESS;
                            distances[i] = 0;
                            handeling.Enqueue(i);
                        }
                        if (options.StepTaskAndCheckIfMustYield())
                            await options.Yield();
                    }
                }
                options.PopTask();
                return handeling;
            }
            catch
            {
                handeling.Dispose();
                throw;
            }
        }

        private static async ValueTask<ushort> CalculateDistances(ushort[] distances, RawPooledQueue<int> handeling, byte[] status, CompactOpenHeightField openHeightField, MeshGenerationOptions options)
        {
            try
            {
                ushort maximumDistance = 0;
                options.PushTask(1, "Calculate Distances");
                {
                    while (handeling.TryDequeue(out int i))
                    {
                        CalculateDistancesBody(distances, ref handeling, status, openHeightField, i);

                        if (distances[i] > maximumDistance)
                            maximumDistance = distances[i];

                        status[i] = STATUS_CLOSED;

                        if (options.CheckIfMustYield())
                            await options.Yield();
                    }
                    options.StepTask();
                }
                options.PopTask();
                return maximumDistance;
            }
            finally
            {
                handeling.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CalculateDistancesBody(ushort[] distances, ref RawPooledQueue<int> handeling, byte[] status, CompactOpenHeightField openHeightField, int i)
        {
            ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans = openHeightField.Spans;
            ref readonly CompactOpenHeightField.HeightSpan span = ref spans[i];

            DistanceFieldCheckNeigbour(distances, ref handeling, status, spans, i, span.Left);
            DistanceFieldCheckNeigbour(distances, ref handeling, status, spans, i, span.Forward);
            DistanceFieldCheckNeigbour(distances, ref handeling, status, spans, i, span.Right);
            DistanceFieldCheckNeigbour(distances, ref handeling, status, spans, i, span.Backward);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DistanceFieldCheckNeigbour(ushort[] distances, ref RawPooledQueue<int> handeling, byte[] status, ReadOnlySpan<CompactOpenHeightField.HeightSpan> spans, int i, int neighbour)
        {
            if (neighbour != CompactOpenHeightField.HeightSpan.NULL_SIDE)
            {
                ref readonly CompactOpenHeightField.HeightSpan spanNeighbour = ref spans[neighbour];

                ref byte statusNeighbour = ref status[neighbour];
                // Don't use a switch statement because the Jitter doesn't inline them.
                if (statusNeighbour == STATUS_OPEN)
                {
                    handeling.Enqueue(neighbour);
                    statusNeighbour = STATUS_IN_PROGRESS;
                    distances[neighbour] = (ushort)(distances[i] + 1);
                }
                else if (statusNeighbour == STATUS_IN_PROGRESS)
                {
                    ref ushort distanceI = ref distances[i];
                    ref ushort distanceNeighbour = ref distances[neighbour];
                    if (distanceI + 1 < distanceNeighbour)
                        distanceNeighbour = distanceI;
                }
                else
                {
                    Debug.Assert(statusNeighbour == STATUS_CLOSED);
                    ref ushort distanceI = ref distances[i];
                    ref ushort distanceNeighbour = ref distances[neighbour];
                    if (distanceI + 1 < distanceNeighbour)
                    {
                        distanceNeighbour = (ushort)(distanceI + 1);
                        statusNeighbour = STATUS_IN_PROGRESS;
                        handeling.Enqueue(neighbour);
                    }
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