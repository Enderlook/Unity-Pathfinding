using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Enderlook.Unity.Pathfinding.Algorithms
{
    internal sealed partial class Manager<TCoord, TNode, TNodes, TGraph, TBuilder>
        where TCoord : unmanaged
        where TNode : unmanaged
        where TNodes : IEnumerator<TNode>
        where TGraph : class, IGraphIntrinsic<TNode, TNodes>, IGraphHeuristic<TNode>, IGraphLocation<TNode, TCoord>
        where TBuilder : class, IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>, new()
    {
        private readonly TGraph graph;
        private readonly ConcurrentQueue<TBuilder> builders = new ConcurrentQueue<TBuilder>();
        private readonly TBuilder builderSync = new TBuilder();

        public Manager(TGraph graph) => this.graph = graph;

        private TBuilder GetBuilder()
        {
            if (builders.TryDequeue(out TBuilder builder))
                return builder;
            return new TBuilder();
        }

        private void StoreBuilder(TBuilder node) => builders.Enqueue(node);
    }
}