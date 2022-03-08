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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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