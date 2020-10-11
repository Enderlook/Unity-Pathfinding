using System.Collections.Generic;

namespace Enderlook.Unity.Pathfinding
{
    /// <inheritdoc cref="IGraphIntrinsic{TNode}"/>
    /// <typeparam name="TNodes">Type of node enumeration.</typeparam>
    public interface IGraphIntrinsic<TNode, TNodes> : IGraphIntrinsic<TNode>
        where TNodes : IEnumerable<TNode>
    {
        /// <inheritdoc cref="IGraphIntrinsic{TNode, TNodes}.GetNeighbours(TNode)"/>
        new TNodes GetNeighbours(TNode node);
    }
}