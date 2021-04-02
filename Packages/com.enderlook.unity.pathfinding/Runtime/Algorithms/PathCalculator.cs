using Enderlook.Unity.Jobs;

using System.Collections.Generic;

using Unity.Jobs;

namespace Enderlook.Unity.Pathfinding.Algorithms
{
    internal static partial class PathCalculator
    {
        public static void CalculatePathSingleThread<TCoord, TNode, TNodes, TGraph, TBuilder, TPath, TSearcher, TWatchdog>
            (TGraph graph, TPath path, TCoord from, TSearcher searcher, TWatchdog watchdog)
            where TNodes : IEnumerator<TNode>
            where TGraph : class, IGraphIntrinsic<TNode, TNodes>, IGraphHeuristic<TNode>, IGraphLocation<TNode, TCoord>, IGraphLineOfSight<TCoord>
            where TBuilder : class, IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>, new()
            where TPath : class, IPathFeedable<TCoord>, IEnumerable<TCoord>
            where TSearcher : ISearcherSatisfy<TNode>, ISearcherHeuristic<TNode>
            where TWatchdog : IWatchdog
        {
            ((IProcessHandleSourceCompletition)path).Start();
            TBuilder builder = ConcurrentPool<TBuilder>.Rent();
            AStar.CalculatePath<TCoord, TNode, TNodes, TGraph, TBuilder, TSearcher, TWatchdog>(graph, builder, from, searcher, watchdog);
            builder.FeedPathTo(path);
            ConcurrentPool<TBuilder>.Return(builder);
            ((IProcessHandleSourceCompletition)path).End();
            ((IProcessHandle)path).Complete();
        }

        public static void CalculatePathSingleThread<TCoord, TNode, TNodes, TGraph, TBuilder, TPath, TSearcher>
            (TGraph graph, TPath path, TCoord from, TSearcher searcher)
            where TNodes : IEnumerator<TNode>
            where TGraph : class, IGraphIntrinsic<TNode, TNodes>, IGraphHeuristic<TNode>, IGraphLocation<TNode, TCoord>, IGraphLineOfSight<TCoord>
            where TBuilder : class, IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>, new()
            where TPath : class, IPathFeedable<TCoord>, IEnumerable<TCoord>
            where TSearcher : ISearcherSatisfy<TNode>, ISearcherHeuristic<TNode>
        {
            ((IProcessHandleSourceCompletition)path).Start();
            TBuilder builder = ConcurrentPool<TBuilder>.Rent();
            AStar.CalculatePath<TCoord, TNode, TNodes, TGraph, TBuilder, TSearcher>(graph, builder, from, searcher);
            builder.FeedPathTo(path);
            ConcurrentPool<TBuilder>.Return(builder);
            ((IProcessHandleSourceCompletition)path).End();
            ((IProcessHandle)path).Complete();
        }

        public static void CalculatePathSingleThread<TCoord, TNode, TNodes, TGraph, TBuilder, TPath, TWatchdog>
            (TGraph graph, TPath path, TCoord from, TCoord to, TWatchdog watchdog)
            where TNodes : IEnumerator<TNode>
            where TGraph : class, IGraphIntrinsic<TNode, TNodes>, IGraphHeuristic<TNode>, IGraphLocation<TNode, TCoord>, IGraphLineOfSight<TCoord>
            where TBuilder : class, IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>, new()
            where TPath : class, IPathFeedable<TCoord>, IEnumerable<TCoord>
            where TWatchdog : IWatchdog
        {
            ((IProcessHandleSourceCompletition)path).Start();
            TBuilder builder = ConcurrentPool<TBuilder>.Rent();
            AStar.CalculatePath<TCoord, TNode, TNodes, TGraph, TBuilder, TWatchdog>(graph, builder, from, to, watchdog);
            builder.FeedPathTo(path);
            ConcurrentPool<TBuilder>.Return(builder);
            ((IProcessHandleSourceCompletition)path).End();
            ((IProcessHandle)path).Complete();
        }

        public static void CalculatePathSingleThread<TCoord, TNode, TNodes, TGraph, TBuilder, TPath>
            (TGraph graph, TPath path, TCoord from, TCoord to)
            where TNodes : IEnumerator<TNode>
            where TGraph : class, IGraphIntrinsic<TNode, TNodes>, IGraphHeuristic<TNode>, IGraphLocation<TNode, TCoord>, IGraphLineOfSight<TCoord>
            where TBuilder : class, IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>, new()
            where TPath : class, IPathFeedable<TCoord>, IEnumerable<TCoord>
        {
            ((IProcessHandleSourceCompletition)path).Start();
            TBuilder builder = ConcurrentPool<TBuilder>.Rent();
            AStar.CalculatePath<TCoord, TNode, TNodes, TGraph, TBuilder>(graph, builder, from, to);
            builder.FeedPathTo(path);
            ConcurrentPool<TBuilder>.Return(builder);
            ((IProcessHandleSourceCompletition)path).End();
            ((IProcessHandle)path).Complete();
        }

