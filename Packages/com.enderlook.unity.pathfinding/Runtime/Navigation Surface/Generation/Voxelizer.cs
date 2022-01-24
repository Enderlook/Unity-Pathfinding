using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Pools;
using Enderlook.Unity.Jobs;
using Enderlook.Unity.Pathfinding.Utils;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Generation
{
    /// <summary>
    /// A voxelizer of multiple meshes and colliders.
    /// </summary>
    internal partial struct Voxelizer : IDisposable
    {
        private readonly NavigationGenerationOptions options;
        private List<Vector3> vertices;
        private RawPooledList<InformationElement> information;
        private float voxelSize;
        private LayerMask includeLayers;
        private bool[] voxels;

        /// <summary>
        /// Voxelization result.
        /// </summary>
        public ReadOnlyArraySlice<bool> Voxels => new ReadOnlyArraySlice<bool>(voxels, options.VoxelizationParameters.VoxelsCount);

        /// <summary>
        /// Creates a new voxelizer of meshes.
        /// </summary>
        /// <param name="options">Stores configuration information.</param>
        public Voxelizer(NavigationGenerationOptions options, float voxelSize, LayerMask includeLayers)
        {
            this.options = options;
            this.voxelSize = voxelSize;
            this.includeLayers = includeLayers;
            information = RawPooledList<InformationElement>.Create();
            vertices = ObjectPool<List<Vector3>>.Shared.Rent();
            voxels = null;
        }

        /// <summary>
        /// Voxelizes all enqueued meshes.
        /// </summary>
        public ValueTask<Voxelizer> Process()
        {
            if (information.Count == 0)
            {
                options.SetVoxelizationParameters(0, default, default);
                voxels = ArrayPool<bool>.Shared.Rent(0);
                return new ValueTask<Voxelizer>(this);
            }

            if (options.UseMultithreading && information.Count > 1)
                return ProcessMultiThread();
            else if (options.ShouldUseTimeSlice)
                return ProcessSingleThread<Toggle.Yes>();
            else
                return ProcessSingleThread<Toggle.No>();
        }

        private async ValueTask<Voxelizer> ProcessSingleThread<TYield>()
        {
            int count = information.Count;

            options.PushTask(2, "Calculate Voxelization");
            {
                options.PushTask(count, "Calculate Bounding Box");
                {
                    Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                    Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                    for (int i = 0; i < count; i++)
                    {
                        InformationElement pack = information[i];
                        int j = 0;
                        if (Toggle.IsToggled<TYield>())
                        {
                            const int unroll = 4;
                            int total = (pack.Vertices.Length / unroll) * unroll;
                            for (; j < total;)
                            {
                                int k = j + 0;
                                Vector3 point = pack.Vertices[k];
                                point = (pack.Rotation * Vector3.Scale(point, pack.LocalScale)) + pack.WorldPosition; // Equivalent of transform.TransformPoint(Vector3).
                                pack.Vertices[k] = point;
                                pack.Min = Vector3.Min(pack.Min, point);
                                pack.Max = Vector3.Max(pack.Max, point);

                                k = j + 1;
                                point = pack.Vertices[k];
                                point = (pack.Rotation * Vector3.Scale(point, pack.LocalScale)) + pack.WorldPosition;
                                pack.Vertices[k] = point;
                                pack.Min = Vector3.Min(pack.Min, point);
                                pack.Max = Vector3.Max(pack.Max, point);

                                k = j + 2;
                                point = pack.Vertices[k];
                                point = (pack.Rotation * Vector3.Scale(point, pack.LocalScale)) + pack.WorldPosition;
                                pack.Vertices[k] = point;
                                pack.Min = Vector3.Min(pack.Min, point);
                                pack.Max = Vector3.Max(pack.Max, point);

                                k = j + 3;
                                point = pack.Vertices[k];
                                point = (pack.Rotation * Vector3.Scale(point, pack.LocalScale)) + pack.WorldPosition;
                                pack.Vertices[k] = point;
                                pack.Min = Vector3.Min(pack.Min, point);
                                pack.Max = Vector3.Max(pack.Max, point);

                                j += unroll;
                                await options.Yield();
                            }
                        }

                        CalculateBounds(ref pack, j);

                        information[i] = pack;
                        min = Vector3.Min(min, pack.Min);
                        max = Vector3.Max(max, pack.Max);
                        await options.StepTaskAndYield<TYield>();
                    }
                    options.SetVoxelizationParameters(voxelSize, min, max);
                }
                options.StepPopTask();

                options.PushTask(count, "Voxelizing Meshes");
                {
                    VoxelizationParameters parameters = options.VoxelizationParameters;
                    int voxelsCount = parameters.VoxelsCount;
                    voxels = ArrayPool<bool>.Shared.Rent(voxelsCount);
                    Array.Clear(voxels, 0, voxelsCount);
                    bool[] voxels_ = ArrayPool<bool>.Shared.Rent(voxelsCount);
                    Array.Clear(voxels_, 0, voxelsCount);
                    for (int i = 0; i < count; i++)
                    {
                        CalculateMultiplesVolumeIndexes(information[i], voxels_, parameters, out int xMinMultiple, out int yMinMultiple, out int zMinMultiple, out int xMaxMultiple, out int yMaxMultiple, out int zMaxMultiple);

                        VoxelInfo[] voxelsInfo = Unsafe.As<VoxelInfo[]>(voxels_);
                        int index = parameters.Depth * (parameters.Height * xMinMultiple);
                        for (int x = xMinMultiple; x < xMaxMultiple; x++)
                        {
                            index += parameters.Depth * yMinMultiple;
                            for (int y = yMinMultiple; y < yMaxMultiple; y++)
                            {
                                index += zMinMultiple;
                                for (int z = zMinMultiple; z < zMaxMultiple; z++, index++)
                                {
                                    Debug.Assert(index == parameters.GetIndex(x, y, z));
                                    voxels[index] |= voxelsInfo[index].Fill;
                                    voxelsInfo[index] = default;
                                }
                                index += parameters.Depth - zMaxMultiple;
                            }
                            index += parameters.Depth * (parameters.Height - yMaxMultiple);
                        }

                        await options.StepTaskAndYield<TYield>();
                    }
                    ArrayPool<bool>.Shared.Return(voxels_);
                    information.Dispose();
                }
                options.StepPopTask();
            }
            options.PopTask();
            return this;
        }

        private static void CalculateBounds(ref InformationElement pack, int j)
        {
            // Stores values in the stack to reduce memory reading access
            Quaternion rotation = pack.Rotation;
            Vector3 localScale = pack.LocalScale;
            Vector3 worldPosition = pack.WorldPosition;
            Vector3 min = pack.Min;
            Vector3 max = pack.Max;

            ref Vector3 current = ref pack.Vertices[j];
            ref Vector3 end = ref Unsafe.Add(ref pack.Vertices[pack.Vertices.Length - 1], 1);
            while (Unsafe.IsAddressLessThan(ref current, ref end))
            {
                Debug.Assert(current == pack.Vertices[j]);
                current = (rotation * Vector3.Scale(current, localScale)) + worldPosition; // Equivalent of transform.TransformPoint(Vector3).
                min = Vector3.Min(min, current);
                max = Vector3.Max(max, current);
                current = ref Unsafe.Add(ref current, 1);
#if UNITY_ASSERTIONS
                j++;
                #endif
            }
            pack.Min = min;
            pack.Max = max;
        }

        private static void CalculateMultiplesVolumeIndexes(
            InformationElement content,
            bool[] tmpVoxels,
            in VoxelizationParameters parameters,
            out int xMinMultiple, out int yMinMultiple, out int zMinMultiple,
            out int xMaxMultiple, out int yMaxMultiple, out int zMaxMultiple)
        {
            Debug.Assert(sizeof(bool) == Unsafe.SizeOf<VoxelInfo>());
            Voxelize(
                MemoryMarshal.Cast<Vector3, System.Numerics.Vector3>(content.Vertices),
                content.Triangles,
                MemoryMarshal.Cast<bool, VoxelInfo>(tmpVoxels),
                parameters
            );
            content.Dispose();

            // Fit bounds to global resolution.
            xMinMultiple = Mathf.FloorToInt(content.Min.x / parameters.VoxelSize);
            yMinMultiple = Mathf.FloorToInt(content.Min.y / parameters.VoxelSize);
            zMinMultiple = Mathf.FloorToInt(content.Min.z / parameters.VoxelSize);
            xMaxMultiple = Mathf.CeilToInt(content.Max.x / parameters.VoxelSize);
            yMaxMultiple = Mathf.CeilToInt(content.Max.y / parameters.VoxelSize);
            zMaxMultiple = Mathf.CeilToInt(content.Max.z / parameters.VoxelSize);

            // Fix offset
            xMinMultiple += parameters.Width / 2;
            yMinMultiple += parameters.Height / 2;
            zMinMultiple += parameters.Depth / 2;
            xMaxMultiple += parameters.Width / 2;
            yMaxMultiple += parameters.Height / 2;
            zMaxMultiple += parameters.Depth / 2;

            // Clamp values because a part of the mesh may be outside the voxelization area.
            xMinMultiple = Mathf.Max(xMinMultiple, 0);
            yMinMultiple = Mathf.Max(yMinMultiple, 0);
            zMinMultiple = Mathf.Max(zMinMultiple, 0);
            xMaxMultiple = Mathf.Min(xMaxMultiple, parameters.Width);
            yMaxMultiple = Mathf.Min(yMaxMultiple, parameters.Height);
            zMaxMultiple = Mathf.Min(zMaxMultiple, parameters.Depth);
        }

        private async ValueTask<Voxelizer> ProcessMultiThread()
        {
            NavigationGenerationOptions options = this.options;
            RawPooledList<InformationElement> information = this.information;
            int count = information.Count;
            options.PushTask(2, "Calculate Voxelization");
            {
                options.PushTask(2, "Calculate Bounding Box");
                {
                    options.PushTask(count, "Calculate Individual Bounding Boxes");
                    {
                        Parallel.For(0, count, i =>
                        {
                            CalculateBounds(ref information[i], 0);
                            options.StepTask();
                        });
                    }
                    options.StepPopTask();

                    // The overhead of multithreading here may not be worthly, so we check the amount of work before using it.
                    if (count > 5000) // TODO: Research a better threshold.
                    {
                        IndexPartitioner partitioner = new IndexPartitioner(0, count);
                        int doublePartsCount = partitioner.PartsCount * 2;

                        options.PushTask(count, "Merging Individual Bounding Boxes");
                        {
                            // TODO: Should we pool this allocation?
                            StrongBox<(Vector3 min, Vector3 max)> box = new StrongBox<(Vector3 min, Vector3 max)>((
                                new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
                                new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity)
                                ));

                            Parallel.For(0, partitioner.PartsCount, i =>
                            {
                                (int fromInclusive, int toExclusive) tuple = partitioner[i];
                                Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                                Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                                for (int j = tuple.fromInclusive; j < tuple.toExclusive; j++)
                                {
                                    InformationElement pack = information[j];
                                    min = Vector3.Min(min, pack.Min);
                                    max = Vector3.Max(max, pack.Max);
                                    options.StepTask();
                                }

                                lock (box)
                                {
                                    (Vector3 min, Vector3 max) value_ = box.Value;
                                    value_.min = Vector3.Max(value_.min, min);
                                    value_.max = Vector3.Max(value_.max, max);
                                    box.Value = value_;
                                }
                            });

                            (Vector3 min, Vector3 max) value = box.Value;
                            options.SetVoxelizationParameters(voxelSize, value.min, value.max);
                        }
                        options.StepPopTask();
                    }
                    else
                    {
                        options.PushTask(count, "Merging Individual Bounding Boxes");
                        {
                            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                            for (int i = 0; i < count; i++)
                            {
                                InformationElement pack = information[i];
                                min = Vector3.Min(min, pack.Min);
                                max = Vector3.Max(max, pack.Max);
                                options.StepTask();
                            }
                            options.SetVoxelizationParameters(voxelSize, min, max);
                        }
                        options.StepPopTask();
                    }
                }
                options.StepPopTask();

                VoxelizationParameters parameters = options.VoxelizationParameters;
                int voxelsCount = parameters.VoxelsCount;
                this.voxels = ArrayPool<bool>.Shared.Rent(voxelsCount + (sizeof(int) / sizeof(bool)));
                Array.Clear(this.voxels, 0, voxelsCount);
                bool[] voxels = this.voxels;
                options.PushTask(information.Count, "Voxelizing Meshes");
                {
                    Parallel.For(0, information.Count, i =>
                    {
                        VoxelizeMultithreadSlave(options.VoxelizationParameters, voxels, information, i);
                        options.StepTask();
                    });
                    this.information.Dispose();
                }
                options.StepPopTask();
            }
            options.PopTask();
            return this;
        }

        private static void VoxelizeMultithreadSlave(in VoxelizationParameters parameters, bool[] source, RawPooledList<InformationElement> information, int i)
        {
            InformationElement pack = information[i];

            if (unchecked((uint)pack.Vertices.Length > (uint)pack.Vertices.Array.Length))
            {
                Debug.Assert(false, "Index out of range.");
                return;
            }

            bool[] voxels = ArrayPool<bool>.Shared.Rent(parameters.VoxelsCount + (sizeof(int) / sizeof(bool)));
            Array.Clear(voxels, 0, parameters.VoxelsCount);
            Span<VoxelInfo> voxelsInfo = MemoryMarshal.Cast<bool, VoxelInfo>(voxels);

            Voxelize(
                MemoryMarshal.Cast<Vector3, System.Numerics.Vector3>(pack.Vertices),
                pack.Triangles,
                MemoryMarshal.Cast<bool, VoxelInfo>(voxels),
                parameters
            );

            CalculateMultiplesVolumeIndexes(information[i], voxels, parameters, out int xMinMultiple, out int yMinMultiple, out int zMinMultiple, out int xMaxMultiple, out int yMaxMultiple, out int zMaxMultiple);

            Span<bool> bytes = stackalloc bool[sizeof(int) / sizeof(bool)];
            ref int int_ = ref Unsafe.As<bool, int>(ref bytes[0]);
            int index = parameters.Depth * (parameters.Height * xMinMultiple);
            for (int x = xMinMultiple; x < xMaxMultiple; x++)
            {
                index += parameters.Depth * yMinMultiple;
                for (int y = yMinMultiple; y < yMaxMultiple; y++)
                {
                    index += zMinMultiple;

                    for (int z = zMinMultiple; z < zMaxMultiple;)
                    {
                        bytes.Clear();

                        Debug.Assert(index == parameters.GetIndex(x, y, z));
                        bytes[0] = voxelsInfo[index].Fill;
                        index++;
                        z++;

                        // TODO: On .NET 5 replace this with Interlocked.Or.
                        InterlockedOr(ref Unsafe.As<bool, int>(ref source[index]), int_);
                    }
                    index += parameters.Depth - zMaxMultiple;
                }
                index += parameters.Depth * (parameters.Height - yMaxMultiple);
            }

            ArrayPool<bool>.Shared.Return(voxels);
        }

        public void DrawGizmos()
        {
            Gizmos.color = Color.red;
            VoxelizationParameters parameters = options.VoxelizationParameters;
            float voxelSize = parameters.VoxelSize;
            Vector3 size = Vector3.one * voxelSize;
            int i = 0;
            for (int x = 0; x < parameters.Width; x++)
            {
                float x_ = parameters.Min.x + (voxelSize * x);
                for (int y = 0; y < parameters.Height; y++)
                {
                    float y_ = parameters.Min.y + (voxelSize * y);
                    for (int z = 0; z < parameters.Depth; z++)
                    {
                        if (voxels[i++])
                        {
                            float z_ = parameters.Min.z + (voxelSize * z);
                            Gizmos.DrawWireCube(new Vector3(x_, y_, z_), size);
                        }
                    }
                }
            }

            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(parameters.VolumeCenter, parameters.VolumeSize);
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            Debug.Assert(!(vertices is null), "Already disposed.");
            for (int i = 0; i < information.Count; i++)
                information[i].Dispose();
            information.Dispose();
            vertices.Clear();
            ObjectPool<List<Vector3>>.Shared.Return(vertices);
            vertices = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int InterlockedOr(ref int location1, int value)
        {
            int current = location1;
            while (true)
            {
                int newValue = current | value;
                int oldValue = Interlocked.CompareExchange(ref location1, newValue, current);
                if (oldValue == current)
                    return oldValue;
                current = oldValue;
            }
        }
    }
}
