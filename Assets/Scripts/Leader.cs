using Enderlook.Unity.Pathfinding;
using Enderlook.Unity.Pathfinding.Steerings;

using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(PathFollower), typeof(FlockingLeader))]
    public sealed class Leader : Creature
    {
        [Header("Leader")]
        [SerializeField, Tooltip("Path follower used to reach middle point.")]
        private PathFollower middlePointPathFollower;

        public FlockingLeader FlockingLeader { get; private set; }

        protected override void Awake()
        {
            FlockingLeader = GetComponent<FlockingLeader>();
            base.Awake();
        }

        protected override void MovingToTarget_OnEntry() => middlePointPathFollower.SetDestination(GameManager.MiddlePoint);

        protected override void MovingToTarget_OnExit() => middlePointPathFollower.Clear();

        protected override void MovingToTarget_OnUpdate()
        {
            base.MovingToTarget_OnUpdate();
            if (Vector3.Distance(transform.position, GameManager.MiddlePoint) < middlePointPathFollower.StoppingDistance)
                Brain.Fire(Event.ReachedToTarget);
            if (!middlePointPathFollower.IsCalculatingPath && !middlePointPathFollower.HasPath)
                MovingToTarget_OnEntry();
            CheckEnemyInSight();
        }
    }
}