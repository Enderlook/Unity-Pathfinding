﻿using Enderlook.Unity.Pathfinding;

using System;

using UnityEngine;

namespace Assets.Enderlook.Unity.Pathfinding.Steerings
{
    /// <summary>
    /// An steering behavour to follow a path.
    /// </summary>
    [Serializable]
    public struct PathFollower : ISteering
    {
        [SerializeField, Min(0), Tooltip("Determines the minimal distance from the target to consider it as reached.")]
        private float stoppingDistance;
        public float StoppingDistance {
            get => stoppingDistance;
            set => stoppingDistance = ErrorMessage.NoNegativeGuard(nameof(StoppingDistance), value);
        }

        public bool HasPath { get; private set; }

        public Vector3 NextPosition {
            get {
                if (!HasPath)
                {
                    Debug.Assert(enumerator.IsDefault);
                    throw new InvalidOperationException("Doesn't have a path.");
                }
                Debug.Assert(!enumerator.IsDefault);
                return enumerator.Current;
            }
        }

        private DynamicArray<Vector3> innerPath;
        internal DynamicArray<Vector3>.Enumerator enumerator;

        /// <summary>
        /// Set the path to follow.
        /// </summary>
        /// <param name="path">Path to follow.</param>
        public void SetPath(Span<Vector3> path)
        {
            if (innerPath.IsDefault)
                innerPath = DynamicArray<Vector3>.Create();
            innerPath.Clear();
            innerPath.AddRange(path);
            enumerator = innerPath.GetEnumerator();
            enumerator.MoveNext();
            HasPath = true;
        }

        /// <inheritdoc cref="SetPath(Span{Vector3})"/>
        public void SetPath(Path<Vector3> path) => SetPath(path.AsSpan);

        /// <inheritdoc cref="ISteering.GetDirection(Rigidbody)"/>
        internal Vector3 GetDirection(Rigidbody agent)
        {
            if (!HasPath)
                return Vector3.zero;

            Vector3 current = enumerator.Current;
            current.y = agent.position.y;

            Vector3 direction = current - agent.position;
            float distance = direction.magnitude;
            if (distance <= stoppingDistance)
            {
                if (!enumerator.MoveNext())
                {
                    HasPath = false;
                    return Vector3.zero;
                }
            }

            return direction.normalized;
        }

        /// <inheritdoc cref="ISteering.GetDirection(Rigidbody)"/>
        Vector3 ISteering.GetDirection(Rigidbody agent) => GetDirection(agent);
    }
}