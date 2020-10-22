namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Represents the ephemereal id of a node.<br/>
    /// This handles aren't persistent and they become invalidates on load, save or modification of the graph.<br/>
    /// An invalid handle results in undefined behaviour.
    /// </summary>
    public readonly struct NodeId
    {
        internal readonly int id;

        internal NodeId(int id) => this.id = id;
    }
}