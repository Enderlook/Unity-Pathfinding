using Enderlook.Threading;
using Enderlook.Unity.Pathfinding.Algorithms;
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

        internal NavigationGenerationOptions options;

        private int navigationLock;
        private CompactOpenHeightField compactOpenHeightField;
        private int[] spanToColumn;

        private Func<(Vector3, Vector3), bool> lineCast;

        internal bool HasNavigation => !(options is null) && options.Progress == 1;

        private static readonly Func<NavigationSurface, Task> buildNavigationFunc = async (e) => await e.BuildNavigation();

        private void Awake()
        {
            options = new NavigationGenerationOptions();
            lineCast = e => Physics.Linecast(e.Item1, e.Item2, includeLayers);

            if (Info.SupportMultithreading)
                Task.Factory.StartNew(buildNavigationFunc, this).Unwrap();
            else
                BuildNavigation();
        }

        private void Update() => options?.Poll();

        private void OnDrawGizmosSelected()
        {
            if (HasNavigation)
            {
                Lock();
                compactOpenHeightField.DrawGizmos(options.VoxelizationParameters, false, true);
                Unlock();
            }
        }

        internal void CalculatePathSync(Path<Vector3> path, Vector3 position, Vector3 destination)
        {
            if (options is null) ThrowNoNavigation();
            if (!options.IsCompleted) ThrowNavigationInProgress();

            SearcherToLocationWithHeuristic<NavigationSurface, Vector3, int> searcher = SearcherToLocationWithHeuristic<NavigationSurface, Vector3, int>.From(this, destination);

            ValueTask task = PathCalculator.CalculatePath<Vector3, int, NodesEnumerator, NavigationSurface, PathBuilder<int, Vector3>, Path<Vector3>, SearcherToLocationWithHeuristic<NavigationSurface, Vector3, int>, SimpleWatchdog, DummyAwaitable, DummyAwaitable.Awaiter>(this, path, position, searcher, new SimpleWatchdog(false));

            Debug.Assert(task.IsCompleted);
            task.GetAwaiter().GetResult();
        }

        internal ValueTask CalculatePathAsync(Path<Vector3> path, Vector3 position, Vector3 destination)
        {
            if (options is null) ThrowNoNavigation();
            if (!options.IsCompleted) ThrowNavigationInProgress();

            SearcherToLocationWithHeuristic<NavigationSurface, Vector3, int> searcher = SearcherToLocationWithHeuristic<NavigationSurface, Vector3, int>.From(this, destination);

            if (Info.SupportMultithreading)
                return PathCalculator.CalculatePath<Vector3, int, NodesEnumerator, NavigationSurface, PathBuilder<int, Vector3>, Path<Vector3>, SearcherToLocationWithHeuristic<NavigationSurface, Vector3, int>, SimpleWatchdog, DummyAwaitable, DummyAwaitable.Awaiter>(this, path, position, searcher, new SimpleWatchdog(true));
            else
            {
                int pathfindingExecutionTimeSlice = this.pathfindingExecutionTimeSlice;
                if (pathfindingExecutionTimeSlice > 0)
                    return PathCalculator.CalculatePath<Vector3, int, NodesEnumerator, NavigationSurface, PathBuilder<int, Vector3>, Path<Vector3>, SearcherToLocationWithHeuristic<NavigationSurface, Vector3, int>, TimeSliceWatchdog, TimeSlicer.Yielder, TimeSlicer.Yielder>(this, path, position, searcher, new TimeSliceWatchdog(pathfindingExecutionTimeSlice));
                else
                    return PathCalculator.CalculatePath<Vector3, int, NodesEnumerator, NavigationSurface, PathBuilder<int, Vector3>, Path<Vector3>, SearcherToLocationWithHeuristic<NavigationSurface, Vector3, int>, SimpleWatchdog, DummyAwaitable, DummyAwaitable.Awaiter>(this, path, position, searcher, new SimpleWatchdog(false));
            }
        }

        public ValueTask BuildNavigation()
        {
            if (!options.IsCompleted)
                ThrowNavigationInProgress();
            ValueTask task = BuildNavigation_();
            options.SetTask(task);
            return task;
        }

        private async ValueTask BuildNavigation_()
        {
            float voxelSize = this.voxelSize;
            LayerMask includeLayers = this.includeLayers;
            CollectionType collectObjects = this.collectObjects;
            GeometryType collectInformation = this.collectInformation;

            if (voxelSize <= 0)
                ThrowVoxelSizeMustBeGreaterThanZero();

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
                    await Switch.ToLongBackground;

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

        int IGraphLocation<int, Vector3>.FindClosestNodeTo(Vector3 position)
        {
            VoxelizationParameters parameters = options.VoxelizationParameters;
            Vector3 indexes_ = (position - parameters.Min) / parameters.VoxelSize;
            if (indexes_.x < 0 || indexes_.y < 0 || indexes_.z < 0)
                goto fail;

            Vector3Int indexes = new Vector3Int(Mathf.FloorToInt(indexes_.x), Mathf.FloorToInt(indexes_.y), Mathf.FloorToInt(indexes_.z));
            if (indexes.x >= parameters.Width || indexes.y >= parameters.Height || indexes.z >= parameters.Depth)
                goto fail;

            ref readonly CompactOpenHeightField.HeightColumn column = ref compactOpenHeightField.Column(parameters.GetIndex(indexes.x, indexes.z));
            // TODO: This loop could be replaced with a binary search to reduce time complexity.
            for (int i = column.First; i < column.Last; i++)
            {
                ref readonly CompactOpenHeightField.HeightSpan span = ref compactOpenHeightField.Span(i);
                if (span.Floor >= indexes.y)
                    return i;
            }

            fail:
            return -1;
        }

        Vector3 IGraphLocation<int, Vector3>.ToPosition(int node)
        {
            Debug.Assert(node >= 0 && node < compactOpenHeightField.SpansCount);
            VoxelizationParameters parameters = options.VoxelizationParameters;
            int columnIndex = spanToColumn[node];
            ref readonly CompactOpenHeightField.HeightColumn column = ref compactOpenHeightField.Column(columnIndex);
            Debug.Assert(node >= column.First && node < column.Last);
            Vector2 indexes = parameters.From2D(columnIndex);
            int y = compactOpenHeightField.Span(node).Floor;
            Vector3 position = parameters.Min + (new Vector3(indexes.x, y, indexes.y) * parameters.VoxelSize);
            Debug.Assert(node == ((IGraphLocation<int, Vector3>)this).FindClosestNodeTo(position));
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

        bool IGraphLineOfSight<Vector3>.HasLineOfSight(Vector3 from, Vector3 to)
        {
            if (UnityThread.IsMainThread)
                return Physics.Linecast(from, to, includeLayers);
            else
                return UnityThread.RunNow(lineCast, (from, to));
        }

        internal struct NodesEnumerator : IEnumerator<int>
        {
            private int left;
            private int forward;
            private int right;
            private int backward;
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