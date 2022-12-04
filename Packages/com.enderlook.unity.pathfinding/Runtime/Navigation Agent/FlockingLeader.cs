using Enderlook.Collections.Pooled.LowLevel;
using Enderlook.Unity.Pathfinding.Steerings;
using Enderlook.Unity.Threading;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

using UnityEngine;

using UnityObject = UnityEngine.Object;

namespace Enderlook.Unity.Pathfinding
{
    [AddComponentMenu("Enderlook/Pathfinding/Flocking Leader"), RequireComponent(typeof(Rigidbody)), DefaultExecutionOrder(ExecutionOrder.NavigationAgent)]
    public sealed class FlockingLeader : MonoBehaviour
#if UNITY_EDITOR
        , ISteeringBehaviourEditor
#endif
    {
        private static int fixedUpdateCount;

#if UNITY_EDITOR
        internal RawPooledList<FlockingFollower> followers = RawPooledList<FlockingFollower>.Create();
        private RawPooledList<FlockingFollower> toRemove = RawPooledList<FlockingFollower>.Create();
#else
        private RawPooledList<Rigidbody> followers = RawPooledList<Rigidbody>.Create();
        private RawPooledList<Rigidbody> toRemove = RawPooledList<Rigidbody>.Create();
#endif

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
#if UNITY_EDITOR
            followers.Add(follower);
#else
            followers.Add(follower.Rigidbody);
#endif

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

                if (followersPositions.Length >= followers.Count)
                {
                    followersPositions[followers.Count - 1] = follower.Rigidbody.position;
                    return;
                }
            }

            // We resize the array with Capacity instead of Count in order to reduce reallocation amounts.
            ArrayPool<Vector3> pool = ArrayPool<Vector3>.Shared;
            Vector3[] newFollowersPositions = pool.Rent(followers.Capacity);
            Array.Copy(followersPositions, newFollowersPositions, followersPositions.Length);
            pool.Return(followersPositions);
            followersPositions = newFollowersPositions;
            followersPositions[followers.Count - 1] = follower.Rigidbody.position;
        }

        internal void RemoveFollower(FlockingFollower follower)
        {
#if UNITY_EDITOR
            toRemove.Add(follower);
#else
            toRemove.Add(follower.Rigidbody);
#endif
        }

        internal Span<EntityInfo> GetEntitiesInRange(Rigidbody rigibody, float range, LayerMask blockVisionLayers)
        {
            // Note: The result of this method becomes undefined on the next method call or next fixed frame.

            if (lastUpdate != fixedUpdateCount)
                GetEntitiesInRange_Slow();

            followersInRange.Clear();
            Vector3 currentPosition = rigibody.position;
#if UNITY_EDITOR
            Span<FlockingFollower> followers = this.followers.AsSpan();
#else
            Span<Rigidbody> followers = this.followers.AsSpan();
#endif
            for (int i = 0; i < followers.Length; i++)
            {
#if UNITY_EDITOR
                FlockingFollower follower = followers[i];
#else
                Rigidbody follower = followers[i];
#endif
                GetEntitiesInRange_Check(followersPositions[i], follower.transform, range, currentPosition, blockVisionLayers
#if UNITY_EDITOR
                    , follower
#endif
                );
            }

            GetEntitiesInRange_Check(Rigidbody.position, transform, range, currentPosition, blockVisionLayers
#if UNITY_EDITOR
                , this
#endif
            );

            return followersInRange.AsSpan();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GetEntitiesInRange_Check(Vector3 position, Transform transform, float range, Vector3 currentPosition, LayerMask blockVisionLayers
#if UNITY_EDITOR
                , UnityObject entity
#endif
            )
        {
            Vector3 rigidbodyMinusEntity = currentPosition - position;
            float distance = rigidbodyMinusEntity.magnitude;
            float distanceFactor = (range - distance) / range;
            Vector3 forwardFactor = transform.forward * distanceFactor;
            if (distance <= range && !Physics.Linecast(currentPosition, position, blockVisionLayers))
                followersInRange.Add(new EntityInfo(position, forwardFactor, rigidbodyMinusEntity, distance, distanceFactor
#if UNITY_EDITOR
                , entity
#endif
                ));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void GetEntitiesInRange_Slow()
        {
            if (toRemove.Count > 0)
                Remove();

            lastUpdate = fixedUpdateCount;
            Vector3[] array = followersPositions;
#if UNITY_EDITOR
            Span<FlockingFollower> followers = this.followers.AsSpan();
#else
            Span<Rigidbody> followers = this.followers.AsSpan();
#endif
            if (array.Length < followers.Length)
            {
                // We resize the array with Capacity instead of Count in order to reduce reallocation amounts.
                ArrayPool<Vector3>.Shared.Return(array);
                followersPositions = array = ArrayPool<Vector3>.Shared.Rent(this.followers.Capacity);
            }

            for (int i = 0; i < followers.Length; i++)
            {
#if UNITY_EDITOR
                array[i] = followers[i].Rigidbody.position;
#else
                array[i] = followers[i].position;
#endif
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void Remove()
        {
            // This reduces time complexity of multiple removes by batching them.

#if UNITY_EDITOR
            Span<FlockingFollower> toRemove = this.toRemove.AsSpan();
#else
            Span<Rigidbody> toRemove = this.toRemove.AsSpan();
#endif
            int toRemoveCount = toRemove.Length;
            if (toRemoveCount == 1)
            {
                this.followers.Remove(toRemove[0]);
                this.toRemove.Clear();
                return;
            }

#if UNITY_EDITOR
            Span<FlockingFollower> followers = this.followers.AsSpan();
#else
            Span<Rigidbody> followers = this.followers.AsSpan();
#endif
            int followersCount = followers.Length;
            for (int i = 0; i < followersCount; i++)
            {
#if UNITY_EDITOR
                FlockingFollower element = followers[i];
#else
                Rigidbody element = followers[i];
#endif
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
#if UNITY_EDITOR
            this.followers = RawPooledList<FlockingFollower>.From(this.followers.UnderlyingArray, followersCount);
#else
            this.followers = RawPooledList<Rigidbody>.From(this.followers.UnderlyingArray, followersCount);
#endif
            this.toRemove.Clear();
        }


#if UNITY_EDITOR
        /// <inheritdoc cref="ISteeringBehaviourEditor.PrepareForGizmos"/>
        void ISteeringBehaviourEditor.PrepareForGizmos()
        {
            if (Rigidbody == null)
                Rigidbody = GetComponent<Rigidbody>();
        }
#endif
    }
}