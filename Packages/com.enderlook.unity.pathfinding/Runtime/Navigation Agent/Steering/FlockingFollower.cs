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
        [SerializeField, Tooltip("Determines which leader will it follow.\n" +
            "If leader is null, not active or disabled, this steering behaviour will return zero.")]
        private FlockingLeader flockingLeader;
        public FlockingLeader FlockingLeader {
            get => flockingLeader;
            set
            {
                FlockingLeader leader = flockingLeader;

                if (leader == value)
                    return;

                next = 0;
                flockingLeader = value;

                if (subscribedToLeader && leader != null)
                    leader.RemoveFollower(this);

                if (value != null)
                {
                    if (isActiveAndEnabled)
                    {
                        subscribedToLeader = true;
                        value.AddFollower(this);
                    }
                }
                else
                    subscribedToLeader = false;
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

        [SerializeField, Tooltip("Determines which layers blocks the vision of the agent when looking for the leader or members of the flock.")]
        private LayerMask blockVisionLayers;
        public LayerMask BlockVisionLayers
        {
            get => blockVisionLayers;
            set => blockVisionLayers = value;
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
        private float next;

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

            if (flockingLeader == null)
            {
#if !UNITY_EDITOR
                FlockingLeader = null; // Remove fake Unity null.
#endif
                return Vector3.zero;
            }

            if (!flockingLeader.isActiveAndEnabled)
                return Vector3.zero;

            Rigidbody rigidbody = Rigidbody;
            Vector3 position = rigidbody.position;
            Vector3 leaderPosition = flockingLeader.Rigidbody.position;
            Vector3 direction = position - leaderPosition;
            if (direction.magnitude < leaderStoppingDistance)
                return direction.normalized;

            Span<EntityInfo> entities = flockingLeader.GetEntitiesInRange(rigidbody, flockingRange, blockVisionLayers);

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
            cohesion = ((cohesion / entities.Length) - position).normalized;

            Vector3 leader = (leaderPosition - position).normalized * leaderWeight;

            PathFollower pathFollower = PathFollower;
            // Check if we can use pathfinding.
            if (pathFollower.NavigationSurface != null)
            {
                // Check if leader is inside flocking range.
                if (Vector3.Distance(position, leaderPosition) <= flockingRange)
                    goto skipPathfinding;

                // Check if there are no obtacles between follower and leader.
                if (!Physics.Linecast(position, leaderPosition, BlockVisionLayers))
                    goto skipPathfinding;

                if (pathFollower.IsCalculatingPath)
                {
                    if (pathFollower.HasPath)
                        goto hasPath;
                    goto skipPathfinding;
                }
                else if (pathFollower.HasPath)
                {
                    // Check if distance from current path's destination to current leader's position is outside the allowed magin error.
                    if (Vector3.Distance(pathFollower.Destination, leaderPosition) > pathFollower.StoppingDistance)
                        pathFollower.SetDestination(leaderPosition);
                    goto hasPath;
                }
                else
                    goto hasNoPath;

            hasPath:
                float time = Time.fixedTime;
                if (next < time)
                {
                    next = time + PathRecalculationCooldown;
                    if (!pathFollower.IsCalculatingPath && Vector3.Distance(pathFollower.NextPosition, position) > pathFollower.StoppingDistance * 2)
                        pathFollower.SetDestination(leaderPosition);
                }

                Vector3 path = pathFollower.GetDirection() * pathStrength;
                return (((separation + alineation + cohesion).normalized * .2f) + path).normalized;

            hasNoPath:;
                pathFollower.SetDestination(leaderPosition);
                next = Time.fixedTime + PathRecalculationCooldown;
            }

            skipPathfinding:
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