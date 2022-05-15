using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [CreateAssetMenu(fileName = "Agent Type", menuName = "Enderlook/Pathfinding/Agent Type")]
    public class AgentType : ScriptableObject
    {
#pragma warning disable CS0649
        [SerializeField, Tooltip("How close to the walls navigation mesh exists.")]
        private float agentRadius = .5f;

        [SerializeField, Tooltip("How much vertical clearance space must exist.")]
        private float agentHeight = 2;

        [SerializeField, Range(0, 60), Tooltip("Maximum slope the agent can walk up.")]
        private float maxSlope = 45;

        [SerializeField, Tooltip("The height of discontinuities in the level the agent can climb over. (i.e: steps and stairs)")]
        private float stepHeight = .4f;
#pragma warning restore CS0649
    }
}
