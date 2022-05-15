using Enderlook.Unity.Pathfinding.Steerings;

using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(FlockingFollower))]
    public sealed class Minion : Creature
    {
        [Header("Minion")]
        [SerializeField, Min(0), Tooltip("Strength of flocking behaviour.")]
        private float flockingStrength;

        private FlockingFollower flockingFollower;

        protected override void Awake()
        {
            flockingFollower = GetComponent<FlockingFollower>();
            base.Awake();
        }

        public void SetLeader(Leader leader) => flockingFollower.FlockingLeader = leader.FlockingLeader;

        protected override void MovingToTarget_OnEntry() => AgentRigidbody.SetSteeringBehaviour(flockingFollower, flockingStrength);

        protected override void MovingToTarget_OnExit() => AgentRigidbody.SetSteeringBehaviour(flockingFollower, 0);

        protected override void MovingToTarget_OnUpdate()
        {
            base.MovingToTarget_OnUpdate();
            CheckEnemyInSight();
        }
    }
}