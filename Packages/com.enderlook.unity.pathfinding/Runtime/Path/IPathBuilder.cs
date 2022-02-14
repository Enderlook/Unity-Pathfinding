using Enderlook.Unity.Pathfinding.Utils;

using System;
using System.Threading.Tasks;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Constructor of a path between two nodes.
    /// </summary>
    /// <typeparam name="TNode">Type of node.</typeparam>
    /// <typeparam name="TCoord">Type of coordinate.</typeparam>
    /// <remarks>Execution order of methods is:<br/>
    /// <list type="number">
    ///     <item><term><see cref="IPathBuilder{TNode, TCoord}.InitializeBuilderSession()"/></term></item>
    ///     <item><term><see cref="IPathBuilder{TNode, TCoord}.SetStart(TCoord, TNode)"/></term></item>
    ///     <item><term>Others.</term></item>
    ///     <item><term><see cref="IPathBuilder{TNode, TCoord}.SetEnd(TCoord, TNode)"/> and <see cref="IPathBuilder{TNode, TCoord}.SetEnd(TCoord, TNode)"/></term></item>
    ///     <item><term><see cref="IPathBuilder{TNode, TCoord}.FinalizeBuilderSession{TGraph, TWatchdog, TWatchdogAwaitable, TWatchdogAwaiter, TThreadingAwaitable, TThreadingAwaiter}(TGraph, CalculationResult, TWatchdog)"/></term></item>
    /// </list>
    /// </remarks>
    internal interface IPathBuilder<TNode, TCoord>
    {
        /// <summary>
        /// Enqueues a node to visit it later.
        /// </summary>
        /// <param name="node">Node to enqueue.</param>
        /// <param name="priority">Priority of the node.</param>
        void EnqueueToVisit(TNode node, float priority);

        /// <summary>
        /// Clear the current path and initializes a new builder session.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the builder session was already enabled.</exception>
        void InitializeBuilderSession();

        /// <summary>
        /// Finalize the building session.
        /// </summary>
        /// <param name="graph">Graph used during the building session.</param>
        /// <param name="result">Result status.</param>
        /// <param name="watchdog">Watchdog of the operation.</param>
        /// <exception cref="InvalidOperationException">Thrown when no builder session was enabled.</exception>
        /// <remarks>If the initial node and the target node are the same, no other method is necessary to be executed apart from this one.</remarks>
        ValueTask FinalizeBuilderSession<TGraph, TWatchdog, TWatchdogAwaitable, TWatchdogAwaiter, TThreadingAwaitable, TThreadingAwaiter>(TGraph graph, CalculationResult result, TWatchdog watchdog)
            where TGraph : IGraphLocation<TNode, TCoord>/*, IGraphLineOfSight<TNode>, IGraphLineOfSight<TCoord>*/
            where TWatchdog : IWatchdog<TWatchdogAwaitable, TWatchdogAwaiter>, IThreadingPreference<TThreadingAwaitable, TThreadingAwaiter>
            where TWatchdogAwaitable : IAwaitable<TWatchdogAwaiter>
            where TWatchdogAwaiter : IAwaiter
            where TThreadingAwaitable : IAwaitable<TThreadingAwaiter>
            where TThreadingAwaiter : IAwaiter;

        /// <summary>
        /// Try to dequeue a node (if any) to visit.
        /// </summary>
        /// <param name="node">Dequeued node, if any.</param>
        /// <returns>Whenever a node was dequeued of not.</returns>
        /// <remarks>It doesn't need to check if the node was already visited. For that <see cref="VisitIfWasNotVisited(TNode)"/> is used.</remarks>
        bool TryDequeueToVisit(out TNode node);

        /// <summary>
        /// Get the cost required to travel from the starting node to <paramref name="to"/>.
        /// </summary>
        /// <param name="to">Destination node.</param>
        /// <param name="cost">Cost required to travel from the starting node to <paramref name="to"/>, if any.</param>
        /// <returns>Whenever a cost was found or not.</returns>
        /// <remarks>This value was previously setted by <seealso cref="SetCost(TNode, float)"/>.</remarks>
        bool TryGetCost(TNode to, out float cost);

        /// <summary>
        /// Get the from node which travels to <paramref name="to"/>.
        /// </summary>
        /// <param name="to">Destination node.</param>
        /// <param name="from">Parent node used to reach to <paramref name="to"/>, if any.</param>
        /// <returns>Whenever this value was previously setted by <seealso cref="SetEdge(TNode, TNode)"/>.</returns>
        bool TryGetEdge(TNode to, out TNode from);

        /// <summary>
        /// Stores the cost required to travel from the starting node to <paramref name="to"/>.
        /// </summary>
        /// <param name="to">Node to set cost.</param>
        /// <param name="cost">Cost required to travel from the starting node to <paramref name="to"/>.</param>
        void SetCost(TNode to, float cost);

        /// <summary>
        /// Set an edge between two nodes.
        /// </summary>
        /// <param name="from">Starting edge's node.</param>
        /// <param name="to">End edge's node.</param>
        void SetEdge(TNode from, TNode to);

        /// <summary>
        /// Set the end node of this path.
        /// </summary>
        /// <param name="endPosition">End node of the path.</param>
        /// <param name="endNode">End node of the path.</param>
        /// <remarks>The argument of this method may be the same as the argument of <see cref="SetStart(TNode)"/> or be <see cref="default"/>.</remarks>
        void SetEnd(TCoord endPosition, TNode endNode);

        /// <summary>
        /// Set the start node of this path.
        /// </summary>
        /// <param name="startPosition">Start node of the path.</param>
        /// <param name="startNode">Start node of the path.</param>
        void SetStart(TCoord startPosition, TNode startNode);

        /// <summary>
        /// Mark the node as visited.
        /// </summary>
        /// <param name="node">Node to mask as visited.</param>
        /// <returns><see langword="true"/> if <paramref name="node"/> <see langword="false"/> if <paramref name="node"/> was already visited.</returns>
        bool VisitIfWasNotVisited(TNode node);
    }
}