using Assets.Enderlook.Unity.Pathfinding;
using Assets.Enderlook.Unity.Pathfinding.Steerings;

using System;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{

    [AddComponentMenu("Enderlook/Pathfinding/Navigation Agent Follower"), RequireComponent(typeof(Rigidbody)), DisallowMultipleComponent, DefaultExecutionOrder(ExecutionOrder.NavigationAgent)]
    public sealed class NavigationAgentFollower : MonoBehaviour
    {
        [Header("Flocking")]
        [SerializeField, Tooltip("Determines which leader will it follow.")]
        public NavigationAgentLeader FlockingLeader;

        [SerializeField, Min(0), Tooltip("Determines at which distance from leader should it stop.")]
        private float leaderStoppingDistance;
        public float LeaderStoppingDistance {
            get => leaderStoppingDistance;
            set => leaderStoppingDistance = ErrorMessage.NoNegativeGuard(nameof(LeaderStoppingDistance), value);
        }

        [SerializeField, Min(0), Tooltip("Determines the range of flocking.")]
        private float flockingRange;
        public float FlockingRange {
            get => flockingRange;
            set => flockingRange = ErrorMessage.NoNegativeGuard(nameof(FlockingRange), value);
        }

        [SerializeField, Min(0), Tooltip("Determines the seperation strength.")]
        private float separationWeight;
        public float SeparationWeight {
            get => separationWeight;
            set => separationWeight = ErrorMessage.NoNegativeGuard(nameof(SeparationWeight), value);
        }

        [SerializeField, Min(0), Tooltip("Determines the leader strength.")]
        private float leaderWeight;
        public float LeaderWeight {
            get => leaderWeight;
            set => leaderWeight = ErrorMessage.NoNegativeGuard(nameof(LeaderWeight), value);
        }

        [SerializeField, Min(0), Tooltip("Determines the cohesion strength.")]
        private float cohesionWeight;
        public float CohesionWeight {
            get => cohesionWeight;
            set => cohesionWeight = ErrorMessage.NoNegativeGuard(nameof(CohesionWeight), value);
        }

        [SerializeField, Min(0), Tooltip("Determines the alineation strength.")]
        private float alineationWeight;
        public float AlineationWeight {
            get => alineationWeight;
            set => alineationWeight = ErrorMessage.NoNegativeGuard(nameof(AlineationWeight), value);
        }

        [Header("Movement")]
        [SerializeField, Tooltip("Configuration of the agent movement.")]
        public Movement Movement;

        [Header("Leader Searcher")]
        [SerializeField, Tooltip("Configuration of the path used in case the leaders is out of sight.")]
        public PathFollower PathFollower;

        [SerializeField, Tooltip("Determines the strength of path.")]
        private float pathStrength;
        public float PathStrength {
            get => pathStrength;
            set => pathStrength = ErrorMessage.NoNegativeGuard(nameof(PathStrength), value);
        }

        [Header("Obstacle Avoidance")]
        [SerializeField, Tooltip("Configuration of the obstacle avoidance.")]
        public ObstacleAvoidance ObstacleAvoidance;

        internal Rigidbody Rigidbody { get; private set; }

        private Path<Vector3> path = new Path<Vector3>();
        private bool pathPending;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void Awake()
        {
            Rigidbody = GetComponent<Rigidbody>();
            Movement.Initialize(Rigidbody);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnEnable() => FlockingLeader.AddFollower(this);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnDisable() => FlockingLeader.RemoveFollower(this);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void FixedUpdate()
        {
            if (Movement.IsStopped)
                return;

            if (FlockingLeader == null)
                return;

            Movement.MoveAndRotate(Rigidbody, GetDirection());
        }

        private Vector3 GetDirection()
        {
            if (Vector3.Distance(Rigidbody.position, FlockingLeader.Rigidbody.position) < leaderStoppingDistance)
                return Vector3.zero;

            Span<EntityInfo> entities = FlockingLeader.GetEntitiesInRange(Rigidbody, flockingRange);

            Vector3 separation = GetSeparation(entities) * separationWeight;
            Vector3 alineation = GetAlineation(entities) * alineationWeight;
            Vector3 cohesion = GetCohesion(entities) * cohesionWeight;
            Vector3 leader = GetLeader() * leaderWeight;

            Vector3 obstacles = ObstacleAvoidance.GetDirection(Rigidbody);

            bool isNear = !Physics.Linecast(Rigidbody.position, FlockingLeader.Rigidbody.position, ObstacleAvoidance.Layers)
                && (Vector3.Distance(Rigidbody.position, FlockingLeader.Rigidbody.position) <= flockingRange);

            if (!isNear)
            {
                NavigationVolume navigationVolume = FlockingLeader.NavigationAgent.NavigationVolume;
                if (navigationVolume != null)
                {
                    Vector3 leaderPosition = FlockingLeader.Rigidbody.position;

                    if (path.IsComplete)
                    {
                        if (pathPending)
                        {
                            path.Complete();
                            pathPending = false;
                            PathFollower.SetPath(path);
                        }

                        if (path.HasPath)
                        {
                            if (Vector3.Distance(path.Destination, leaderPosition) > PathFollower.StoppingDistance)
                                CalculatePath(navigationVolume, leaderPosition);
                        }
                        else
                            CalculatePath(navigationVolume, leaderPosition);
                    }

                    if (PathFollower.HasPath)
                    {
                        Vector3 path = PathFollower.GetDirection(Rigidbody) * pathStrength;
                        return ((separation + alineation + cohesion).normalized * .5f + path + obstacles).normalized;
                    }
                }
            }

            return (separation + alineation + cohesion + leader + obstacles).normalized;
        }

        private void CalculatePath(NavigationVolume navigationVolume, Vector3 leaderPosition)
        {
            navigationVolume.CalculatePathSync(path, Rigidbody.position, leaderPosition);
            pathPending = true;
        }

        private Vector3 GetLeader() => (FlockingLeader.Rigidbody.position - Rigidbody.position).normalized;

        private Vector3 GetCohesion(Span<EntityInfo> entities)
        {
            Vector3 total = Vector3.zero;
            for (int i = 0; i < entities.Length; i++)
                total += entities[i].Position;
            total /= entities.Length;
            return (total - Rigidbody.position).normalized;
        }

        private Vector3 GetAlineation(Span<EntityInfo> entities)
        {
            Vector3 total = Vector3.zero;
            for (int i = 0; i < entities.Length; i++)
            {
                EntityInfo entity = entities[i];
                total += entity.FowardFactor;
            }
            return total.normalized;
        }

        private Vector3 GetSeparation(Span<EntityInfo> entities)
        {
            Vector3 total = Vector3.zero;
            for (int i = 0; i < entities.Length; i++)
            {
                EntityInfo entity = entities[i];
                float multiplier = flockingRange - entity.Distance;
                total += entity.RigidbodyMinusEntity.normalized * multiplier;
            }
            return total.normalized;
        }
    }
}