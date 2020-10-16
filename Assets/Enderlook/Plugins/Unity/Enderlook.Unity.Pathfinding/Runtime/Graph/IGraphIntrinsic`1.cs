using System.Collections.Generic;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Interface to extract node information from a graph.
    /// </summary>
    /// <typeparam name="TNodeId">Type of node id.</typeparam>
    public interface IGraphIntrinsic<TNodeId>
    {
        /// <summary>
        /// Get all nodes which can be reached from <paramref name="node"/>.
        /// </summary>
        /// <param name="node">Starting node.</param>
        /// <returns>All nodes that can be reached from <paramref name="node"/>.</returns>
        IEnumerable<TNodeId> GetNeighbours(TNodeId node);

        /// <summary>
        /// Get the cost of travel from <paramref name="from"/> node to <paramref name="to"/> node.
        /// </summary>
        /// <param name="from">Initial node.</param>
        /// <param name="to">Destination node.</param>
        /// <returns>Cost of traveling from <paramref name="from"/> to <paramref name="to"/>.</returns>
        float GetCost(TNodeId from, TNodeId to);
    }
}