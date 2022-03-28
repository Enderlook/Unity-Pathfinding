using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Unity.Pathfinding.Steerings;
using Enderlook.Unity.Threading;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [AddComponentMenu("Enderlook/Pathfinding/Flocking Leader"), RequireComponent(typeof(Rigidbody)), DefaultExecutionOrder(ExecutionOrder.NavigationAgent)]
    public sealed class FlockingLeader : MonoBehaviour
    {
        private static int fixedUpdateCount;

        private RawPooledList<Rigidbody> followers = RawPooledList<Rigidbody>.Create();
        private RawPooledList<Rigidbody> toRemove = RawPooledList<Rigidbody>.Create();

        private Vector3[] followersPositions = ArrayPool<Vector3>.Shared.Rent(0);

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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnDestroy()
        {
            followers.Clear();
            followers.Dispose();
            toRemove.Dispose();
            followersInRange.Dispose();
            ArrayPool<Vector3>.Shared.Return(followersPositions);
            followersPositions = null;
        }

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
            if (toRemove.Count > 0)
            {
                Remove();
                AddFollower(follower);
            }

            // We resize the array with Capacity instead of Count in order to reduce reallocation amounts.
            Vector3[] newFollowersPositions = new Vector3[followers.Capacity];
            Array.Copy(followersPositions, newFollowersPositions, followersPositions.Length);
            followersPositions = newFollowersPositions;
            followersPositions[followers.Count - 1] = follower.Rigidbody.position;
        }

        internal void RemoveFollower(FlockingFollower follower) => toRemove.Add(follower.Rigidbody);

        internal Span<EntityInfo> GetEntitiesInRange(Rigidbody rigibody, float range)
        {
            // Note: The result of this method becomes undefined on the next method call or next fixed frame.

            if (lastUpdate != fixedUpdateCount)
                GetEntitiesInRange_Slow();

            followersInRange.Clear();
            Vector3 currentPosition = rigibody.position;
            Span<Rigidbody> followers = this.followers.AsSpan();
            for (int i = 0; i < followers.Length; i++)
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
            if (toRemove.Count > 0)
                Remove();

            lastUpdate = fixedUpdateCount;
            Vector3[] array = followersPositions;
            Span<Rigidbody> followers = this.followers.AsSpan();
            if (array.Length < followers.Length)
            {
                // We resize the array with Capacity instead of Count in order to reduce reallocation amounts.
                ArrayPool<Vector3>.Shared.Return(array);
                followersPositions = array = ArrayPool<Vector3>.Shared.Rent(this.followers.Capacity);
            }

            for (int i = 0; i < followers.Length; i++)
                array[i] = followers[i].position;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Remove()
        {
            // This reduces time complexity of multiple removes by batching them.

            Span<Rigidbody> toRemove = this.toRemove.AsSpan();
            int toRemoveCount = toRemove.Length;
            if (toRemoveCount == 1)
            {
                this.followers.Remove(toRemove[0]);
                this.toRemove.Clear();
                return;
            }

            Span<Rigidbody> followers = this.followers.AsSpan();
            int followersCount = followers.Length;
            for (int i = 0; i < followersCount; i++)
            {
                Rigidbody element = followers[i];
                for (int k = 0; k < toRemoveCount; k++)
                {
                    if (element == toRemove[k])
                    {
                        followers[i] = followers[--followersCount];
                        if (toRemoveCount > 1)
                        {
                            toRemove[k] = toRemove[--toRemoveCount];
                            goto continue_;
                        }
                        else
                            goto double_break;
                    }
                }
            continue_:;
            }

        double_break:
            followers.Slice(followersCount).Clear();
            this.followers = RawPooledList<Rigidbody>.From(this.followers.UnderlyingArray, followersCount);
            this.toRemove.Clear();
        }
    }
}