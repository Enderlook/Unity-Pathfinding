namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Determines the final position of the searched location.
    /// </summary>
    /// <typeparam name="TCoord">Type of coordinate.</typeparam>
    public interface ISearcherPosition<TCoord>
    {
        /// <summary>
        /// Get the final position of the path.<br/>
        /// This is always called after <see cref="ISearcherSatisfy{TNode}.DoesSatisfy(TNode)"/> returns <see langword="true"/>.
        /// </summary>
        TCoord EndPosition { get; }
    }
}