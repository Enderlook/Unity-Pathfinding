using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Unity.Pathfinding.Utils;

using System;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Steerings
{
    [AddComponentMenu("Enderlook/Pathfinding/Flocking Follower"), RequireComponent(typeof(Rigidbody)), DisallowMultipleComponent, DefaultExecutionOrder(ExecutionOrder.NavigationAgent)]
    public sealed class FlockingFollower : MonoBehaviour, ISteeringBehaviour
    {
        [Header("Flocking")]
        [SerializeField, Tooltip("Determines which leader will it follow.")]
        private FlockingLeader flockingLeader;
        public FlockingLeader FlockingLeader {
            get => flockingLeader;
            set
            {
                if (subscribedToLeader && flockingLeader is FlockingLeader leader)
                {
                    subscribedToLeader = false;
                    leader.RemoveFollower(this);
                }
                flockingLeader = value;
                if (value is FlockingLeader leader_)
                {
                    subscribedToLeader = true;
                    leader_.AddFollower(this);
                }
            }
        }

        private bool subscribedToLeader;

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

        [Header("Leader Searcher")]
        [SerializeField, Tooltip("Configuration of the path used in case the leader is out of sight." +
            "\nThis component must not be registered into the Navigation Agent Rigidbody.")]
        private PathFollower pathFollower;
        public PathFollower PathFollower
        {
            get => pathFollower;
            set => pathFollower = value;
        }

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

        [SerializeField, Tooltip("Determines which layers blocks the vision of the agent when looking for the leader.")]
        private LayerMask blockVisionLayers;
        public LayerMask BlockVisionLayers
        {
            get => blockVisionLayers;
            set => blockVisionLayers = value;
        }

        [SerializeField, Tooltip("Determines cooldown used for recalculating path to the leader.")]
        private float pathRecalculationCooldown = 4;
        public float PathRecalculationCooldown
        {
            get => pathRecalculationCooldown;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                pathRecalculationCooldown = value;
            }
        }

        internal Rigidbody Rigidbody { get; private set; }
        private float cooldown;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void Awake() => Rigidbody = GetComponent<Rigidbody>();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnEnable()
        {
            if (!subscribedToLeader)
                return;
            FlockingLeader.AddFollower(this);
            subscribedToLeader = true;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnDisable()
        {
            if (!subscribedToLeader)
                return;
            FlockingLeader.RemoveFollower(this);
            subscribedToLeader = false;
        }

        /// <inheritdoc cref="ISteeringBehaviour.GetDirection()"/>
        Vector3 ISteeringBehaviour.GetDirection() => GetDirection();

        private Vector3 GetDirection()
        {
            FlockingLeader flockingLeader = FlockingLeader;

            Vector3 direction = Rigidbody.position - flockingLeader.Rigidbody.position;
            if (direction.magnitude < leaderStoppingDistance)
                return direction.normalized;

            Span<EntityInfo> entities = flockingLeader.GetEntitiesInRange(Rigidbody, flockingRange);

            Vector3 separation = Vector3.zero;
            Vector3 alineation = Vector3.zero;
            Vector3 cohesion = Vector3.zero;
            for (int i = 0; i < entities.Length; i++)
            {
                EntityInfo entity = entities[i];

                float multiplier = flockingRange - entity.Distance;
                separation += entity.RigidbodyMinusEntity.normalized * multiplier;

                alineation += entity.ForwardFactor;

                cohesion += entity.Position;
            }
            separation = separation.normalized;
            alineation = alineation.normalized;
            cohesion = ((cohesion / entities.Length) - Rigidbody.position).normalized;

            Vector3 leader = (FlockingLeader.Rigidbody.position - Rigidbody.position).normalized * leaderWeight;

            if (PathFollower.NavigationSurface != null)
            {
                bool isNear = !Physics.Linecast(Rigidbody.position, flockingLeader.Rigidbody.position, BlockVisionLayers)
                    && (Vector3.Distance(Rigidbody.position, flockingLeader.Rigidbody.position) <= flockingRange);
                if (!isNear)
                {
                    Vector3 leaderPosition = flockingLeader.Rigidbody.position;

                    if (!PathFollower.IsCalculatingPath)
                    {
                        if (PathFollower.HasPath)
                        {
                            if (Vector3.Distance(PathFollower.Destination, leaderPosition) > PathFollower.StoppingDistance)
                                PathFollower.SetDestination(leaderPosition);
                        }
                        else
                            PathFollower.SetDestination(leaderPosition);
                    }

                    if (PathFollower.HasPath)
                    {
                        cooldown -= Time.fixedDeltaTime;
                        if (cooldown < 0)
                        {
                            cooldown = PathRecalculationCooldown;
                            if (!PathFollower.IsCalculatingPath && Vector3.Distance(PathFollower.NextPosition, Rigidbody.position) > PathFollower.StoppingDistance * 2)
                                PathFollower.SetDestination(leaderPosition);
                        }

                        Vector3 path = PathFollower.GetDirection() * pathStrength;
                        return (((separation + alineation + cohesion).normalized * .2f) + path).normalized;
                    }
                }
            }

            {
                return (separation + alineation + cohesion + leader).normalized;
            }
        }

#if UNITY_EDITOR
        void ISteeringBehaviour.DrawGizmos()
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
            Gizmos.DrawLine(Rigidbody.position, Rigidbody.position + (direction * 3));
            Gizmos.color = Color.gray;

            RawPooledList<Vector3>.Enumerator enumerator = PathFollower.previousEnumerator;
            if (enumerator.IsDefault)
                return;

            Gizmos.color = Color.black;
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
    }
}