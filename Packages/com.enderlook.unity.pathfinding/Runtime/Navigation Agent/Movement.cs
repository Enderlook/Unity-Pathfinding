using Enderlook.Unity.Pathfinding.Utils;

using System;

using UnityEngine;

namespace Assets.Enderlook.Unity.Pathfinding
{
    [Serializable]
    public struct Movement
    {
        [SerializeField, Tooltip("Determines if the agent has control over the rigidbody.")]
        public bool IsStopped;

        [Header("Main Configuration")]
        [SerializeField, Min(0), Tooltip("Determines the maximum speed of the agent while following a path.")]
        private float linealSpeed;
        public float LinealSpeed {
            get => linealSpeed;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                linealSpeed = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the acceleration of the lineal speed while following a path.")]
        private float linealAcceleration;
        public float LinealAcceleration {
            get => linealAcceleration;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                linealAcceleration = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the turning speed while following a path.")]
        private float angularSpeed;
        public float AngularSpeed {
            get => angularSpeed;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                angularSpeed = value;
            }
        }

        internal void Initialize(Rigidbody rigidbody) => rigidbody.constraints |= RigidbodyConstraints.FreezeRotation;

        internal void MoveAndRotate(Rigidbody rigidbody, Vector3 direction)
        {
            if (IsStopped)
                return;

            direction.y = 0;

            if (direction.magnitude > 1)
                direction = direction.normalized;

            Vector3 targetSpeed = direction * linealSpeed;
            rigidbody.velocity = Vector3.MoveTowards(rigidbody.velocity, targetSpeed, linealAcceleration * Time.fixedDeltaTime);

            if (direction == Vector3.zero)
                return;

            rigidbody.rotation = Quaternion.RotateTowards(rigidbody.rotation, Quaternion.LookRotation(direction), angularSpeed * Time.fixedDeltaTime);
        }
    }
}