        public static void CalculatePathJob<TCoord, TNode, TNodes, TGraph, TBuilder, TPath, TSearcher, TWatchdog>
            (TGraph graph, TPath path, TCoord from, TSearcher searcher, TWatchdog watchdog)
            where TNodes : IEnumerator<TNode>
            where TGraph : class, IGraphIntrinsic<TNode, TNodes>, IGraphHeuristic<TNode>, IGraphLocation<TNode, TCoord>, IGraphLineOfSight<TCoord>
            where TBuilder : class, IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>, new()
            where TPath : class, IPathFeedable<TCoord>, IEnumerable<TCoord>
            where TSearcher : ISearcherSatisfy<TNode>, ISearcherHeuristic<TNode>
            where TWatchdog : IWatchdog
        {
            ((IProcessHandleSourceCompletition)path).Start();
            JobHandle jobHandle = new JobSearcherWatchdog<TCoord, TNode, TNodes, TGraph, TBuilder, TPath, TSearcher, TWatchdog>(graph, path, from, searcher, watchdog).Schedule();
            ((IProcessHandleSourceCompletition)path).SetJobHandle(jobHandle);
        }

        public static void CalculatePathJob<TCoord, TNode, TNodes, TGraph, TBuilder, TPath, TSearcher>
            (TGraph graph, TPath path, TCoord from, TSearcher searcher)
            where TNodes : IEnumerator<TNode>
            where TGraph : class, IGraphIntrinsic<TNode, TNodes>, IGraphHeuristic<TNode>, IGraphLocation<TNode, TCoord>, IGraphLineOfSight<TCoord>
            where TBuilder : class, IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>, new()
            where TPath : class, IPathFeedable<TCoord>, IEnumerable<TCoord>
            where TSearcher : ISearcherSatisfy<TNode>, ISearcherHeuristic<TNode>
        {
            ((IProcessHandleSourceCompletition)path).Start();
            JobHandle jobHandle = new JobSearcher<TCoord, TNode, TNodes, TGraph, TBuilder, TPath, TSearcher>(graph, path, from, searcher).Schedule();
            ((IProcessHandleSourceCompletition)path).SetJobHandle(jobHandle);
        }

        public static void CalculatePathJob<TCoord, TNode, TNodes, TGraph, TBuilder, TPath, TWatchdog>
            (TGraph graph, TPath path, TCoord from, TCoord to, TWatchdog watchdog)
            where TNodes : IEnumerator<TNode>
            where TGraph : class, IGraphIntrinsic<TNode, TNodes>, IGraphHeuristic<TNode>, IGraphLocation<TNode, TCoord>, IGraphLineOfSight<TCoord>
            where TBuilder : class, IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>, new()
            where TPath : class, IPathFeedable<TCoord>, IEnumerable<TCoord>
            where TWatchdog : IWatchdog
        {
            ((IProcessHandleSourceCompletition)path).Start();
            JobHandle jobHandle = new JobWatchdog<TCoord, TNode, TNodes, TGraph, TBuilder, TPath, TWatchdog>(graph, path, from, to, watchdog).Schedule();
            ((IProcessHandleSourceCompletition)path).SetJobHandle(jobHandle);
        }

        public static void CalculatePathJob<TCoord, TNode, TNodes, TGraph, TBuilder, TPath>
            (TGraph graph, TPath path, TCoord from, TCoord to)
            where TNodes : IEnumerator<TNode>
            where TGraph : class, IGraphIntrinsic<TNode, TNodes>, IGraphHeuristic<TNode>, IGraphLocation<TNode, TCoord>, IGraphLineOfSight<TCoord>
            where TBuilder : class, IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>, new()
            where TPath : class, IPathFeedable<TCoord>, IEnumerable<TCoord>
        {
            ((IProcessHandleSourceCompletition)path).Start();
            JobHandle jobHandle = new Job<TCoord, TNode, TNodes, TGraph, TBuilder, TPath>(graph, path, from, to).Schedule();
            ((IProcessHandleSourceCompletition)path).SetJobHandle(jobHandle);
        }

        private struct JobSearcherWatchdog<TCoord, TNode, TNodes, TGraph, TBuilder, TPath, TSearcher, TWatchdog> : IManagedJob
            where TNodes : IEnumerator<TNode>
            where TGraph : class, IGraphIntrinsic<TNode, TNodes>, IGraphHeuristic<TNode>, IGraphLocation<TNode, TCoord>, IGraphLineOfSight<TCoord>
            where TBuilder : class, IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>, new()
            where TPath : class, IPathFeedable<TCoord>, IEnumerable<TCoord>
            where TSearcher : ISearcherSatisfy<TNode>, ISearcherHeuristic<TNode>
            where TWatchdog : IWatchdog
        {
            private readonly TGraph graph;
            private readonly TPath path;
            private readonly TCoord from;
            private readonly TSearcher searcher;
            private readonly TWatchdog watchdog;

            public JobSearcherWatchdog(TGraph graph, TPath path, TCoord from, TSearcher searcher, TWatchdog watchdog)
            {
                this.graph = graph;
                this.path = path;
                this.from = from;
                this.searcher = searcher;
                this.watchdog = watchdog;
            }

            void IManagedJob.Execute()
            {
                TBuilder builder = ConcurrentPool<TBuilder>.Rent();
                AStar.CalculatePath<TCoord, TNode, TNodes, TGraph, TBuilder, TSearcher, TWatchdog>(graph, builder, from, searcher, watchdog);
                ((IPathFeeder<TCoord>)builder).FeedPathTo(path);
                ((IProcessHandleSourceCompletition)path).End();
                ConcurrentPool<TBuilder>.Return(builder);
            }
        }

