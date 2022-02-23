using Enderlook.Unity.Pathfinding.Steerings;

using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Unity.Pathfinding.Utils;

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
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                leaderStoppingDistance = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the range of flocking.")]
        private float flockingRange;
        public float FlockingRange {
            get => flockingRange;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                flockingRange = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the seperation strength.")]
        private float separationWeight;
        public float SeparationWeight {
            get => separationWeight;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                separationWeight = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the leader strength.")]
        private float leaderWeight;
        public float LeaderWeight {
            get => leaderWeight;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                leaderWeight = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the cohesion strength.")]
        private float cohesionWeight;
        public float CohesionWeight {
            get => cohesionWeight;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                cohesionWeight = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the alineation strength.")]
        private float alineationWeight;
        public float AlineationWeight {
            get => alineationWeight;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                alineationWeight = value;
            }
        }

        [Header("Movement")]
        [SerializeField, Tooltip("Configuration of the agent movement.")]
        public Movement Movement;

        [Header("Leader Searcher")]
        [SerializeField, Tooltip("Configuration of the path used in case the leader is out of sight.")]
        public PathFollower PathFollower;

        [SerializeField, Tooltip("Determines the strength of path.")]
        private float pathStrength;
        public float PathStrength {
            get => pathStrength;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                pathStrength = value;
            }
        }

        [Header("Obstacle Avoidance")]
        [SerializeField, Tooltip("Configuration of the obstacle avoidance.")]
        public ObstacleAvoidance ObstacleAvoidance;

        internal Rigidbody Rigidbody { get; private set; }

        private Path<Vector3> path;
        private bool isPending;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void Awake()
        {
            Rigidbody = GetComponent<Rigidbody>();
            Movement.Initialize(Rigidbody);
            path = Path<Vector3>.Rent();
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

        private float cooldown = maxCooldown;
        private const float maxCooldown = 4;

        private Vector3 GetDirection()
        {
            Vector3 direction = Rigidbody.position - FlockingLeader.Rigidbody.position;
            if (direction.magnitude < leaderStoppingDistance)
                return direction.normalized;

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
                NavigationSurface navigationSurface = FlockingLeader.NavigationAgent.NavigationSurface;
                if (navigationSurface != null)
                {
                    Vector3 leaderPosition = FlockingLeader.Rigidbody.position;

                    if (path.IsCompleted)
                    {
                        if (isPending)
                        {
                            isPending = false;
                            PathFollower.SetPath(path);
                        }

                        if (path.HasPath)
                        {
                            if (Vector3.Distance(path.Destination, leaderPosition) > PathFollower.StoppingDistance)
                                CalculatePath(navigationSurface, leaderPosition);
                        }
                        else
                            CalculatePath(navigationSurface, leaderPosition);
                    }

                    if (PathFollower.HasPath)
                    {
                        cooldown -= Time.fixedDeltaTime;
                        if (cooldown < 0)
                        {
                            cooldown = maxCooldown;
                            if (!isPending && Vector3.Distance(PathFollower.NextPosition, Rigidbody.position) > PathFollower.StoppingDistance * 2)
                                CalculatePath(navigationSurface, leaderPosition);
                        }

                        Vector3 path = PathFollower.GetDirection(Rigidbody) * pathStrength;
                        return (((separation + alineation + cohesion).normalized * .2f) + path + obstacles).normalized;
                    }
                }
            }

            {
                return (separation + alineation + cohesion + leader + obstacles).normalized;
            }
        }

        private void CalculatePath(NavigationSurface navigationSurface, Vector3 leaderPosition)
        {
            navigationSurface.CalculatePath(path, Rigidbody.position, leaderPosition);
            isPending = true;
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
                total += entity.ForwardFactor;
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

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
                return;

            /*Span<EntityInfo> entities = FlockingLeader.GetEntitiesInRange(Rigidbody, flockingRange);

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(Rigidbody.position, GetSeparation(entities) * separationWeight);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(Rigidbody.position, GetAlineation(entities) * alineationWeight);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(Rigidbody.position, GetCohesion(entities) * cohesionWeight);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(Rigidbody.position, GetLeader() * leaderWeight);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(Rigidbody.position, ObstacleAvoidance.GetDirection(Rigidbody));
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(Rigidbody.position, PathFollower.GetDirection(Rigidbody) * pathStrength);
            Gizmos.color = Color.white;
            Gizmos.DrawLine(Rigidbody.position, q);*/

            Vector3 direction = GetDirection();
            Gizmos.color = Color.white;
            Gizmos.DrawLine(Rigidbody.position, Rigidbody.position + (direction * 3));

            RawPooledList<Vector3>.Enumerator enumerator = PathFollower.previousEnumerator;
            if (enumerator.IsDefault)
                return;

            Gizmos.color = Color.blue;
            Vector3 start;
            Vector3 end = transform.position;
            while (enumerator.MoveNext())
            {
                Gizmos.DrawWireCube(end, Vector3.one * .1f);
                start = end;
                end = enumerator.Current;
                Gizmos.DrawLine(start, end);
            }
            Gizmos.DrawWireCube(end, Vector3.one * .1f);
        }
#endif

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnDestroy() => path.Dispose();
    }
}