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
    public sealed class NavigationSurface : MonoBehaviour, IGraphLocation<int, Vector3>, IGraphHeuristic<int>, IGraphIntrinsic<int, NavigationSurface.NodesEnumerator>, IGraphLineOfSight<Vector3>, IGraphLineOfSight<int>
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

        private int navigationLock;
        private CompactOpenHeightField compactOpenHeightField;
        private int[] spanToColumn;

        internal bool HasNavigation => !(options is null) && options.Progress == 1;

#if UNITY_EDITOR
        private static readonly Func<(bool IsEditor, NavigationSurface Instance), Task> buildNavigationFunc = async e => await e.Instance.BuildNavigation_(e.IsEditor);
#else
        private static readonly Func<NavigationSurface, Task> buildNavigationFunc = async e => await e.BuildNavigation_();
#endif

        private void Awake()
        {
            if (!(options is null))
                return;

            BuildNavigation();
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
        internal float Progress() => options.Progress;
#endif

        internal ValueTask CalculatePath(Path<Vector3> path, Vector3 position, Vector3 destination, bool synchronous = false)
        {
            Lock();
            {
                Awake();
            }
            Unlock();

            TimeSlicer timeSlicer = path.Start();
            timeSlicer.SetParent(options.TimeSlicer);
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

                if (!SearcherToLocationWithHeuristic<NavigationSurface, Vector3, int>.TryFrom(this, destination, out SearcherToLocationWithHeuristic<NavigationSurface, Vector3, int> searcher))
                {
                    path.ManualSetNotFound();
                    return;
                }

                await PathCalculator.CalculatePath<Vector3, int, NodesEnumerator, NavigationSurface, PathBuilder<int, Vector3>, Path<Vector3>, SearcherToLocationWithHeuristic<NavigationSurface, Vector3, int>, TimeSlicer, TimeSlicer.YieldAwait, TimeSlicer.YieldAwait, TimeSlicer.ToUnityAwait, TimeSlicer.ToUnityAwait>(this, path, position, searcher, timeSlicer);
            }
        }

        internal ValueTask BuildNavigation(
#if UNITY_EDITOR
            bool isEditor = false
#endif
            )
        {
            if (options is null)
                options = new NavigationGenerationOptions();

            TimeSlicer timeSlicer = options.TimeSlicer;

            if (!timeSlicer.IsCompleted) ThrowNavigationInProgress();

#if UNITY_EDITOR
            bool useMultithreading = timeSlicer.PreferMultithreading;
            if (isEditor)
                timeSlicer.PreferMultithreading = true;
#endif

            if (timeSlicer.PreferMultithreading)
                timeSlicer.SetTask(new ValueTask(Task.Factory.StartNew(buildNavigationFunc,
#if UNITY_EDITOR
                    (isEditor, this)
#else
                    this
#endif
                    ).Unwrap()));
            else
                BuildNavigation_(
#if UNITY_EDITOR
                    isEditor
#endif
                );

#if UNITY_EDITOR
            if (isEditor)
                timeSlicer.AsTask().GetAwaiter().OnCompleted(() => timeSlicer.PreferMultithreading = useMultithreading);
#endif

            return timeSlicer.AsTask();
        }

        internal ValueTask BuildNavigation_(
#if UNITY_EDITOR
            bool isEditor = false
#endif
            )
        {
            try
            {
                TimeSlicer timeSlicer = options.TimeSlicer;

                ValueTask task = Work();
                if (!timeSlicer.PreferMultithreading)
                    timeSlicer.SetTask(task);

                if (timeSlicer.ExecutionTimeSlice == 0)
                    timeSlicer.RunSynchronously();

                return timeSlicer.AsTask();

                async ValueTask Work()
                {
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

                    options.PushTask(4, "Generate Navigation Mesh");
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

                        int[] spanToColumn;
                        options.PushTask(compactOpenHeightField.Columns.Length, "Building Lookup Table");
                        {
                            spanToColumn = ArrayPool<int>.Shared.Rent(compactOpenHeightField.Spans.Length);
                            VoxelizationParameters parameters = options.VoxelizationParameters;
                            if (timeSlicer.PreferMultithreading)
                            {
                                Parallel.For(0, parameters.ColumnsCount, i =>
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
                                    for (int i = 0; i < parameters.ColumnsCount; i++)
                                    {
                                        CompactOpenHeightField.HeightColumn column = compactOpenHeightField.Columns[i];
                                        column.Span<int>(spanToColumn).Fill(i);
                                        options.StepTask();
                                        await timeSlicer.Yield();
                                    }
                                }
                                else
                                {
                                    for (int i = 0; i < parameters.ColumnsCount; i++)
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
                        {
                            this.compactOpenHeightField = compactOpenHeightField;
                            if (this.spanToColumn != null)
                                ArrayPool<int>.Shared.Return(this.spanToColumn);
                            this.spanToColumn = spanToColumn;
                        }
                        Unlock();

                        voxelizer.Dispose();
                    }
                    options.PopTask();
                    Debug.Assert(options.Progress == 1);
                    options.TimeSlicer.MarkAsCompleted();

                    void ThrowVoxelSizeMustBeGreaterThanZero() => throw new ArgumentException("Must be greater than 0.", nameof(voxelSize));
                    void ThrowCollectInformationNotChosen() => throw new ArgumentException("Can't be default", nameof(collectInformation));
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
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

        bool IGraphLocation<int, Vector3>.TryFindNodeTo(Vector3 position, out int node)
        {
            VoxelizationParameters parameters = options.VoxelizationParameters;
            Vector3 indexes_ = (position - parameters.Min) / parameters.VoxelSize;
            if (indexes_.x < 0 || indexes_.y < 0 || indexes_.z < 0)
                goto fail;

            Vector3Int indexes = new Vector3Int(Mathf.FloorToInt(indexes_.x), Mathf.FloorToInt(indexes_.y), Mathf.FloorToInt(indexes_.z));
            if (indexes.x >= parameters.Width || indexes.y >= parameters.Height || indexes.z >= parameters.Depth)
                goto fail;

            CompactOpenHeightField.HeightColumn column = compactOpenHeightField.Columns[parameters.GetIndex(indexes.x, indexes.z)];

            // TODO: This is very error-prone.

            for (int i = column.First; i < column.Last; i++)
            {
                ref readonly CompactOpenHeightField.HeightSpan span = ref compactOpenHeightField.Spans[i];
                if (span.Floor >= indexes.y)
                {
                    node = i;
                    return true;
                }
            }

        fail:
            // Node could not be found, so look for closer one.
            // TODO: This could be improved with spatial indexing.
            node = default;
            Vector3 offset = parameters.OffsetAtFloor;
            float minSquaredDistance = float.PositiveInfinity;
            int j = 0;
            for (int x = 0; x < parameters.Width; x++)
            {
                for (int z = 0; z < parameters.Depth; z++)
                {
                    Vector2 position_ = new Vector2(x, z) * parameters.VoxelSize;
                    column = compactOpenHeightField.Columns[j++];
                    for (int k = column.First; k < column.Last; k++)
                    {
                        ref readonly CompactOpenHeightField.HeightSpan span = ref compactOpenHeightField.Spans[k];
                        Vector3 position__ = new Vector3(position_.x, parameters.VoxelSize * span.Floor, position_.y);
                        Vector3 center = offset + position__;
                        float squaredDistance = (position - center).sqrMagnitude;
                        if (squaredDistance < minSquaredDistance)
                        {
                            minSquaredDistance = squaredDistance;
                            node = k;
                        }
                    }
                }
            }

            // We only take the node as valid if we are somewhat close to it.
            return minSquaredDistance < parameters.VoxelSize * 2;
        }

        Vector3 IGraphLocation<int, Vector3>.ToPosition(int node)
        {
            Debug.Assert(node >= 0 && node < compactOpenHeightField.Spans.Length);
            int columnIndex = spanToColumn[node];
            ref readonly CompactOpenHeightField.HeightColumn column = ref compactOpenHeightField.Columns[columnIndex];
            Debug.Assert(node >= column.First && node < column.Last);
            VoxelizationParameters parameters = options.VoxelizationParameters;
            Vector2Int indexes = parameters.From2D(columnIndex);
            int y = compactOpenHeightField.Spans[node].Floor;
            Vector3 position = parameters.Min + (new Vector3(indexes.x, y, indexes.y) * parameters.VoxelSize);
            //Debug.Assert(node == ((IGraphLocation<int, Vector3>)this).FindClosestNodeTo(position));
            return position;
        }

        float IGraphHeuristic<int>.GetHeuristicCost(int from, int to)
            => ((IGraphIntrinsic<int, NodesEnumerator>)this).GetCost(from, to);

        NodesEnumerator IGraphIntrinsic<int, NodesEnumerator>.GetNeighbours(int node)
        {
            Debug.Assert(node >= 0 && node < compactOpenHeightField.Spans.Length);
            return new NodesEnumerator(compactOpenHeightField.Spans[node]);
        }

        float IGraphIntrinsic<int, NodesEnumerator>.GetCost(int from, int to)
        {
            return Vector3.Distance(
                ((IGraphLocation<int, Vector3>)this).ToPosition(from),
                ((IGraphLocation<int, Vector3>)this).ToPosition(to)
            );
        }

        bool IGraphLineOfSight<Vector3>.RequiresUnityThread => true;

        bool IGraphLineOfSight<int>.RequiresUnityThread => true;

        bool IGraphLineOfSight<Vector3>.HasLineOfSight(Vector3 from, Vector3 to)
            => !Physics.Linecast(from, to, includeLayers);

        bool IGraphLineOfSight<int>.HasLineOfSight(int from, int to)
        {
            IGraphLocation<int, Vector3> graphLocation = this;
            return !Physics.Linecast(graphLocation.ToPosition(from), graphLocation.ToPosition(to), includeLayers);
        }

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

        private static void ThrowNavigationInProgress() => throw new InvalidOperationException("Navigation generation is in progress.");
    }
}
