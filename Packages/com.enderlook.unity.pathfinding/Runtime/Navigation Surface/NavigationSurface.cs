using Enderlook.Collections.LowLevel;
using Enderlook.Threading;
using Enderlook.Unity.Pathfinding.Generation;
using Enderlook.Unity.Pathfinding.Utils;
using Enderlook.Unity.Threading;

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [AddComponentMenu("Enderlook/Pathfinding/Navigation Surface"), DefaultExecutionOrder(ExecutionOrder.NavigationSurface)]
    public sealed class NavigationSurface : MonoBehaviour, IGraphLocation<int, Vector3>, IGraphHeuristic<int>, IGraphIntrinsic<int, NavigationSurface.NodesEnumerator>, IGraphLineOfSight<Vector3>
    {
        [Header("Baking Basic")]
        [SerializeField, Tooltip("Determine objects that are collected to generate navigation data.")]
        private CollectionType collectObjects;

        [SerializeField, Tooltip("Layers used to generate navigation data.")]
        private LayerMask includeLayers = int.MaxValue;

        [SerializeField, Tooltip("Determines information from collected objects used to build the navigation data.")]
        private GeometryType collectInformation;

        [Header("Baking Advanced")]
        [SerializeField, Min(0), Tooltip("Aproximate size of voxels. Lower values increases accuracy at cost of perfomance.")]
        private float voxelSize = 1f;

        [SerializeField, Min(0), Tooltip("For executions that happens in the main thread, determines the amount of milliseconds executed per frame.\nUse 0 to disable time slicing.")]
        private int backingExecutionTimeSlice = 1000 / 60;

        [Header("Pathfinding Advanced")]
        [SerializeField, Min(0), Tooltip("For executions that happens in the main thread, determines the amount of milliseconds executed per frame.\nUse 0 to disable time slicing.")]
        private int pathfindingExecutionTimeSlice = 1000 / 60;

        private NavigationGenerationOptions options;

        private int navigationLock;
        private CompactOpenHeightField compactOpenHeightField;
        private int[] spanToColumn;

        private RawList<TimeSlicer> timeSlicers = RawList<TimeSlicer>.Create();

        internal bool HasNavigation => !(options is null) && options.Progress == 1;

        private static readonly Func<NavigationSurface, Task> buildNavigationFunc = async (e) => await e.BuildNavigation();

        private void Awake()
        {
            options = new NavigationGenerationOptions();

            if (options.UseMultithreading)
                Task.Factory.StartNew(buildNavigationFunc, this).Unwrap();
            else
                BuildNavigation();
        }

        private void Update()
        {
            if (!options.IsCompleted)
                options.Poll();
            else
            {
                int j = 0;
                for (int i = 0; i < timeSlicers.Count; i++)
                {
                    TimeSlicer timeSlicer = timeSlicers[i];
                    if (timeSlicer.IsCompleted)
                        continue;

                    timeSlicer.Poll();
                    if (timeSlicer.IsCompleted)
                        continue;

                    timeSlicers[j++] = timeSlicer;
                }
                timeSlicers = RawList<TimeSlicer>.From(timeSlicers.UnderlyingArray, j);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (HasNavigation)
            {
                Lock();
                compactOpenHeightField.DrawGizmos(options.VoxelizationParameters, false, true, false);
                Unlock();
            }
        }

#if UNITY_EDITOR
        internal void Poll() => options.Poll();

        internal float Progress() => options.Progress;
#endif

        internal ValueTask CalculatePath(Path<Vector3> path, Vector3 position, Vector3 destination, bool synchronous = false)
        {
            if (options is null) ThrowNoNavigation();
            if (!options.IsCompleted) ThrowNavigationInProgress();

            if (!SearcherToLocationWithHeuristic<NavigationSurface, Vector3, int>.TryFrom(this, destination, out SearcherToLocationWithHeuristic<NavigationSurface, Vector3, int> searcher))
            {
                path.ManualSetNotFound();
                goto default_;
            }

            TimeSlicer timeSlicer = path.Start();
            timeSlicer.ExecutionTimeSlice = synchronous ? 0 : pathfindingExecutionTimeSlice;

            ValueTask task = PathCalculator.CalculatePath<Vector3, int, NodesEnumerator, NavigationSurface, PathBuilder<int, Vector3>, Path<Vector3>, SearcherToLocationWithHeuristic<NavigationSurface, Vector3, int>, TimeSlicer, TimeSlicer.Yielder, TimeSlicer.Yielder>(this, path, position, searcher, timeSlicer);

            timeSlicer.SetTask(task);

            if (synchronous)
            {
                timeSlicer.RunSynchronously();
                goto default_;
            }

            timeSlicers.Add(timeSlicer);
            return timeSlicer.AsTask();

            default_:
            return default;
        }

        internal ValueTask BuildNavigation(
#if UNITY_EDITOR
            bool isEditor = false
#endif
            )
        {
#if UNITY_EDITOR
            if (isEditor && options is null)
                options = new NavigationGenerationOptions();
#endif

            if (!options.IsCompleted) ThrowNavigationInProgress();

#if UNITY_EDITOR
            bool useMultithreading = options.UseMultithreading;
            if (isEditor)
                options.UseMultithreading = true;
#endif

            ValueTask task = BuildNavigation_(isEditor);
            options.SetTask(task);

#if UNITY_EDITOR
            if (isEditor)
                options.AsTask().GetAwaiter().OnCompleted(() => options.UseMultithreading = useMultithreading);
#endif

            return task;
        }

        private async ValueTask BuildNavigation_(
#if UNITY_EDITOR
            bool isEditor
#endif
            )
        {
            float voxelSize = this.voxelSize;
            LayerMask includeLayers = this.includeLayers;
            CollectionType collectObjects = this.collectObjects;
            GeometryType collectInformation = this.collectInformation;

            if (voxelSize <= 0) ThrowVoxelSizeMustBeGreaterThanZero();

            if (collectInformation.HasFlag(GeometryType.PhysicsColliders))
                throw new NotImplementedException($"Not implemented voxelization with {GeometryType.PhysicsColliders}.");
            if (collectInformation.HasFlag(GeometryType.PhysicsTriggerColliders))
                throw new NotImplementedException($"Not implemented voxelization with {GeometryType.PhysicsColliders}.");

            options.ExecutionTimeSlice = backingExecutionTimeSlice;

            if (options.Progress != 1 || !options.IsCompleted)
                ThrowNavigationInProgress();

            options.PushTask(4, "Generate Navigation Mesh");
            {
                Voxelizer voxelizer = new Voxelizer(options, voxelSize, includeLayers);

                bool wasNotInMainThread = !UnityThread.IsMainThread;
                if (wasNotInMainThread)
                    await Switch.ToUnity;

                // TODO: Add support for GeometryType of physics colliders.
                switch (collectObjects)
                {
                    case CollectionType.Volume:
                    {
                        if (collectInformation.HasFlag(GeometryType.RenderMeshes))
                            // This is cheaper than FindObjectOfType<MeshFilter>() since it doesn't allocate twice nor check types of each element twice.
                            voxelizer = await voxelizer.Enqueue(Unsafe.As<MeshFilter[]>(FindObjectsOfType(typeof(MeshFilter))));

                        bool nonTrigger = collectInformation.HasFlag(GeometryType.PhysicsColliders);
                        bool trigger = collectInformation.HasFlag(GeometryType.PhysicsTriggerColliders);
                        if (nonTrigger || trigger)
                            voxelizer = await voxelizer.Enqueue(Unsafe.As<Collider[]>(FindObjectsOfType(typeof(Collider))), nonTrigger && trigger ? 2 : (trigger ? 1 : 0));

                        break;
                    }
                    case CollectionType.Children:
                    {
                        if (collectInformation.HasFlag(GeometryType.RenderMeshes))
                            voxelizer = await voxelizer.Enqueue(GetComponentsInChildren<MeshFilter>());

                        bool nonTrigger = collectInformation.HasFlag(GeometryType.PhysicsColliders);
                        bool trigger = collectInformation.HasFlag(GeometryType.PhysicsTriggerColliders);
                        if (nonTrigger || trigger)
                            voxelizer = await voxelizer.Enqueue(GetComponentsInChildren<Collider>(), nonTrigger && trigger ? 2 : (trigger ? 1 : 0));

                        break;
                    }
                }

                if (wasNotInMainThread)
                {
#if UNITY_EDITOR
                    if (isEditor)
                        await Switch.ToLongBackgroundEditor;
                    else
#endif
                        await Switch.ToLongBackground;
                }

                voxelizer = await voxelizer.Process();
                options.StepTask();

                HeightField heightField = await HeightField.Create(voxelizer.Voxels, options);
                options.StepTask();

                CompactOpenHeightField compactOpenHeightField = await CompactOpenHeightField.Create(heightField, options);
                options.StepTask();

                heightField.Dispose();

                int[] spanToColumn;
                options.PushTask(compactOpenHeightField.ColumnsCount, "Building Lookup Table");
                {
                    spanToColumn = ArrayPool<int>.Shared.Rent(compactOpenHeightField.SpansCount);
                    int c = 0;
                    VoxelizationParameters parameters = options.VoxelizationParameters;
                    for (int x = 0; x < parameters.Width; x++)
                    {
                        for (int z = 0; z < parameters.Depth; z++)
                        {
                            Debug.Assert(c == parameters.GetIndex(x, z));
                            CompactOpenHeightField.HeightColumn column = compactOpenHeightField.Columns[c];
                            column.Span<int>(spanToColumn).Fill(c++);
                            if (options.ShouldUseTimeSlice)
                                await options.StepTaskAndYield();
                            else
                                options.StepTask();
                        }
                    }
                }
                options.PopTask();
                options.StepTask();

                Lock();
                {
                    this.compactOpenHeightField = compactOpenHeightField;
                    if (this.spanToColumn != null)
                        ArrayPool<int>.Shared.Return(this.spanToColumn);
                    this.spanToColumn = spanToColumn;
                }
                Unlock();
            }
            options.PopTask();

            void ThrowVoxelSizeMustBeGreaterThanZero() => throw new ArgumentException(nameof(voxelSize), "Must be greater than 0.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Lock()
        {
            while (Interlocked.Exchange(ref navigationLock, 1) != 0) ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Unlock() => navigationLock = 0;

        bool IGraphLocation<int, Vector3>.TryFindNodeTo(Vector3 position, out int node)
        {
            VoxelizationParameters parameters = options.VoxelizationParameters;
            Vector3 indexes_ = (position - parameters.Min) / parameters.VoxelSize;
            if (indexes_.x < 0 || indexes_.y < 0 || indexes_.z < 0)
                goto fail;

            Vector3Int indexes = new Vector3Int(Mathf.FloorToInt(indexes_.x), Mathf.FloorToInt(indexes_.y), Mathf.FloorToInt(indexes_.z));
            if (indexes.x >= parameters.Width || indexes.y >= parameters.Height || indexes.z >= parameters.Depth)
                goto fail;

            CompactOpenHeightField.HeightColumn column = compactOpenHeightField.Column(parameters.GetIndex(indexes.x, indexes.z));

            // TODO: This is very error-prone.

            for (int i = column.First; i < column.Last; i++)
            {
                ref readonly CompactOpenHeightField.HeightSpan span = ref compactOpenHeightField.Span(i);
                if (span.Floor >= indexes.y)
                {
                    node = i;
                    return true;
                }
            }

            fail:
            node = default;
            return false;
        }

        Vector3 IGraphLocation<int, Vector3>.ToPosition(int node)
        {
            Debug.Assert(node >= 0 && node < compactOpenHeightField.SpansCount);
            int columnIndex = spanToColumn[node];
            ref readonly CompactOpenHeightField.HeightColumn column = ref compactOpenHeightField.Column(columnIndex);
            Debug.Assert(node >= column.First && node < column.Last);
            VoxelizationParameters parameters = options.VoxelizationParameters;
            Vector2Int indexes = parameters.From2D(columnIndex);
            int y = compactOpenHeightField.Span(node).Floor;
            Vector3 position = parameters.Min + (new Vector3(indexes.x, y, indexes.y) * parameters.VoxelSize);
            //Debug.Assert(node == ((IGraphLocation<int, Vector3>)this).FindClosestNodeTo(position));
            return position;
        }

        float IGraphHeuristic<int>.GetHeuristicCost(int from, int to)
            => ((IGraphIntrinsic<int, NodesEnumerator>)this).GetCost(from, to);

        NodesEnumerator IGraphIntrinsic<int, NodesEnumerator>.GetNeighbours(int node)
        {
            Debug.Assert(node >= 0 && node < compactOpenHeightField.SpansCount);
            return new NodesEnumerator(compactOpenHeightField.Span(node));
        }

        float IGraphIntrinsic<int, NodesEnumerator>.GetCost(int from, int to)
            => Vector3.Distance(
            ((IGraphLocation<int, Vector3>)this).ToPosition(from),
            ((IGraphLocation<int, Vector3>)this).ToPosition(to)
        );

        bool IGraphLineOfSight<Vector3>.RequiresUnityThread => true;

        bool IGraphLineOfSight<Vector3>.HasLineOfSight(Vector3 from, Vector3 to) => !Physics.Linecast(from, to, includeLayers);

        internal struct NodesEnumerator : IEnumerator<int>
        {
            private readonly int left;
            private readonly int forward;
            private readonly int right;
            private readonly int backward;
            private int index;
            public int Current { get; private set; }

            object IEnumerator.Current => Current;

            public NodesEnumerator(in CompactOpenHeightField.HeightSpan heightSpan)
            {
                left = heightSpan.Left;
                forward = heightSpan.Forward;
                right = heightSpan.Right;
                backward = heightSpan.Backward;
                index = 0;
                Current = default;
            }

            public bool MoveNext()
            {
                int value;
                int index_;
                switch (index)
                {
                    case 0:
                        value = left;
                        index_ = 1;
                        if (value != -1)
                            goto success;
                        goto case 1;
                    case 1:
                        value = forward;
                        index_ = 2;
                        if (value != -1)
                            goto success;
                        goto case 2;
                    case 2:
                        value = right;
                        index_ = 3;
                        if (value != -1)
                            goto success;
                        goto case 3;
                    case 3:
                        value = backward;
                        index_ = 4;
                        if (value != -1)
                            goto success;
                        goto case 4;
                    case 4:
                    default:
                        return false;
                }
                success:
                index = index_;
                Current = value;
                return true;
            }

            public void Reset() => index = 0;

            public void Dispose() { }
        }

        private static void ThrowNavigationInProgress()
            => throw new InvalidOperationException("Navigation generation is in progress.");

        private static void ThrowNoNavigation()
            => throw new InvalidOperationException("No navigation built.");
    }
}