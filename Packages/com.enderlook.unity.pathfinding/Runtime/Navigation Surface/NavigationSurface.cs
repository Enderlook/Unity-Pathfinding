using Enderlook.Collections.Spatial;
using Enderlook.Pools;
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

using NVector3 = System.Numerics.Vector3;

namespace Enderlook.Unity.Pathfinding
{
    [AddComponentMenu("Enderlook/Pathfinding/Navigation Surface"), DefaultExecutionOrder(ExecutionOrder.NavigationSurface)]
    public sealed class NavigationSurface : MonoBehaviour
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

        [SerializeField, Min(0), Tooltip("Maximum amount of cells between two floors to be considered neighbours.")]
        private int maximumTraversableStep = 1;

        [SerializeField, Min(0), Tooltip("Minimum height between a floor and a ceil to be considered traversable.")]
        private int minimumTraversableHeight = 1;

        [SerializeField, Min(0), Tooltip("For executions that happens in the main thread, determines the amount of milliseconds executed per frame.\nUse 0 to disable time slicing.")]
        private int backingExecutionTimeSlice = 1000 / 60;

        [Header("Pathfinding Advanced")]
        [SerializeField, Min(0), Tooltip("For executions that happens in the main thread, determines the amount of milliseconds executed per frame.\nUse 0 to disable time slicing.")]
        private int pathfindingExecutionTimeSlice = 1000 / 60;

        private NavigationGenerationOptions options;
        private NavigationGenerationOptions inProgress;

        private int navigationLock;
        private CompactOpenHeightField compactOpenHeightField;
        private KDTreeVector3<int> tree;
        private int[] spanToColumn;
#if UNITY_EDITOR
        private bool hasNavigation;
        internal float Progress() => inProgress?.Progress ?? 0;
#endif

#if UNITY_EDITOR
        private static readonly Func<(NavigationSurface Instance, NavigationGenerationOptions Options, bool IsEditor), Task> buildNavigationFunc = async e =>
        {
            TimeSlicer timeSlicer = e.Options.TimeSlicer;
            ValueTask task = e.Instance.BuildNavigationInner(e.Options, e.IsEditor);
            if (timeSlicer.ExecutionTimeSlice == 0)
                timeSlicer.RunSynchronously();
            await task;
        };
#else
        private static readonly Func<(NavigationSurface Instance, NavigationGenerationOptions Options), Task> buildNavigationFunc = async e =>
        {
            TimeSlicer timeSlicer = e.Options.TimeSlicer;
            ValueTask task = e.Instance.BuildNavigationInner(e.Options);
            if (timeSlicer.ExecutionTimeSlice == 0)
                timeSlicer.RunSynchronously();
            await task;
        };
#endif

        private static readonly Func<(Wrapper graph, Vector3 from, Path<Vector3> path, SearcherToLocationWithHeuristic<Wrapper, Vector3, int> searcher, TimeSlicer timeSlicer), Task> calculatePath =
            async e =>
            {
                PathBuilder<int, Vector3> builder = PathBuilder<int, Vector3>.Rent();
                await PathCalculator.CalculatePath<Vector3, int, NodesEnumerator, Wrapper, PathBuilder<int, Vector3>, Path<Vector3>.Feedable, SearcherToLocationWithHeuristic<Wrapper, Vector3, int>, TimeSlicer, TimeSlicer.YieldAwait, TimeSlicer.YieldAwait, TimeSlicer.ToUnityAwait, TimeSlicer.ToUnityAwait>(e.graph, builder, e.from, new Path<Vector3>.Feedable(e.path), e.searcher, e.timeSlicer);
                builder.Return();
            };

        private void Awake()
        {
            if (!(options is null))
                return;

            BuildNavigation();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (hasNavigation && !(options is null))
            {
                Lock();
                try
                {
                    compactOpenHeightField.DrawGizmos(options.VoxelizationParameters, false, true, false);
                }
                finally
                {
                    Unlock();
                }
            }
        }
#endif

