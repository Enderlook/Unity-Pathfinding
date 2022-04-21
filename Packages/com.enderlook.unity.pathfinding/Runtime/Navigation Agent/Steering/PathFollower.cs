using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Unity.Pathfinding.Utils;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Steerings
{
    /// <summary>
    /// An steering behavour to follow a path.
    /// </summary>
    [AddComponentMenu("Enderlook/Pathfinding/Path Follower"), RequireComponent(typeof(Rigidbody))]
    public sealed class PathFollower : MonoBehaviour, ISteeringBehaviour
    {
        [SerializeField, Tooltip("Navigation surface used to calculate path when requested.\n" +
            "If this value is null, the agent will try look for an already instantiate surface.\n" +
            "You don't need this if you plan to manually set the path.")]
        private NavigationSurface navigationSurface;

        public NavigationSurface NavigationSurface
        {
            get
            {
                if (navigationSurface == null)
                    navigationSurface = FindObjectOfType<NavigationSurface>();
                return navigationSurface;
            }
            set => navigationSurface = value;
        }

        [SerializeField, Min(0), Tooltip("Determines the minimal distance from the target to consider it as reached.")]
        private float stoppingDistance;
        public float StoppingDistance {
            get => stoppingDistance;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                stoppingDistance = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the minimal distance from a point in path to consider it as reached and go for the next point.")]
        private float nextPointDistance;
        public float NextPointDistance {
            get => nextPointDistance;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                nextPointDistance = value;
            }
        }

        [SerializeField, Tooltip("Determines if the follower should attempt to acquire a new path if the existing path becomes invalid.")]
        private bool autoRepath = true;
        public bool AutoRepath {
            get => autoRepath;
            set => autoRepath = value;
        }

        /// <summary>
        /// Whenever it contains a path.
        /// </summary>
        public bool HasPath
        {
            get {
            start:
                Path<Vector3> path_ = path;
                if (path_?.IsCompleted ?? false)
                {
                    path = null;
                    SetPath(path_);
                    path_.SendToPool();

                    if (queuedDestinationToSet is Vector3 queuedDestination)
                    {
                        queuedDestinationToSet = null;
                        SetDestination(queuedDestination);
                        // We could remove this, but this is safer in case we make SetDestinationi synchronous by default.
                        goto start;
                    }
                }
                return !enumerator.IsDefault;
            }
        }

        /// <summary>
        /// Whenever a path is being calculated at this moment.
        /// </summary>
        public bool IsCalculatingPath
        {
            get {
            start:
                Path<Vector3> path_ = path;
                if (!(path_ is null))
                {
                    if (!path_.IsCompleted)
                        return true;
                    else
                    {
                        path = null;
                        SetPath(path_);
                        path_.SendToPool();

                        if (queuedDestinationToSet is Vector3 queuedDestination)
                        {
                            queuedDestinationToSet = null;
                            SetDestination(queuedDestination);
                            // We could return true, but this is safer in case we make SetDestination synchronous by default.
                            goto start;
                        }
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Current point that this follower want to reach from its path.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="HasPath"/> is <see langword="false"/>.</exception>
        public Vector3 NextPosition {
            get {
                if (!HasPath)
                    ThrowInvalidOperationException_DoesNotHavePath();
                return enumerator.Current;
            }
        }

        /// <summary>
        /// Destination of this follower's path.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="HasPath"/> is <see langword="false"/>.</exception>
        public Vector3 Destination
        {
            get
            {
                if (!HasPath)
                    ThrowInvalidOperationException_DoesNotHavePath();
                return innerPath[innerPath.Count - 1];
            }
        }

        private new Rigidbody rigidbody;
        private Path<Vector3> path;
        private RawPooledList<Vector3> innerPath;
        private RawPooledList<Vector3>.Enumerator enumerator;
        private Vector3? queuedDestinationToSet;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void Awake() => rigidbody = GetComponent<Rigidbody>();

        /// <summary>
        /// Set the path to follow.<br/>
        /// This method cancel any other path that is being calculated, if any.
        /// </summary>
        /// <param name="path">Path to follow.</param>
        public void SetPath(Span<Vector3> path)
        {
            Cancel();
            if (innerPath.IsDefault)
                innerPath = RawPooledList<Vector3>.Create();
            innerPath.Clear();
            innerPath.AddRange(path);
            if (innerPath.Count > 0)
            {
                RawPooledList<Vector3>.Enumerator enumerator_ = innerPath.GetEnumerator();
                if (enumerator_.MoveNext())
                {
                    enumerator = enumerator_;
                    // Compute path to remove closed nodes to current position.
                    GetDirection<Toggle.No>();
                    return;
                }
            }
            enumerator = default;
        }

        /// <summary>
        /// Set the path to follow.<br/>
        /// The enumerator must not be endless.<br/>
        /// This method cancel any other path that is being calculated, if any.
        /// </summary>
        /// <param name="path">Path to follow.</param>
        public void SetPath(IEnumerable<Vector3> path)
        {
            Cancel();
            if (innerPath.IsDefault)
                innerPath = RawPooledList<Vector3>.Create();
            innerPath.Clear();
            innerPath.AddRange(path);
            if (innerPath.Count > 0)
            {
                RawPooledList<Vector3>.Enumerator enumerator_ = innerPath.GetEnumerator();
                if (enumerator_.MoveNext())
                {
                    enumerator = enumerator_;
                    // Compute path to remove close points to current position.
                    GetDirection<Toggle.No>();
                    return;
                }
            }
            enumerator = default;
        }

        /// <summary>
        /// Set the path to follow.<br/>
        /// This method does not take ownership of the <paramref name="path"/> instance.<br/>
        /// This method cancel any other path that is being calculated, if any.
        /// </summary>
        /// <param name="path">Path to follow.</param>
        public void SetPath(Path<Vector3> path) => SetPath(path.AsSpan);

        /// <summary>
        /// Clears current path.<br/>
        /// To clear path being calculated and current path, execute <see cref="Cancel"/> followed by <see cref="Clear"/>.
        /// </summary>
        public void Clear() => enumerator = default;

        /// <summary>
        /// Cancels the path that is currently being calculated, if any.<br/>
        /// To clear path being calculated and current path, execute <see cref="Cancel"/> followed by <see cref="Clear"/>.
        /// </summary>
        /// <returns><see langword="true"/> if there was a path calculation. <see langword="false"/> if not path was being calculated.</returns>
        public bool Cancel()
        {
            queuedDestinationToSet = null;
            Path<Vector3> path_ = path;
            if (!(path is null))
            {
                path_.SendToPool();
                path = null;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Set the target destination of this agent.<br/>
        /// If a previous destination was being calcualted, it's canceled.<br/>
        /// To prevent moving using the old path while the new path is being calculated do:<br/>
        /// <list type="bullet">
        /// <item>Execute <see cref="Cancel"/> to remove any possible path that is being calculated.</item>
        /// <item>Then execute <see cref="Clear"/> to remove current path.</item>
        /// <item>Finally execute this method to set the new path.</item>
        /// </list>
        /// This method cancels paths that are being calculated when it's called, however we still need to call <see cref="Cancel"/> because path may be calculated asynchronously and so it may finish between the call <see cref="Clear"/> and <see cref="SetDestination(Vector3, bool)"/>.
        /// </summary>
        /// <param name="destination">Destination to follow.</param>
        /// <param name="synchronous">If <see langword="true"/>, path calculation will be forced to execute immediately.</param>
        public void SetDestination(Vector3 destination, bool synchronous = false)
        {
            queuedDestinationToSet = null;

            Path<Vector3> path_ = path;
            if (!(path_ is null))
            {
                if (path_.IsCompleted)
                    goto next;
                else
                    path.SendToPool();
            }
            path_ = Path<Vector3>.Rent();

            next:
            NavigationSurface.CalculatePath(path_, rigidbody.position, destination, synchronous);
            if (path_.IsCompleted)
            {
                path = null;
                SetPath(path_);
            }
            else
                path = path_;
        }

        internal void EnqueueDestination(Vector3 destination)
        {
            if (!IsCalculatingPath)
                SetDestination(destination);
            queuedDestinationToSet = destination;
        }

        /// <inheritdoc cref="ISteeringBehaviour.GetDirection()"/>
        Vector3 ISteeringBehaviour.GetDirection() => GetDirection<Toggle.Yes>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector3 GetDirection() => GetDirection<Toggle.Yes>();

        private Vector3 GetDirection<TAllowRepath>()
        {
            Vector3 direction;
            if (!HasPath)
                return Vector3.zero;

            NavigationSurface navigationSurface = NavigationSurface;
            RawPooledList<Vector3>.Enumerator enumerator_ = enumerator;
            Vector3 position = rigidbody.position;
        start:
            Vector3 current = enumerator_.Current;
            current.y = position.y;

            if (!navigationSurface.HasLineOfSight(current, position))
            {
                if (Toggle.IsToggled<TAllowRepath>())
                    EnqueueDestination(Destination);
            }
            else if (Toggle.IsToggled<TAllowRepath>())
                Cancel();

            direction = current - position;
            float distance = direction.magnitude;
            RawPooledList<Vector3>.Enumerator enumerator__ = enumerator_;
            bool hasNext = enumerator_.MoveNext();
            if (distance <= (hasNext ? nextPointDistance : stoppingDistance))
            {
                if (!hasNext)
                    enumerator_ = default;
                else
                    goto start;
            }
            else
            {
                direction = direction.normalized;
                enumerator_ = enumerator__;
            }

            enumerator = enumerator_;
            return direction;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnDestroy()
        {
            innerPath.Dispose();
            enumerator.Dispose();
        }

        private static void ThrowInvalidOperationException_DoesNotHavePath() => throw new InvalidOperationException("Doesn't have a path.");

#if UNITY_EDITOR
        void ISteeringBehaviour.DrawGizmos()
        {
            if (!Application.isPlaying)
                return;

            Vector3 direction = GetDirection<Toggle.Yes>();
            Vector3 position = rigidbody.position;
            Gizmos.color = Color.gray;
            Gizmos.DrawLine(position, position + (direction * 3));

            RawPooledList<Vector3>.Enumerator enumerator = this.enumerator;
            if (enumerator.IsDefault)
                return;

            Gizmos.color = Color.black;
            Vector3 start;
            Vector3 end = transform.position;
            do
            {
                Gizmos.DrawWireCube(end, Vector3.one * .1f);
                start = end;
                end = enumerator.Current;
                Gizmos.DrawLine(start, end);
            } while (enumerator.MoveNext());
            Gizmos.DrawWireCube(end, Vector3.one * .1f);
        }
#endif
    }
}
