namespace Enderlook.Unity.Pathfinding
{
    /// <inheritdoc cref="IPathBuilder{TNode}"/>
    /// <typeparam name="TNode">Type of coordinate.</typeparam>
    /// <remarks>Execution order of methods is:<br/>
    /// <list type="number">
    ///     <item><term><see cref="IPathBuilder{TNode}.InitializeBuilderSession()"/></term></item>
    ///     <item><term><see cref="SetNodeToPositionConverter(IGraphLocation{TNode, TCoord})"/></term></item>
    ///     <item><term><see cref="IPathBuilder{TNode}.SetStart(TCoord)"/> and <see cref="SetStart(TCoord)"/></term></item>
    ///     <item><term>Others.</term></item>
    ///     <item><term><see cref="SetEnd(TCoord)"/> and <see cref="IPathBuilder{TNode}.SetEnd(TCoord)"/></term></item>
    ///     <item><term><see cref="IPathBuilder{TNode}.FinalizeBuilderSession(CalculationResult)"/></term></item>
    /// </list>
    /// </remarks>
    public interface IPathBuilder<TNode, TCoord> : IPathBuilder<TNode>
    {
        /// <summary>
        /// Set the converter between nodes to positions.<br/>
        /// After finalizing the execution of <see cref="IPathBuilder{TNode}.FinalizeBuilderSession(CalculationResult)"/>, the <paramref name="converter"/> is no longer valid and should not rely on it.
        /// </summary>
        /// <param name="converter">Converter which converts nodes into positions.</param>
        void SetNodeToPositionConverter(IGraphLocation<TNode, TCoord> converter);

        /// <summary>
        /// Set the end position of this path.
        /// </summary>
        /// <param name="end">End position of the path.</param>
        /// <remarks>The argument of this method may be the same as the argument of <see cref="SetStart(TCoord)"/> or be <see cref="default"/>.</remarks>
        void SetEnd(TCoord end);

        /// <summary>
        /// Set the start position of this path.
        /// </summary>
        /// <param name="start">Start position of the path.</param>
        void SetStart(TCoord start);
    }
}