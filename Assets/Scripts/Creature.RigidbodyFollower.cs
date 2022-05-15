using Enderlook.Unity.Pathfinding.Steerings;

using UnityEngine;

namespace Game
{
    public abstract partial class Creature
    {
        private sealed class RigidbodyFollower : ISteeringBehaviour
        {
            private readonly Rigidbody rigidbody;
            private Rigidbody otherRigidbody;

            public RigidbodyFollower(Rigidbody rigidbody) => this.rigidbody = rigidbody;

            public Vector3 GetDirection() => otherRigidbody.position - rigidbody.position;

#if UNITY_EDITOR
            public void DrawGizmos()
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(rigidbody.position, otherRigidbody.position);
            }
#endif

            public bool Follow(Creature enemy)
            {
                Rigidbody rigidbody = enemy.Rigidbody;
                if (otherRigidbody != rigidbody)
                {
                    otherRigidbody = rigidbody;
                    return true;
                }
                return false;
            }

            public void Unfollow() => otherRigidbody = null;
        }
    }
}