        private struct JobSearcher<TCoord, TNode, TNodes, TGraph, TBuilder, TPath, TSearcher> : IManagedJob
            where TNodes : IEnumerator<TNode>
            where TGraph : class, IGraphIntrinsic<TNode, TNodes>, IGraphHeuristic<TNode>, IGraphLocation<TNode, TCoord>, IGraphLineOfSight<TCoord>
            where TBuilder : class, IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>, new()
            where TPath : class, IPathFeedable<TCoord>, IEnumerable<TCoord>
            where TSearcher : ISearcherSatisfy<TNode>, ISearcherHeuristic<TNode>
        {
            private readonly TGraph graph;
            private readonly TPath path;
            private readonly TCoord from;
            private readonly TSearcher searcher;

            public JobSearcher(TGraph graph, TPath path, TCoord from, TSearcher searcher)
            {
                this.graph = graph;
                this.path = path;
                this.from = from;
                this.searcher = searcher;
            }

            void IManagedJob.Execute()
            {
                TBuilder builder = ConcurrentPool<TBuilder>.Rent();
                AStar.CalculatePath<TCoord, TNode, TNodes, TGraph, TBuilder, TSearcher>(graph, builder, from, searcher);
                ((IPathFeeder<TCoord>)builder).FeedPathTo(path);
                ((IProcessHandleSourceCompletition)path).End();
                ConcurrentPool<TBuilder>.Return(builder);
            }
        }

        private struct JobWatchdog<TCoord, TNode, TNodes, TGraph, TBuilder, TPath, TWatchdog> : IManagedJob
            where TNodes : IEnumerator<TNode>
            where TGraph : class, IGraphIntrinsic<TNode, TNodes>, IGraphHeuristic<TNode>, IGraphLocation<TNode, TCoord>, IGraphLineOfSight<TCoord>
            where TBuilder : class, IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>, new()
            where TPath : class, IPathFeedable<TCoord>, IEnumerable<TCoord>
            where TWatchdog : IWatchdog
        {
            private readonly TGraph graph;
            private readonly TPath path;
            private readonly TCoord from;
            private readonly TCoord to;
            private readonly TWatchdog watchdog;

            public JobWatchdog(TGraph graph, TPath path, TCoord from, TCoord to, TWatchdog watchdog)
            {
                this.graph = graph;
                this.path = path;
                this.from = from;
                this.to = to;
                this.watchdog = watchdog;
            }

            void IManagedJob.Execute()
            {
                TBuilder builder = ConcurrentPool<TBuilder>.Rent();
                AStar.CalculatePath<TCoord, TNode, TNodes, TGraph, TBuilder, TWatchdog>(graph, builder, from, to, watchdog);
                ((IPathFeeder<TCoord>)builder).FeedPathTo(path);
                ((IProcessHandleSourceCompletition)path).End();
                ConcurrentPool<TBuilder>.Return(builder);
            }
        }

        private struct Job<TCoord, TNode, TNodes, TGraph, TBuilder, TPath> : IManagedJob
            where TNodes : IEnumerator<TNode>
            where TGraph : class, IGraphIntrinsic<TNode, TNodes>, IGraphHeuristic<TNode>, IGraphLocation<TNode, TCoord>, IGraphLineOfSight<TCoord>
            where TBuilder : class, IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>, new()
            where TPath : class, IPathFeedable<TCoord>, IEnumerable<TCoord>
        {
            private readonly TGraph graph;
            private readonly TPath path;
            private readonly TCoord from;
            private readonly TCoord to;

            public Job(TGraph graph, TPath path, TCoord from, TCoord to)
            {
                this.graph = graph;
                this.path = path;
                this.from = from;
                this.to = to;
            }

            void IManagedJob.Execute()
            {
                TBuilder builder = ConcurrentPool<TBuilder>.Rent();
                AStar.CalculatePath<TCoord, TNode, TNodes, TGraph, TBuilder>(graph, builder, from, to);
                ((IPathFeeder<TCoord>)builder).FeedPathTo(path);
                ((IProcessHandleSourceCompletition)path).End();
                ConcurrentPool<TBuilder>.Return(builder);
            }
        }
    }
}
