using System;
using System.Buffers;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    /// <summary>
    /// Represent the open height field of a voxelization.
    /// </summary>
    internal partial struct OpenHeightField : IDisposable
    {
        private readonly HeightColumn[] columns;
        private readonly (int x, int y, int z) resolution;
        // Used by Distance
        private int maximumDistance;

        /// <summary>
        /// Creates a the open height field of a height field.
        /// </summary>
        /// <param name="heightField">Height field used to create open height field.</param>
        /// <param name="maxTraversableStep">Maximum amount of cells between two floors to be considered neighbours.</param>
        /// <param name="minTraversableHeight">Minimum height between a floor and a ceil to be considered traversable.</param>
        /// <returns>The open height field of the heigh field.</returns>
        public OpenHeightField(HeightField heightField, (int x, int y, int z) resolution, int maxTraversableStep, int minTraversableHeight)
        {
            maximumDistance = 0;
            this.resolution = resolution;//heightField.Resolution;

            Span<HeightSpan> stack;
            HeightSpan[] stackOwner;
            if (resolution.y * Unsafe.SizeOf<HeightSpan>() < sizeof(byte) * 1024)
            {
                stackOwner = null;
                unsafe
                {
                    HeightSpan* ptr = stackalloc HeightSpan[resolution.y];
                    stack = new Span<HeightSpan>(ptr, resolution.y);
                }
            }
            else
            {
                stackOwner = ArrayPool<HeightSpan>.Shared.Rent(resolution.y);
                stack = stackOwner;
            }

            try
            {
                ReadOnlySpan<HeightField.HeightColumn> columns = heightField.AsSpan();
                HeightColumn[] openColumns = ArrayPool<HeightColumn>.Shared.Rent(resolution.x * resolution.z);
                this.columns = openColumns;
                int index = 0;
                for (int x = 0; x < resolution.x; x++)
                {
                    for (int z = 0; z < resolution.z; z++)
                    {
                        Debug.Assert(index == GetIndex(x, z));
                        HeightField.HeightColumn column = columns[index];
                        ReadOnlySpan<HeightField.HeightSpan> spans = column.AsSpan();
                        Debug.Assert(spans.Length > 0);

                        HeightColumnBuilder openColumn = new HeightColumnBuilder(stack);
                        int i = 0;
                        int y = 0;

                        if (spans.Length > 1)
                        {
                            HeightField.HeightSpan span = spans[i++];

#if DEBUG
                            bool wasSolid = span.IsSolid;
#endif

                            if (!span.IsSolid)
                            {
                                /* Do we actually need to add this span?
                                 * If we remove it, everything works... the output is just a bit different,
                                 * maybe it doesn't mater */
                                const int floor = -1;
                                int ceil = y + span.Height;
                                openColumn.Add(floor, ceil);

                                // Regardless we remove above span, this line must stay!
                                y += span.Height;
                            }
                            else
                            {
                                int floor = y + span.Height;
                                if (spans.Length > 2)
                                {
                                    span = spans[i++];

#if DEBUG
                                    Debug.Assert(wasSolid != span.IsSolid);
                                    wasSolid = span.IsSolid;
#endif

                                    y += span.Height;
                                    int ceil = y;
                                    openColumn.Add(floor, ceil);
                                }
                                else
                                {
                                    Debug.Assert(i == spans.Length - 1);
#if DEBUG
                                    span = spans[i];
                                    Debug.Assert(wasSolid != span.IsSolid);
#endif
                                    const int ceil = -1;
                                    openColumn.Add(floor, ceil);

                                    goto end;
                                }
                            }

                            for (; i < spans.Length - 1; i++)
                            {
                                span = spans[i];
#if DEBUG
                                Debug.Assert(wasSolid != span.IsSolid);
                                wasSolid = span.IsSolid;
#endif
                                if (!span.IsSolid)
                                {
                                    int floor = y;
                                    y += span.Height;
                                    int ceil = y;
                                    openColumn.Add(floor, ceil);
                                }
                                else
                                    y += span.Height;
                            }

                            if (spans.Length > 2)
                            {
                                Debug.Assert(i == spans.Length - 1);
                                span = spans[i];
#if DEBUG
                                Debug.Assert(wasSolid != span.IsSolid);
#endif
                                if (!span.IsSolid)
                                {
                                    int floor = y;
                                    const int ceil = -1;
                                    openColumn.Add(floor, ceil);
                                }
                            }

                            end:
                            openColumns[index++] = openColumn.ToBuilt();
                        }
                        else
                            index++;
                    }
                }
                CalculateNeighbours(maxTraversableStep, minTraversableHeight);
            }
            finally
            {
                if (!(stackOwner is null))
                    ArrayPool<HeightSpan>.Shared.Return(stackOwner);
            }
        }

        private void CalculateNeighbours(int maxTraversableStep, int minTraversableHeight)
        {
            int xM = resolution.x - 1;
            int zM = resolution.z - 1;

            int index = CalculateNeighboursWhenXIs0(maxTraversableStep, minTraversableHeight);
            int x;
            for (x = 1; x < xM; x++)
            {
                index = CalculateNeighboursWhenZIs0(maxTraversableStep, minTraversableHeight, index, x);

                int z;
                for (z = 1; z < zM; z++)
                {
                    /* This is the true body of this function.
                     * All methods that starts with When...() are actually specializations of this body to avoid branching inside the loop.
                     * TODO: Does that actually improves perfomance? */

                    Debug.Assert(index == GetIndex(x, z));

                    HeightColumn column = columns[index];

                    Debug.Assert(index - resolution.z == GetIndex(x - 1, z));
                    HeightColumn left = columns[index - resolution.z];
                    Debug.Assert(index + resolution.z == GetIndex(x + 1, z));
                    HeightColumn righ = columns[index + resolution.z];
                    Debug.Assert(index - 1 == GetIndex(x, z - 1));
                    HeightColumn backward = columns[index - 1];
                    Debug.Assert(index + 1 == GetIndex(x, z + 1));
                    HeightColumn forward = columns[++index];

                    Span<HeightSpan> leftSpan = left.AsSpan();
                    Span<HeightSpan> rightSpan = righ.AsSpan();
                    Span<HeightSpan> forwardSpan = forward.AsSpan();
                    Span<HeightSpan> backwardSpan = backward.AsSpan();

                    Span<HeightSpan> columnSpan = column.AsSpan();
                    for (int i = 0; i < columnSpan.Length; i++)
                    {
                        // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                        // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                        ref HeightSpan span = ref columnSpan[i];

                        for (int j = 0; j < leftSpan.Length; j++)
                            if (span.PresentNeighbour(ref span.Left, j, leftSpan[j], maxTraversableStep, minTraversableHeight))
                                break;

                        for (int j = 0; j < rightSpan.Length; j++)
                            if (span.PresentNeighbour(ref span.Right, j, rightSpan[j], maxTraversableStep, minTraversableHeight))
                                break;

                        for (int j = 0; j < forwardSpan.Length; j++)
                            if (span.PresentNeighbour(ref span.Forward, j, forwardSpan[j], maxTraversableStep, minTraversableHeight))
                                break;

                        for (int j = 0; j < backwardSpan.Length; j++)
                            if (span.PresentNeighbour(ref span.Backward, j, backwardSpan[j], maxTraversableStep, minTraversableHeight))
                                break;
                    }
                }

                Debug.Assert(z == zM);
                Debug.Assert(z == resolution.z - 1);
                index = CalculateNeighboursWhenZIsZM(maxTraversableStep, minTraversableHeight, index, x, zM);
            }

            Debug.Assert(x == xM);
            Debug.Assert(x == resolution.x - 1);
            CalculateNeighboursWhenXIsXM(maxTraversableStep, minTraversableHeight, index, x);
        }

        private int CalculateNeighboursWhenXIs0(int maxTraversableStep, int minTraversableHeight)
        {
            int index = 0;
            const int x = 0;
            int zM = resolution.z - 1;

            index = CalculateNeighboursWhenXIs0AndZIs0(maxTraversableStep, minTraversableHeight, index);

            int z;
            for (z = 1; z < zM; z++)
            {
                Debug.Assert(index == GetIndex(x, z));

                HeightColumn column = columns[index];

                Debug.Assert(index + resolution.z == GetIndex(x + 1, z));
                HeightColumn righ = columns[index + resolution.z];
                Debug.Assert(index - 1 == GetIndex(x, z - 1));
                HeightColumn backward = columns[index - 1];
                Debug.Assert(index + 1 == GetIndex(x, z + 1));
                HeightColumn forward = columns[++index];

                Span<HeightSpan> rightSpan = righ.AsSpan();
                Span<HeightSpan> forwardSpan = forward.AsSpan();
                Span<HeightSpan> backwardSpan = backward.AsSpan();

                Span<HeightSpan> columnSpan = column.AsSpan();
                for (int i = 0; i < columnSpan.Length; i++)
                {
                    // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                    // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                    ref HeightSpan span = ref columnSpan[i];

                    for (int j = 0; j < rightSpan.Length; j++)
                        if (span.PresentNeighbour(ref span.Right, j, rightSpan[j], maxTraversableStep, minTraversableHeight))
                            break;

                    for (int j = 0; j < forwardSpan.Length; j++)
                        if (span.PresentNeighbour(ref span.Forward, j, forwardSpan[j], maxTraversableStep, minTraversableHeight))
                            break;

                    for (int j = 0; j < backwardSpan.Length; j++)
                        if (span.PresentNeighbour(ref span.Backward, j, backwardSpan[j], maxTraversableStep, minTraversableHeight))
                            break;
                }
            }

            Debug.Assert(z == zM);
            Debug.Assert(z == resolution.z - 1);
            index = CalculateNeighboursWhenXIs0AndZIsZM(maxTraversableStep, minTraversableHeight, index);

            return index;
        }

        private int CalculateNeighboursWhenXIs0AndZIs0(int maxTraversableStep, int minTraversableHeight, int index)
        {
            const int x = 0;
            const int z = 0;

            Debug.Assert(index == GetIndex(x, z));

            HeightColumn column = columns[index];

            Debug.Assert(index + resolution.z == GetIndex(x + 1, z));
            HeightColumn righ = columns[index + resolution.z];
            Debug.Assert(index + 1 == GetIndex(x, z + 1));
            HeightColumn forward = columns[++index];

            Span<HeightSpan> rightSpan = righ.AsSpan();
            Span<HeightSpan> forwardSpan = forward.AsSpan();

            Span<HeightSpan> columnSpan = column.AsSpan();
            for (int i = 0; i < columnSpan.Length; i++)
            {
                // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                ref HeightSpan span = ref columnSpan[i];

                for (int j = 0; j < rightSpan.Length; j++)
                    if (span.PresentNeighbour(ref span.Right, j, rightSpan[j], maxTraversableStep, minTraversableHeight))
                        break;

                for (int j = 0; j < forwardSpan.Length; j++)
                    if (span.PresentNeighbour(ref span.Forward, j, forwardSpan[j], maxTraversableStep, minTraversableHeight))
                        break;
            }

            return index;
        }

        private int CalculateNeighboursWhenXIs0AndZIsZM(int maxTraversableStep, int minTraversableHeight, int index)
        {
            const int x = 0;
            int z = resolution.z - 1;
            Debug.Assert(index == GetIndex(x, z));

            HeightColumn column = columns[index];

            Debug.Assert(index + resolution.z == GetIndex(x + 1, z));
            HeightColumn righ = columns[index + resolution.z];
            Debug.Assert(index - 1 == GetIndex(x, z - 1));
            HeightColumn backward = columns[index - 1];
            index++;

            Span<HeightSpan> rightSpan = righ.AsSpan();
            Span<HeightSpan> backwardSpan = backward.AsSpan();

            Span<HeightSpan> columnSpan = column.AsSpan();
            for (int i = 0; i < columnSpan.Length; i++)
            {
                // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                ref HeightSpan span = ref columnSpan[i];

                for (int j = 0; j < rightSpan.Length; j++)
                    if (span.PresentNeighbour(ref span.Right, j, rightSpan[j], maxTraversableStep, minTraversableHeight))
                        break;

                for (int j = 0; j < backwardSpan.Length; j++)
                    if (span.PresentNeighbour(ref span.Backward, j, backwardSpan[j], maxTraversableStep, minTraversableHeight))
                        break;
            }

            return index;
        }

        private int CalculateNeighboursWhenZIs0(int maxTraversableStep, int minTraversableHeight, int index, int x)
        {
            const int z = 0;

            Debug.Assert(index == GetIndex(x, z));

            HeightColumn column = columns[index];

            Debug.Assert(index - resolution.z == GetIndex(x - 1, z));
            HeightColumn left = columns[index - resolution.z];
            Debug.Assert(index + resolution.z == GetIndex(x + 1, z));
            HeightColumn righ = columns[index + resolution.z];
            Debug.Assert(index + 1 == GetIndex(x, z + 1));
            HeightColumn forward = columns[++index];

            Span<HeightSpan> leftSpan = left.AsSpan();
            Span<HeightSpan> rightSpan = righ.AsSpan();
            Span<HeightSpan> forwardSpan = forward.AsSpan();

            Span<HeightSpan> columnSpan = column.AsSpan();
            for (int i = 0; i < columnSpan.Length; i++)
            {
                // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                ref HeightSpan span = ref columnSpan[i];

                for (int j = 0; j < leftSpan.Length; j++)
                    if (span.PresentNeighbour(ref span.Left, j, leftSpan[j], maxTraversableStep, minTraversableHeight))
                        break;

                for (int j = 0; j < rightSpan.Length; j++)
                    if (span.PresentNeighbour(ref span.Right, j, rightSpan[j], maxTraversableStep, minTraversableHeight))
                        break;

                for (int j = 0; j < forwardSpan.Length; j++)
                    if (span.PresentNeighbour(ref span.Forward, j, forwardSpan[j], maxTraversableStep, minTraversableHeight))
                        break;
            }

            return index;
        }

        private int CalculateNeighboursWhenZIsZM(int maxTraversableStep, int minTraversableHeight, int index, int x, int z)
        {
            Debug.Assert(z == resolution.z - 1);
            Debug.Assert(index == GetIndex(x, z));

            HeightColumn column = columns[index];

            Debug.Assert(index - resolution.z == GetIndex(x - 1, z));
            HeightColumn left = columns[index - resolution.z];
            Debug.Assert(index + resolution.z == GetIndex(x + 1, z));
            HeightColumn righ = columns[index + resolution.z];
            Debug.Assert(index - 1 == GetIndex(x, z - 1));
            HeightColumn backward = columns[index - 1];
            index++;

            Span<HeightSpan> leftSpan = left.AsSpan();
            Span<HeightSpan> rightSpan = righ.AsSpan();
            Span<HeightSpan> backwardSpan = backward.AsSpan();

            Span<HeightSpan> columnSpan = column.AsSpan();
            for (int i = 0; i < columnSpan.Length; i++)
            {
                // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                ref HeightSpan span = ref columnSpan[i];

                for (int j = 0; j < leftSpan.Length; j++)
                    if (span.PresentNeighbour(ref span.Left, j, leftSpan[j], maxTraversableStep, minTraversableHeight))
                        break;

                for (int j = 0; j < rightSpan.Length; j++)
                    if (span.PresentNeighbour(ref span.Right, j, rightSpan[j], maxTraversableStep, minTraversableHeight))
                        break;

                for (int j = 0; j < backwardSpan.Length; j++)
                    if (span.PresentNeighbour(ref span.Backward, j, backwardSpan[j], maxTraversableStep, minTraversableHeight))
                        break;
            }

            return index;
        }

        private void CalculateNeighboursWhenXIsXM(int maxTraversableStep, int minTraversableHeight, int index, int x)
        {
            Debug.Assert(x == resolution.x - 1);
            int zM = resolution.z - 1;

            index = CalculateNeighboursWhenXIsXMAndZIs0(maxTraversableStep, minTraversableHeight, index, x);

            int z;
            for (z = 1; z < zM; z++)
            {
                Debug.Assert(index == GetIndex(x, z));

                HeightColumn column = columns[index];

                Debug.Assert(index - resolution.z == GetIndex(x - 1, z));
                HeightColumn left = columns[index - resolution.z];
                Debug.Assert(index - 1 == GetIndex(x, z - 1));
                HeightColumn backward = columns[index - 1];
                Debug.Assert(index + 1 == GetIndex(x, z + 1));
                HeightColumn forward = columns[++index];

                Span<HeightSpan> leftSpan = left.AsSpan();
                Span<HeightSpan> forwardSpan = forward.AsSpan();
                Span<HeightSpan> backwardSpan = backward.AsSpan();

                Span<HeightSpan> columnSpan = column.AsSpan();
                for (int i = 0; i < columnSpan.Length; i++)
                {
                    // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                    // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                    ref HeightSpan span = ref columnSpan[i];

                    for (int j = 0; j < leftSpan.Length; j++)
                        if (span.PresentNeighbour(ref span.Left, j, leftSpan[j], maxTraversableStep, minTraversableHeight))
                            break;

                    for (int j = 0; j < forwardSpan.Length; j++)
                        if (span.PresentNeighbour(ref span.Forward, j, forwardSpan[j], maxTraversableStep, minTraversableHeight))
                            break;

                    for (int j = 0; j < backwardSpan.Length; j++)
                        if (span.PresentNeighbour(ref span.Backward, j, backwardSpan[j], maxTraversableStep, minTraversableHeight))
                            break;
                }
            }

            Debug.Assert(z == zM);
            Debug.Assert(z == resolution.z - 1);
            CalculateNeighboursWhenXIsXMAndZIsZM(maxTraversableStep, minTraversableHeight, index, x, z);
        }

        private int CalculateNeighboursWhenXIsXMAndZIs0(int maxTraversableStep, int minTraversableHeight, int index, int x)
        {
            Debug.Assert(x == resolution.x - 1);
            const int z = 0;

            Debug.Assert(index == GetIndex(x, z));

            HeightColumn column = columns[index];

            Debug.Assert(index - resolution.z == GetIndex(x - 1, z));
            HeightColumn left = columns[index - resolution.z];
            Debug.Assert(index + 1 == GetIndex(x, z + 1));
            HeightColumn forward = columns[++index];

            Span<HeightSpan> leftSpan = left.AsSpan();
            Span<HeightSpan> forwardSpan = forward.AsSpan();

            Span<HeightSpan> columnSpan = column.AsSpan();
            for (int i = 0; i < columnSpan.Length; i++)
            {
                // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                ref HeightSpan span = ref columnSpan[i];

                for (int j = 0; j < leftSpan.Length; j++)
                    if (span.PresentNeighbour(ref span.Left, j, leftSpan[j], maxTraversableStep, minTraversableHeight))
                        break;

                for (int j = 0; j < forwardSpan.Length; j++)
                    if (span.PresentNeighbour(ref span.Forward, j, forwardSpan[j], maxTraversableStep, minTraversableHeight))
                        break;
            }

            return index;
        }

        private void CalculateNeighboursWhenXIsXMAndZIsZM(int maxTraversableStep, int minTraversableHeight, int index, int x, int z)
        {
            Debug.Assert(x == resolution.x - 1);
            Debug.Assert(z == resolution.z - 1);
            Debug.Assert(index == GetIndex(x, z));

            HeightColumn column = columns[index];

            Debug.Assert(index - resolution.z == GetIndex(x - 1, z));
            HeightColumn left = columns[index - resolution.z];
            Debug.Assert(index - 1 == GetIndex(x, z - 1));
            HeightColumn backward = columns[index - 1];

            Span<HeightSpan> leftSpan = left.AsSpan();
            Span<HeightSpan> backwardSpan = backward.AsSpan();

            Span<HeightSpan> columnSpan = column.AsSpan();
            for (int i = 0; i < columnSpan.Length; i++)
            {
                // TODO: This can be optimized so neighbour spans must not be iterated all the time.
                // TODO: This may also be optimized to divide the amount of checkings if the result of PresentNeighbour is also shared with the neighbour.

                ref HeightSpan span = ref columnSpan[i];

                for (int j = 0; j < leftSpan.Length; j++)
                    if (span.PresentNeighbour(ref span.Left, j, leftSpan[j], maxTraversableStep, minTraversableHeight))
                        break;

                for (int j = 0; j < backwardSpan.Length; j++)
                    if (span.PresentNeighbour(ref span.Backward, j, backwardSpan[j], maxTraversableStep, minTraversableHeight))
                        break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetIndex(int x, int z)
        {
            Debug.Assert(x >= 0);
            Debug.Assert(x < resolution.x);
            Debug.Assert(z >= 0);
            Debug.Assert(z < resolution.z);
            int index_ = (resolution.z * x) + z;
            Debug.Assert(index_ < resolution.x * resolution.z);
            return index_;
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            int length = resolution.x * resolution.z;
            for (int i = 0; i < length; i++)
                columns[i].Dispose();
            ArrayPool<HeightColumn>.Shared.Return(columns);
        }

        public void DrawGizmosOfOpenHeightField(Vector3 center, Vector3 cellSize, bool neightbours)
        {
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
                    int i_ = i;
                    ReadOnlySpan<HeightSpan> heightSpans = columns[i++].AsSpan();

                    if (heightSpans.Length > 0)
                    {
                        int j = 0;

                        HeightSpan heightSpan = heightSpans[j++];
                        if (heightSpan.Floor != HeightSpan.NULL_SIDE)
                            Draw(heightSpan.Floor - .1f, Color.green);
                        Draw(heightSpan.Ceil + .1f, Color.red);
                        Draw2(heightSpan, columns, i_, resolution);

                        for (; j < heightSpans.Length - 1; j++)
                        {
                            heightSpan = heightSpans[j];
                            Draw(heightSpan.Floor - .1f, Color.green);
                            Draw(heightSpan.Ceil + .1f, Color.red);
                            Draw2(heightSpan, columns, i_, resolution);
                        }

                        if (heightSpans.Length > 1) // Shouldn't this be 2?
                        {
                            Debug.Assert(j == heightSpans.Length - 1);
                            heightSpan = heightSpans[j];
                            Draw(heightSpan.Floor, Color.green);
                            if (heightSpan.Ceil != HeightSpan.NULL_SIDE)
                                Draw(heightSpan.Ceil + .1f, Color.red);
                            Draw2(heightSpan, columns, i_, resolution);
                        }
                    }

                    void Draw(float y, Color color)
                    {
                        Gizmos.color = color;
                        Vector3 position = new Vector3(position_.x, cellSize.y * y, position_.y);
                        Vector3 center_ = offset + position;
                        Vector3 size = new Vector3(cellSize.x, cellSize.y * .1f, cellSize.z);
                        Gizmos.DrawCube(center_, size);
                    }

                    void Draw2(HeightSpan span, HeightColumn[] columns, int index, (int x, int y, int z) resolution)
                    {
                        if (!neightbours)
                            return;
                        Gizmos.color = Color.yellow;
                        unsafe
                        {
                            if (span.Left != HeightSpan.NULL_SIDE)
                            {
                                Debug.Assert(index - resolution.z == GetIndex(x - 1, z));
                                HeightColumn column = columns[index - resolution.z];
                                HeightSpan span_ = column.AsSpan()[span.Left];
                                Draw3(span_.Floor, span.Floor, HeightSpan.NULL_SIDE, 0);
                            }

                            if (span.Right != HeightSpan.NULL_SIDE)
                            {
                                Debug.Assert(index + resolution.z == GetIndex(x + 1, z));
                                HeightColumn column = columns[index + resolution.z];
                                HeightSpan span_ = column.AsSpan()[span.Right];
                                Draw3(span_.Floor, span.Floor, 1, 0);
                            }

                            if (span.Backward != HeightSpan.NULL_SIDE)
                            {
                                Debug.Assert(index - 1 == GetIndex(x, z - 1));
                                HeightColumn column = columns[index - 1];
                                HeightSpan span_ = column.AsSpan()[span.Backward];
                                Draw3(span_.Floor, span.Floor, 0, HeightSpan.NULL_SIDE);
                            }

                            if (span.Forward != HeightSpan.NULL_SIDE)
                            {
                                Debug.Assert(index + 1 == GetIndex(x, z + 1));
                                HeightColumn column = columns[index + 1];
                                HeightSpan span_ = column.AsSpan()[span.Forward];
                                Draw3(span_.Floor, span.Floor, 0, 1);
                            }
                        }

                        int GetIndex(int x_, int z_)
                        {
                            int index_ = (resolution.z * x_) + z_;
                            Debug.Assert(index_ < resolution.x * resolution.z);
                            return index_;
                        }

                        void Draw3(int yTo, float yFrom, int x_, int z_)
                        {
                            Vector3 positionFrom = new Vector3(position_.x, cellSize.y * yFrom, position_.y);
                            Vector3 centerFrom = offset + positionFrom;

                            Vector3 position__ = new Vector2((x + x_) * cellSize.x, (z + z_) * cellSize.z);
                            Vector3 positionTo = new Vector3(position__.x, cellSize.y * yTo, position__.y);
                            Vector3 centerTo = offset + positionTo;

                            Gizmos.DrawLine(centerFrom, centerTo);
                        }
                    }
                }
            }
        }

        internal struct HeightColumn : IDisposable
        {
            private HeightSpan[] spans;
            private int count;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeightColumn(Span<HeightSpan> spans)
            {
                this.spans = ArrayPool<HeightSpan>.Shared.Rent(spans.Length);
                spans.CopyTo(this.spans);
                count = spans.Length;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Span<HeightSpan> AsSpan() => spans.AsSpan(0, count);

            /// <inheritdoc cref="IDisposable.Dispose"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                HeightSpan[] array = spans;
                spans = null;
                if (!(array is null))
                    ArrayPool<HeightSpan>.Shared.Return(array);
            }
        }

        internal ref struct HeightColumnBuilder
        {
            private Span<HeightSpan> spans;
            private int count;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeightColumnBuilder(Span<HeightSpan> spans)
            {
                this.spans = spans;
                count = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Add(int floor, int ceil) => spans[count++] = new HeightSpan(floor, ceil);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeightColumn ToBuilt() => new HeightColumn(spans.Slice(0, count));
        }

        internal struct HeightSpan
        {
            // Value of Floor, Ceil, Left, Forward, Right, Backward when is null.
            public const int NULL_SIDE = -1;
            public const byte NULL_REGION = 0;

            public int Floor;
            public int Ceil;

            public int Left;
            public int Forward;
            public int Right;
            public int Backward;

            // Note: This field is only used during calculation of distance field.
            public SpanStatus Status;
            // Used by DistanceField.
            public ushort Distance;

            // Used by Region.
            public ushort Region;

            // Note: This field is only used during calculation of contour.
            public byte FlaggedEdges;
            public const byte LEFT_FLAG = 1 << 1;
            public const byte FORWARD_FLAG = 1 << 2;
            public const byte RIGHT_FLAG = 1 << 3;
            public const byte BACKWARD_FLAG = 1 << 4;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeightSpan(int floor, int ceil)
            {
                Floor = floor;
                Ceil = ceil;
                Left = NULL_SIDE;
                Forward = NULL_SIDE;
                Right = NULL_SIDE;
                Backward = NULL_SIDE;
                Status = SpanStatus.Open;
                Distance = 0;
                Region = NULL_REGION;
                FlaggedEdges = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool PresentNeighbour(ref int side, int neighbourIndex, HeightSpan neighbourSpan, int maxTraversableStep, int minTraversableHeight)
            {
                if (Floor == NULL_SIDE || neighbourSpan.Floor == NULL_SIDE)
                    return false;

                if (Math.Abs(Floor - neighbourSpan.Floor) <= maxTraversableStep)
                {
                    if (Ceil == NULL_SIDE || neighbourSpan.Ceil == NULL_SIDE || Math.Min(Ceil, neighbourSpan.Ceil) - Math.Max(Floor, neighbourSpan.Floor) >= minTraversableHeight)
                    {
                        Debug.Assert(side == NULL_SIDE);
                        side = neighbourIndex;
                        return true;
                    }
                }
                return false;
            }
        }

        internal enum SpanStatus : byte
        {
            Open = 0,
            InProgress = 1,
            Closed = 2,
        }
    }
}