        internal ValueTask CalculatePath(Path<Vector3> path, Vector3 position, Vector3 destination, bool synchronous = false)
        {
            Lock();
            try
            {
                Awake();
            }
            finally
            {
                Unlock();
            }

            TimeSlicer timeSlicer = path.Start();
            timeSlicer.SetParent((options ?? inProgress).TimeSlicer);
            timeSlicer.ExecutionTimeSlice = synchronous ? 0 : pathfindingExecutionTimeSlice;
            timeSlicer.SetTask(Work());

            if (synchronous)
            {
                timeSlicer.RunSynchronously();
                return default;
            }

            return timeSlicer.AsTask();

            async ValueTask Work()
            {
                await timeSlicer.WaitForParentCompletion();

                if (!SearcherToLocationWithHeuristic<Wrapper, Vector3, int>.TryFrom(new Wrapper(this), destination, out SearcherToLocationWithHeuristic<Wrapper, Vector3, int> searcher))
                {
                    path.ManualSetNotFound();
                    return;
                }

                if (synchronous || !timeSlicer.PreferMultithreading)
                {
                    PathBuilder<int, Vector3> builder = PathBuilder<int, Vector3>.Rent();
                    await PathCalculator.CalculatePath<Vector3, int, NodesEnumerator, Wrapper, PathBuilder<int, Vector3>, Path<Vector3>.Feedable, SearcherToLocationWithHeuristic<Wrapper, Vector3, int>, TimeSlicer, TimeSlicer.YieldAwait, TimeSlicer.YieldAwait, TimeSlicer.ToUnityAwait, TimeSlicer.ToUnityAwait>(new Wrapper(this), builder, position, new Path<Vector3>.Feedable(path), searcher, timeSlicer);
                    builder.Return();
                }
                else
                    await await Task.Factory.StartNew(calculatePath, (new Wrapper(this), position, path, searcher, timeSlicer));
            }
        }

        internal ValueTask BuildNavigation(
#if UNITY_EDITOR
            bool isEditor = false
#endif
            )
        {
            NavigationGenerationOptions options = NavigationGenerationOptions.Rent();

            TimeSlicer timeSlicer = options.TimeSlicer;

            if (!timeSlicer.IsCompleted) ThrowNavigationInProgress();

            timeSlicer.Reset();

#if UNITY_EDITOR
            bool useMultithreading = timeSlicer.PreferMultithreading;
            if (isEditor)
                timeSlicer.PreferMultithreading = true;
#endif

            if (timeSlicer.PreferMultithreading)
            {
                Task<Task> task = Task.Factory.StartNew(buildNavigationFunc, (this, options
#if UNITY_EDITOR
                    , isEditor
#endif
                ));

                timeSlicer.SetTask(new ValueTask(task.Unwrap()));
            }
            else
            {
                ValueTask task = BuildNavigationInner(options
#if UNITY_EDITOR
                    , isEditor
#endif
                );

                timeSlicer.SetTask(task);

                if (timeSlicer.ExecutionTimeSlice == 0)
                    timeSlicer.RunSynchronously();
            }

#if UNITY_EDITOR
            if (isEditor)
                timeSlicer.AsTask().GetAwaiter().OnCompleted(() => timeSlicer.PreferMultithreading = useMultithreading);
#endif

            return timeSlicer.AsTask();
        }

