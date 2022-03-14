using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Unity.Pathfinding.Utils;

using System;
using System.Collections.Generic;

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

        public bool HasPath => !EqualityComparer<RawPooledList<Vector3>.Enumerator>.Default.Equals(enumerator, default);

        public bool IsCalculatingPath => !(path?.IsCompleted ?? true);

        public Vector3 NextPosition {
            get {
                if (!HasPath)
                    ThrowInvalidOperationException_DoesNotHavePath();
                return enumerator.Current;
            }
        }

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
#if UNITY_EDITOR
        internal RawPooledList<Vector3>.Enumerator previousEnumerator;
        private bool isPending;
#endif

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void Awake() => rigidbody = GetComponent<Rigidbody>();

        /// <summary>
        /// Set the path to follow.
        /// </summary>
        /// <param name="path">Path to follow.</param>
        public void SetPath(Span<Vector3> path)
        {
            if (innerPath.IsDefault)
                innerPath = RawPooledList<Vector3>.Create();
            innerPath.Clear();
            innerPath.AddRange(path);
            if (innerPath.Count > 0)
            {
                enumerator = innerPath.GetEnumerator();
                if (!enumerator.MoveNext())
                    enumerator = default;
            }
            else
                enumerator = default;
#if UNITY_EDITOR
            previousEnumerator = enumerator;
#endif
        }

        /// <summary>
        /// Set the path to follow.<br/>
        /// The enumerator must not be endless.
        /// </summary>
        /// <param name="path">Path to follow.</param>
        public void SetPath(IEnumerable<Vector3> path)
        {
            if (innerPath.IsDefault)
                innerPath = RawPooledList<Vector3>.Create();
            innerPath.Clear();
            innerPath.AddRange(path);
            if (innerPath.Count > 0)
            {
                enumerator = innerPath.GetEnumerator();
                if (!enumerator.MoveNext())
                    enumerator = default;
            }
            else
                enumerator = default;
#if UNITY_EDITOR
            previousEnumerator = enumerator;
#endif
        }

        /// <summary>
        /// Set the path to follow.<br/>
        /// This method does not take ownership of the <paramref name="path"/> instance.
        /// </summary>
        /// <param name="path">Path to follow.</param>
        public void SetPath(Path<Vector3> path) => SetPath(path.AsSpan);

        /// <summary>
        /// Clears current path.
        /// </summary>
        public void Clear()
        {
            enumerator = default;
#if UNITY_EDITOR
            previousEnumerator = default;
#endif
        }

        /// <summary>
        /// Set the target destination of this agent.
        /// </summary>
        /// <param name="destination">Destination to follow.</param>
        /// <param name="synchronous">If <see langword="true"/>, path calculation will be forced to execute immediately.</param>
        public void SetDestination(Vector3 destination, bool synchronous = false)
        {
            if (!(path is null))
            {
                if (path.IsCompleted)
                    goto next;
                else
                    path.SendToPool();
            }
            path = Path<Vector3>.Rent();

            next:
            NavigationSurface.CalculatePath(path, rigidbody.position, destination, synchronous);
            if (path.IsCompleted)
                SetPath(path);
            else
                isPending = true;
        }

        /// <inheritdoc cref="ISteeringBehaviour.GetDirection()"/>
        Vector3 ISteeringBehaviour.GetDirection() => GetDirection();

        internal Vector3 GetDirection()
        {
            if (isPending && path.IsCompleted)
            {
                isPending = false;
                SetPath(path);
                path.SendToPool();
            }

            if (!HasPath)
                return Vector3.zero;

            start:
            Vector3 current = enumerator.Current;
            Vector3 position = rigidbody.position;
            current.y = position.y;

            Vector3 direction = current - position;
            float distance = direction.magnitude;
            if (distance <= stoppingDistance)
            {
#if UNITY_EDITOR
                previousEnumerator = enumerator;
#endif
                if (!enumerator.MoveNext())
                {
                    enumerator = default;
                    return Vector3.zero;
                }
                goto start;
            }

            return direction.normalized;
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

            Vector3 direction = GetDirection();
            Vector3 position = rigidbody.position;
            Gizmos.color = Color.gray;
            Gizmos.DrawLine(position, position + (direction * 3));

            RawPooledList<Vector3>.Enumerator enumerator = previousEnumerator;
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
