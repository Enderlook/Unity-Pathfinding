﻿using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Unity.Pathfinding.Steerings;
using Enderlook.Unity.Threading;

using System;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [AddComponentMenu("Enderlook/Pathfinding/Flocking Leader"), RequireComponent(typeof(Rigidbody)), DefaultExecutionOrder(ExecutionOrder.NavigationAgent)]
    public sealed class FlockingLeader : MonoBehaviour
    {
        private static int fixedUpdateCount;

        private RawPooledList<Rigidbody> followers = RawPooledList<Rigidbody>.Create();

        private Vector3[] followersPositions = Array.Empty<Vector3>();

        // We take advantage of Unity single threading to temporarily store in the same array the closest entites to the requested and so reduce allocations.
        private RawPooledList<EntityInfo> followersInRange = RawPooledList<EntityInfo>.Create();

        internal Rigidbody Rigidbody { get; private set; }

        private int lastUpdate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Initialize()
        {
            if (fixedUpdateCount == 0)
            {
                fixedUpdateCount = 1;
                UnityThread.OnFixedUpdate += () => fixedUpdateCount++;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void Awake() => Rigidbody = GetComponent<Rigidbody>();

        internal void AddFollower(FlockingFollower follower)
        {
            followers.Add(follower.Rigidbody);

            if (followersPositions.Length < followers.Count)
                Slow();
            else
                followersPositions[followers.Count - 1] = follower.Rigidbody.position;

            void Slow()
            {
                // We resize the array with Capacity instead of Count in order to reduce reallocation amounts.
                Vector3[] newFollowersPositions = new Vector3[followers.Capacity];
                Array.Copy(followersPositions, newFollowersPositions, followersPositions.Length);
                followersPositions = newFollowersPositions;
                followersPositions[followers.Count - 1] = follower.Rigidbody.position;
            }
        }

        internal void RemoveFollower(FlockingFollower follower) => followers.Remove(follower.Rigidbody);

        internal Span<EntityInfo> GetEntitiesInRange(Rigidbody rigibody, float range)
        {
            if (lastUpdate != fixedUpdateCount)
                Slow();

            followersInRange.Clear();
            Vector3 currentPosition = rigibody.position;
            for (int i = 0; i < followers.Count; i++)
                Check(followersPositions[i], followers[i].transform);

            Check(Rigidbody.position, transform);

            return followersInRange.AsSpan();

            void Check(Vector3 position, Transform transform)
            {
                Vector3 rigidbodyMinusEntity = currentPosition - position;
                float distance = rigidbodyMinusEntity.magnitude;
                float distanceFactor = (range - distance) / range;
                Vector3 forwardFactor = transform.forward * distanceFactor;
                if (distance <= range)
                    followersInRange.Add(new EntityInfo(position, forwardFactor, rigidbodyMinusEntity, distance, distanceFactor));
            }

            void Slow()
            {
                lastUpdate = fixedUpdateCount;
                if (followersPositions.Length < followers.Count)
                    // We resize the array with Capacity instead of Count in order to reduce reallocation amounts.
                    followersPositions = new Vector3[followers.Capacity];

                for (int i = 0; i < followers.Count; i++)
                    followersPositions[i] = followers[i].position;
            }
        }
    }

    internal readonly struct EntityInfo
    {
        public readonly Vector3 Position;
        public readonly Vector3 RigidbodyMinusEntity;
        public readonly Vector3 ForwardFactor;
        public readonly float Distance;
        public readonly float DistanceFactor;

        public EntityInfo(Vector3 position, Vector3 forwardFactor, Vector3 rigidbodyMinusEntity, float distance, float distanceFactor)
        {
            Position = position;
            ForwardFactor = forwardFactor;
            RigidbodyMinusEntity = rigidbodyMinusEntity;
            Distance = distance;
            DistanceFactor = distanceFactor;
        }
    }
}