        private async ValueTask BuildNavigationInner(NavigationGenerationOptions options
#if UNITY_EDITOR
            , bool isEditor
#endif
            )
        {
            try
            {
                inProgress = options;
                Debug.Assert(!(options is null));
                TimeSlicer timeSlicer = options.TimeSlicer;

                float voxelSize = this.voxelSize;
                LayerMask includeLayers = this.includeLayers;
                CollectionType collectObjects = this.collectObjects;
                GeometryType collectInformation = this.collectInformation;
                int maximumTraversableStep = this.maximumTraversableStep;
                int minimumTraversableHeight = this.minimumTraversableHeight;

                if (voxelSize <= 0) ThrowVoxelSizeMustBeGreaterThanZero();

                if ((collectInformation & GeometryType.PhysicsColliders) != 0)
                    throw new NotImplementedException($"Not implemented voxelization with {GeometryType.PhysicsColliders}.");
                if ((collectInformation & GeometryType.PhysicsTriggerColliders) != 0)
                    throw new NotImplementedException($"Not implemented voxelization with {GeometryType.PhysicsTriggerColliders}.");

                timeSlicer.ExecutionTimeSlice = backingExecutionTimeSlice;
                options.MaximumTraversableStep = maximumTraversableStep;
                options.MinimumTraversableHeight = minimumTraversableHeight;

                options.PushTask(5, "Generate Navigation Mesh");
                {
                    Voxelizer voxelizer = new Voxelizer(options, voxelSize, includeLayers);

                    bool wasNotInMainThread = !UnityThread.IsMainThread;
                    if (wasNotInMainThread)
                        await timeSlicer.ToUnity();

                    // TODO: Add support for GeometryType of physics colliders.
                    switch (collectObjects)
                    {
                        case CollectionType.Volume:
                        {
                            bool renderMeshes = (collectInformation & GeometryType.RenderMeshes) != 0;
                            if (renderMeshes)
                                // This is cheaper than FindObjectOfType<MeshFilter>() since it doesn't allocate twice nor check types of each element twice.
                                voxelizer = await voxelizer.Enqueue(Unsafe.As<MeshFilter[]>(FindObjectsOfType(typeof(MeshFilter))));

                            bool nonTrigger = (collectInformation & GeometryType.PhysicsColliders) != 0;
                            bool trigger = (collectInformation & GeometryType.PhysicsTriggerColliders) != 0;
                            if (nonTrigger || trigger)
                                voxelizer = await voxelizer.Enqueue(Unsafe.As<Collider[]>(FindObjectsOfType(typeof(Collider))), nonTrigger && trigger ? 2 : (trigger ? 1 : 0));

                            if (!renderMeshes && !nonTrigger && !trigger)
                                ThrowCollectInformationNotChosen();

                            break;
                        }
                        case CollectionType.Children:
                        {
                            bool renderMeshes = (collectInformation & GeometryType.RenderMeshes) != 0;
                            if (renderMeshes)
                                voxelizer = await voxelizer.Enqueue(GetComponentsInChildren<MeshFilter>());

                            bool nonTrigger = (collectInformation & GeometryType.PhysicsColliders) != 0;
                            bool trigger = (collectInformation & GeometryType.PhysicsTriggerColliders) != 0;
                            if (nonTrigger || trigger)
                                voxelizer = await voxelizer.Enqueue(GetComponentsInChildren<Collider>(), nonTrigger && trigger ? 2 : (trigger ? 1 : 0));

                            if (!renderMeshes && !nonTrigger && !trigger)
                                ThrowCollectInformationNotChosen();
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

                    HeightField heightField = await HeightField.Create(options, voxelizer.Voxels);
                    options.StepTask();

                    CompactOpenHeightField compactOpenHeightField = await CompactOpenHeightField.Create(options, heightField);
                    options.StepTask();

                    heightField.Dispose();

                    KDTreeVector3<int> tree = new KDTreeVector3<int>();
                    options.PushTask(compactOpenHeightField.Columns.Length, "Building Spatial Tree");
                    {
                        int i = 0;
                        int width = options.VoxelizationParameters.Width;
                        int depth = options.VoxelizationParameters.Depth;


                        if (timeSlicer.ShouldUseTimeSlice)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                for (int z = 0; z < depth; z++)
                                {
                                    CompactOpenHeightField.HeightColumn heightColumn = compactOpenHeightField.Columns[i++];
                                    for (int j = heightColumn.First; j <= heightColumn.Last; j++)
                                    {
                                        int floor = compactOpenHeightField.Spans[j].Floor;
                                        if (floor != CompactOpenHeightField.HeightSpan.NULL_SIDE)
                                            tree.Add(new NVector3(x, floor, z), j);
                                    }
                                    options.StepTask();
                                    await timeSlicer.Yield();
                                }
                            }
                        }
                        else
                        {

                            for (int x = 0; x < width; x++)
                            {
                                for (int z = 0; z < depth; z++)
                                {
                                    CompactOpenHeightField.HeightColumn heightColumn = compactOpenHeightField.Columns[i++];
                                    for (int j = heightColumn.First; j <= heightColumn.Last; j++)
                                    {
                                        int floor = compactOpenHeightField.Spans[j].Floor;
                                        if (floor != CompactOpenHeightField.HeightSpan.NULL_SIDE)
                                            tree.Add(new NVector3(x, floor, z), j);
                                    }
                                    options.StepTask();
                                }
                            }
                        }
                    }
                    options.PopTask();
                    options.StepTask();

                    int[] spanToColumn;
                    options.PushTask(compactOpenHeightField.Columns.Length, "Building Lookup Table");
                    {
                        spanToColumn = ArrayPool<int>.Shared.Rent(compactOpenHeightField.Spans.Length);
                        int columnsCount = options.VoxelizationParameters.ColumnsCount;
                        if (timeSlicer.PreferMultithreading)
                        {
                            Parallel.For(0, columnsCount, i =>
                            {
                                CompactOpenHeightField.HeightColumn column = compactOpenHeightField.Columns[i];
                                column.Span<int>(spanToColumn).Fill(i);
                                options.StepTask();
                            });
                        }
                        else
                        {
                            if (timeSlicer.ShouldUseTimeSlice)
                            {
                                for (int i = 0; i < columnsCount; i++)
                                {
                                    CompactOpenHeightField.HeightColumn column = compactOpenHeightField.Columns[i];
                                    column.Span<int>(spanToColumn).Fill(i);
                                    options.StepTask();
                                    await timeSlicer.Yield();
                                }
                            }
                            else
                            {
                                for (int i = 0; i < columnsCount; i++)
                                {
                                    CompactOpenHeightField.HeightColumn column = compactOpenHeightField.Columns[i];
                                    column.Span<int>(spanToColumn).Fill(i);
                                    options.StepTask();
                                }
                            }
                        }
                    }
                    options.PopTask();
                    options.StepTask();

                    Lock();
                    try
                    {
                        // TODO: Return to pool NavigationMeshOptions.
                        // However, in order to do that, it's necessary to guarante that no other asynchronous task is using it...
                        this.options = options;
                        inProgress = null;
                        this.compactOpenHeightField = compactOpenHeightField;
                        if (this.spanToColumn != null)
                            ArrayPool<int>.Shared.Return(this.spanToColumn);
                        this.spanToColumn = spanToColumn;
                        // TODO: We could pool the KD tree.
                        this.tree = tree;
                    }
                    finally
                    {
                        Unlock();
                    }

                    voxelizer.Dispose();
                }
                options.PopTask();
                Debug.Assert(options.Progress == 1);
                options.TimeSlicer.MarkAsCompleted();
#if UNITY_EDITOR
                hasNavigation = true;
#endif

                void ThrowVoxelSizeMustBeGreaterThanZero() => throw new ArgumentException("Must be greater than 0.", nameof(voxelSize));
                void ThrowCollectInformationNotChosen() => throw new ArgumentException("Can't be default", nameof(collectInformation));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                options.TimeSlicer.RequestCancellation(); // Just to be sure.
                options = new NavigationGenerationOptions();
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Lock()
        {
            while (Interlocked.Exchange(ref navigationLock, 1) != 0) ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Unlock() => navigationLock = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool HasLineOfSight(Vector3 from, Vector3 to)
            => new Wrapper(this).HasLineOfSight(from, to);

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

            public void Reset()
            {
                index = 0;
            }

            public void Dispose() { }
        }

        private readonly struct Wrapper : IGraphLocation<int, Vector3>, IGraphHeuristic<int>, IGraphIntrinsic<int, NodesEnumerator>, IGraphLineOfSight<Vector3>, IGraphLineOfSight<int>
        {
            // Value type wrapper forces JIT to specialize code in generics
            // This replaces interface calls with direct inlineable calls.
            private readonly NavigationSurface self;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Wrapper(NavigationSurface self) => this.self = self;

            public bool TryFindNodeTo(Vector3 position, out int node)
            {
                VoxelizationParameters parameters = self.options.VoxelizationParameters;
                Vector3 localPosition = position - parameters.Min;
                float voxelSize = parameters.VoxelSize;
                Vector3 indexes_ = localPosition / voxelSize;
                if (indexes_.x < 0 || indexes_.y < 0 || indexes_.z < 0)
                    goto fail;

                Vector3Int indexes = new Vector3Int(Mathf.FloorToInt(indexes_.x), Mathf.FloorToInt(indexes_.y), Mathf.FloorToInt(indexes_.z));
                if (indexes.x >= parameters.Width || indexes.y >= parameters.Height || indexes.z >= parameters.Depth)
                    goto fail;

                CompactOpenHeightField compactOpenHeightField = self.compactOpenHeightField;
                CompactOpenHeightField.HeightColumn column = compactOpenHeightField.Columns[parameters.GetIndex(indexes.x, indexes.z)];

                // TODO: This is very error-prone.

                float errorTolerance = voxelSize * voxelSize;
                for (int i = column.First; i < column.Last; i++)
                {
                    ref readonly CompactOpenHeightField.HeightSpan span = ref compactOpenHeightField.Spans[i];
                    if (span.Floor >= indexes.y)
                    {
                        float squaredDistance = ((localPosition - new Vector3(indexes.x, span.Floor, indexes.z)) * voxelSize).sqrMagnitude;
                        if (squaredDistance < errorTolerance)
                        {
                            node = i;
                            return true;
                        }
                    }
                }

            fail:
                // Node could not be found, so look for closer one.
                if (self.tree.TryFindNearestNeighbour(localPosition.ToNumerics(), out NVector3 closest, out node))
                {
                    // We only take the node as valid if we are somewhat close to it.
                    return (closest.ToUnity() - localPosition).sqrMagnitude < voxelSize * voxelSize * 2;
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Vector3 ToPosition(int node)
            {
                Debug.Assert(node >= 0 && node < self.compactOpenHeightField.Spans.Length);
                int columnIndex = self.spanToColumn[node];
                ref readonly CompactOpenHeightField.HeightColumn column = ref self.compactOpenHeightField.Columns[columnIndex];
                Debug.Assert(node >= column.First && node < column.Last);
                VoxelizationParameters parameters = self.options.VoxelizationParameters;
                Vector2Int indexes = parameters.From2D(columnIndex);
                int y = self.compactOpenHeightField.Spans[node].Floor;
                Vector3 position = parameters.Min + (new Vector3(indexes.x, y, indexes.y) * parameters.VoxelSize);
                return position;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float GetHeuristicCost(int from, int to) => GetCost(from, to);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public NodesEnumerator GetNeighbours(int node)
            {
                Debug.Assert(node >= 0 && node < self.compactOpenHeightField.Spans.Length);
                return new NodesEnumerator(self.compactOpenHeightField.Spans[node]);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float GetCost(int from, int to)
                => Vector3.Distance(ToPosition(from), ToPosition(to));

            public bool RequiresUnityThread
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool HasLineOfSight(Vector3 from, Vector3 to)
                => !Physics.Linecast(from, to, self.includeLayers);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool HasLineOfSight(int from, int to)
                => HasLineOfSight(ToPosition(from), ToPosition(to));
        }

        private static void ThrowNavigationInProgress() => throw new InvalidOperationException("Navigation generation is in progress.");
    }
}
