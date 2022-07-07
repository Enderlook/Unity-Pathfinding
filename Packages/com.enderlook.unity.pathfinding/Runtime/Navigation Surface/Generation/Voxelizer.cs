using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Pools;
using Enderlook.Unity.Pathfinding.Utils;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        private RawPooledList<MeshInformation> meshInformations;
        private RawPooledList<BoxInformation> boxInformations;
        private float voxelSize;
        private LayerMask includeLayers;
        private ArraySlice<bool> voxels;

        /// <summary>
        /// Voxelization result.
        /// </summary>
        public ReadOnlyArraySlice<bool> Voxels
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => voxels;
        }

        /// <summary>
        /// Creates a new voxelizer of meshes.
        /// </summary>
        /// <param name="options">Stores configuration information.</param>
        public Voxelizer(NavigationGenerationOptions options, float voxelSize, LayerMask includeLayers)
        {
            this.options = options;
            this.voxelSize = voxelSize;
            this.includeLayers = includeLayers;
            meshInformations = RawPooledList<MeshInformation>.Create();
            boxInformations = RawPooledList<BoxInformation>.Create();
            vertices = ObjectPool<List<Vector3>>.Shared.Rent();
            voxels = default;
        }

        /// <summary>
        /// Voxelizes all enqueued meshes.
        /// </summary>
        public async ValueTask<Voxelizer> Process()
        {
            vertices.Clear();
            ObjectPool<List<Vector3>>.Shared.Return(vertices);
            vertices = default;

            int meshesCount = meshInformations.Count;
            int boxesCount = boxInformations.Count;
            int informationsTypesCount = (meshesCount > 0 ? 1 : 0) + (boxesCount > 0 ? 1 : 0);

            NavigationGenerationOptions options = this.options;
            if (informationsTypesCount == 0)
            {
                options.SetVoxelizationParameters(0, default, default);
                voxels = new ArraySlice<bool>(0, false);
                return this;
            }

            TimeSlicer timeSlicer = options.TimeSlicer;
            options.PushTask(2, "Calculate Voxelization");
            {
                options.PushTask(informationsTypesCount, "Generating Bounding Box");
                {
                    Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                    Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

                    if (meshesCount > 0)
                    {
                        if (timeSlicer.PreferMultithreading)
                            CalculateMeshesBounds_MultiThread.Calculate(options, meshInformations, ref min, ref max);
                        else
                        {
                            options.PushTask(meshesCount, "Calculate Bounding Box of Box Colliders");
                            {
                                int i = 0;
                                int j = 0;
                                if (timeSlicer.ShouldUseTimeSlice)
                                {
                                    while (CalculateMeshesBounds_SingleThread<Toggle.Yes>(ref i, ref j, ref min, ref max))
                                        await timeSlicer.Yield();
                                }
                                else
                                {
                                    bool value = CalculateMeshesBounds_SingleThread<Toggle.No>(ref i, ref j, ref min, ref max);
                                    Debug.Assert(!value);
                                }
                            }
                            options.StepPopTask();
                        }
                    }

                    if (boxesCount > 0)
                    {
                        if (timeSlicer.PreferMultithreading)
                            CalculateBoxesBounds_MultiThread.Calculate(options, boxInformations, ref min, ref max);
                        else
                        {
                            options.PushTask(boxesCount, "Calculate Bounding Box of Mesh Renderers");
                            {
                                int i = 0;
                                if (timeSlicer.ShouldUseTimeSlice)
                                {
                                    while (CalculateBoxesBounds_SingleThread<Toggle.Yes>(ref i, ref min, ref max))
                                        await timeSlicer.Yield();
                                }
                                else
                                {
                                    bool value = CalculateBoxesBounds_SingleThread<Toggle.No>(ref i, ref min, ref max);
                                    Debug.Assert(!value);
                                }
                            }
                            options.StepPopTask();
                        }
                    }

                    options.SetVoxelizationParameters(voxelSize, min, max);
                }
                options.StepPopTask();

                options.PushTask(informationsTypesCount, "Voxelizing");
                {
                    int voxelsCount = options.VoxelizationParameters.VoxelsCount;
                    // (sizeof(int) / sizeof(bool)) used to avoid index out of range when accessing the last element of the array while is reinterpreted as int[] for interlocked usage.
                    voxels = new ArraySlice<bool>(voxelsCount + (sizeof(int) / sizeof(bool)), true);

                    if (meshesCount > 0)
                    {
                        options.PushTask(meshesCount, "Voxelizing Mesh Renderers");
                        {
                            if (timeSlicer.PreferMultithreading)
                                VoxelizeMeshes_MultiThread.Calculate(options, meshInformations, voxels);
                            else
                            {
                                int count = meshInformations.Count;
                                VoxelizationParameters parameters = options.VoxelizationParameters;
                                ArraySlice<VoxelInfo> voxelsInfo = new ArraySlice<VoxelInfo>(voxelsCount, false);
                                if (timeSlicer.ShouldUseTimeSlice)
                                {
                                    for (int i = 0; i < count; i++)
                                    {
                                        voxelsInfo.Clear();
                                        MeshInformation content = meshInformations[i];
                                        await VoxelizeMesh<Toggle.Yes, Toggle.No>(
                                            timeSlicer,
                                            parameters,
                                            voxels,
                                            content,
                                            voxelsInfo);
                                        content.Dispose();
                                        options.StepTask();
                                    }
                                }
                                else
                                {
                                    ArraySlice<bool> voxels = this.voxels;
                                    Local(meshInformations);

                                    void Local(ArraySlice<MeshInformation> list)
                                    {
                                        voxelsInfo.Clear();
                                        for (int i = 0; i < count; i++)
                                        {
                                            MeshInformation content = list[i];
                                            ValueTask task = VoxelizeMesh<Toggle.No, Toggle.No>(
                                                timeSlicer,
                                                parameters,
                                                voxels,
                                                content,
                                                voxelsInfo);
                                            Debug.Assert(task.IsCompleted);
                                            content.Dispose();
                                            options.StepTask();
                                        }
                                    }
                                }
                                voxelsInfo.Dispose();
                            }
                            meshInformations.Dispose();
                        }
                        options.StepPopTask();
                    }

                    if (boxesCount > 0)
                    {
                        options.PushTask(boxesCount, "Voxelizing Box Colliders");
                        {
                            if (timeSlicer.PreferMultithreading)
                                VoxelizeBoxes_MultiThread.Calculate(options, voxels, boxInformations);
                            else if (timeSlicer.ShouldUseTimeSlice)
                            {
                                for (int i = 0; i < boxesCount; i++)
                                {
                                    if (VoxelizeBoxClosure.ShouldVoxelize(options, boxInformations[i], out VoxelizeBoxClosure closure))
                                    {
                                        while (closure.VoxelizeBox<Toggle.No, Toggle.Yes>(options, voxels))
                                            await timeSlicer.Yield();
                                    }
                                    options.StepTask();
                                }
                            }
                            else
                            {
                                ArraySlice<bool> voxels = this.voxels;
                                Local(options, boxInformations);

                                void Local(NavigationGenerationOptions options_, ArraySlice<BoxInformation> list)
                                {
                                    for (int i = 0; i < list.Length; i++)
                                    {
                                        if (VoxelizeBoxClosure.ShouldVoxelize(options_, list[i], out VoxelizeBoxClosure closure))
                                        {
                                            bool value = closure.VoxelizeBox<Toggle.No, Toggle.Yes>(options_, voxels);
                                            Debug.Assert(!value);
                                        }
                                        options_.StepTask();
                                    }
                                }
                            }
                            boxInformations.Dispose();
                        }
                        options.StepPopTask();
                    }
                }
                options.StepPopTask();
            }
            options.PopTask();

            return this;
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
        public void Dispose() => voxels.Dispose();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 TranslatePoint(Vector3 point, Quaternion rotation, Vector3 lossyScale, Vector3 worldPosition)
            => (rotation * Vector3.Scale(point, lossyScale)) + worldPosition;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 TranslatePoint(Vector3 point, Vector3 offset, Quaternion rotation, Vector3 lossyScale, Vector3 worldPosition)
            => (rotation * (point + Vector3.Scale(offset, lossyScale))) + worldPosition;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int InterlockedOr(ref int location1, int value)
        {
            // TODO: On .NET 5 replace this with Interlocked.Or.
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CalculateMultiplesVolumeIndexes(
            Vector3 min, Vector3 max,
            in VoxelizationParameters parameters,
            out int xMinMultiple, out int yMinMultiple, out int zMinMultiple,
            out int xMaxMultiple, out int yMaxMultiple, out int zMaxMultiple)
        {
            // Fit bounds to global resolution.
            xMinMultiple = Mathf.FloorToInt(min.x / parameters.VoxelSize);
            yMinMultiple = Mathf.FloorToInt(min.y / parameters.VoxelSize);
            zMinMultiple = Mathf.FloorToInt(min.z / parameters.VoxelSize);
            xMaxMultiple = Mathf.CeilToInt(max.x / parameters.VoxelSize);
            yMaxMultiple = Mathf.CeilToInt(max.y / parameters.VoxelSize);
            zMaxMultiple = Mathf.CeilToInt(max.z / parameters.VoxelSize);

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

        private interface IMinMax
        {
            Vector3 Min { get; }
            Vector3 Max { get; }
        }
    }
}
