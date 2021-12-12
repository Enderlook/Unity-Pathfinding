using Enderlook.Pools;
using Enderlook.Unity.Pathfinding.Utils;

using System.Collections.Generic;
using System.Threading.Tasks;

using Enderlook.Threading;
using System;

namespace Enderlook.Unity.Pathfinding.Algorithms
{
    /// <summary>
    /// Helper class to calculate paths.
    /// </summary>
    internal static class PathCalculator
    {
        public static async ValueTask CalculatePath<TCoord, TNode, TNodes, TGraph, TBuilder, TPath, TSearcher, TWatchdog, TAwaitable, TAwaiter>(
            TGraph graph, TPath path, TCoord from, TSearcher searcher, TWatchdog watchdog)
            where TNodes : IEnumerator<TNode>
            where TGraph : class, IGraphIntrinsic<TNode, TNodes>, IGraphLocation<TNode, TCoord>, IGraphLineOfSight<TCoord>
            where TBuilder : class, IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>, new()
            where TPath : class, IPathFeedable<TCoord>, ISetTask, IEnumerable<TCoord>
            where TSearcher : struct, ISearcherSatisfy<TNode>
            where TWatchdog : IWatchdog<TAwaitable, TAwaiter>
            where TAwaitable : IAwaitable<TAwaiter>
            where TAwaiter : IAwaiter
            => new Calculator<TCoord, TNode, TNodes, TGraph, TBuilder, TPath, TSearcher, TWatchdog, TAwaitable, TAwaiter>(graph, path, from, searcher, watchdog).Process();

        private readonly struct Calculator<TCoord, TNode, TNodes, TGraph, TBuilder, TPath, TSearcher, TWatchdog, TAwaitable, TAwaiter>
            where TNodes : IEnumerator<TNode>
            where TGraph : class, IGraphIntrinsic<TNode, TNodes>, IGraphLocation<TNode, TCoord>, IGraphLineOfSight<TCoord>
            where TBuilder : class, IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>, new()
            where TPath : class, IPathFeedable<TCoord>, ISetTask, IEnumerable<TCoord>
            where TSearcher : struct, ISearcherSatisfy<TNode>
            where TWatchdog : IWatchdog<TAwaitable, TAwaiter>
            where TAwaitable : IAwaitable<TAwaiter>
            where TAwaiter : IAwaiter
        {
            private static readonly Func<Calculator<TCoord, TNode, TNodes, TGraph, TBuilder, TPath, TSearcher, TWatchdog, TAwaitable, TAwaiter>, Task> action = e => e.InternalProcess().AsTask();

            private readonly TGraph graph;
            private readonly TPath path;
            private readonly TCoord from;
            private readonly TSearcher searcher;
            private readonly TWatchdog watchdog;

            public Calculator(TGraph graph, TPath path, TCoord from, TSearcher searcher, TWatchdog watchdog)
            {
                this.graph = graph;
                this.path = path;
                this.from = from;
                this.searcher = searcher;
                this.watchdog = watchdog;
            }

            public ValueTask Process()
            {
                ValueTask task = watchdog.UseMultithreading ?
                    new ValueTask(Task.Factory.StartNew(action, this).Unwrap())
                    : InternalProcess();
                path.SetTask(task);
                return task;
            }

            private async ValueTask InternalProcess()
            {
                TBuilder builder = null;
                try
                {
                    builder = ObjectPool<TBuilder>.Shared.Rent();
                    builder.InitializeBuilderSession();
                    builder.SetGraphLocation(graph);
                    builder.SetLineOfSight(graph);
                    await AStar.CalculatePath<TCoord, TNode, TNodes, TGraph, TBuilder, TSearcher, TWatchdog, TAwaitable, TAwaiter>(graph, builder, from, searcher, watchdog);
                    builder.FeedPathTo(path);
                }
                finally
                {
                    if (!(builder is null))
                        ObjectPool<TBuilder>.Shared.Return(builder);
                }
            }
        }
    }
}