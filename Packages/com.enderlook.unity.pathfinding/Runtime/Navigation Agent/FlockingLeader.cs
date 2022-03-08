﻿using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Unity.Pathfinding.Steerings;
using Enderlook.Unity.Threading;

using System;
using System.Runtime.CompilerServices;

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

            if (followersPositions.Length >= followers.Count)
                followersPositions[followers.Count - 1] = follower.Rigidbody.position;
            else
                AddFollower_Slow(follower);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void AddFollower_Slow(FlockingFollower follower)
        {
            // We resize the array with Capacity instead of Count in order to reduce reallocation amounts.
            Vector3[] newFollowersPositions = new Vector3[followers.Capacity];
            Array.Copy(followersPositions, newFollowersPositions, followersPositions.Length);
            followersPositions = newFollowersPositions;
            followersPositions[followers.Count - 1] = follower.Rigidbody.position;
        }

        internal void RemoveFollower(FlockingFollower follower) => followers.Remove(follower.Rigidbody);

        internal Span<EntityInfo> GetEntitiesInRange(Rigidbody rigibody, float range)
        {
            if (lastUpdate != fixedUpdateCount)
                GetEntitiesInRange_Slow();

            followersInRange.Clear();
            Vector3 currentPosition = rigibody.position;
            for (int i = 0; i < followers.Count; i++)
                GetEntitiesInRange_Check(followersPositions[i], followers[i].transform, range, currentPosition);

            GetEntitiesInRange_Check(Rigidbody.position, transform, range, currentPosition);

            return followersInRange.AsSpan();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GetEntitiesInRange_Check(Vector3 position, Transform transform, float range, Vector3 currentPosition)
        {
            Vector3 rigidbodyMinusEntity = currentPosition - position;
            float distance = rigidbodyMinusEntity.magnitude;
            float distanceFactor = (range - distance) / range;
            Vector3 forwardFactor = transform.forward * distanceFactor;
            if (distance <= range)
                followersInRange.Add(new EntityInfo(position, forwardFactor, rigidbodyMinusEntity, distance, distanceFactor));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void GetEntitiesInRange_Slow()
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