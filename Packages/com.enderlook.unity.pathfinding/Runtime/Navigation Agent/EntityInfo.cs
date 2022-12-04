using Enderlook.Unity.Pathfinding.Steerings;

using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal readonly struct EntityInfo
    {
        public readonly Vector3 Position;
        public readonly Vector3 RigidbodyMinusEntity;
        public readonly Vector3 ForwardFactor;
        public readonly float Distance;
        public readonly float DistanceFactor;
#if UNITY_EDITOR
        public readonly Object Entity;
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityInfo(Vector3 position, Vector3 forwardFactor, Vector3 rigidbodyMinusEntity, float distance, float distanceFactor
#if UNITY_EDITOR
            , Object entity
#endif
            )
        {
            Position = position;
            ForwardFactor = forwardFactor;
            RigidbodyMinusEntity = rigidbodyMinusEntity;
            Distance = distance;
            DistanceFactor = distanceFactor;
#if UNITY_EDITOR
            Entity = entity;
#endif
        }
    }
}