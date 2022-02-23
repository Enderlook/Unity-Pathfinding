using Enderlook.Unity.Pathfinding.Utils;
using Enderlook.Unity.Toolset.Attributes;

using System.Collections.Generic;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Navigation agent movement system.
    /// </summary>
    [AddComponentMenu("Enderlook/Pathfinding/Navigation Agent Rigidbody"), RequireComponent(typeof(Rigidbody)), DisallowMultipleComponent, DefaultExecutionOrder(ExecutionOrder.NavigationAgent)]
    public sealed class NavigationAgentRigidbody : MonoBehaviour
    {
        [field: Header("Features")]
        [field: IsProperty, SerializeField, Tooltip("Determines if the agent has control over the rigidbody velocity.")]
        public bool UpdateVelocity { get; set; } = true;

        [field: IsProperty, SerializeField, Tooltip("Determines if the agent has control over the rigidbody rotation.")]
        public bool UpdateRotation { get; set; } = true;

        [SerializeField, Tooltip("Steering behaviours that determines the movement of this agent.")]
        private List<SteeringBehaviour> steeringBehaviours;

        [Header("Movement Configuration")]
        [SerializeField, Min(0), Tooltip("Determines the maximum speed of the agent.")]
        private float linealSpeed;
        public float LinealSpeed {
            get => linealSpeed;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                linealSpeed = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the acceleration of the lineal speed.")]
        private float linealAcceleration;
        public float LinealAcceleration {
            get => linealAcceleration;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                linealAcceleration = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the deacceleration of the lineal speed during braking.")]
        private float linearBrackingSpeed;
        public float LinearBrackingSpeed {
            get => linearBrackingSpeed;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                linearBrackingSpeed = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the turning speed.")]
        private float angularSpeed;
        public float AngularSpeed {
            get => angularSpeed;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                angularSpeed = value;
            }
        }

        private new Rigidbody rigidbody;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void Awake()
        {
            rigidbody = GetComponent<Rigidbody>();
            rigidbody.constraints |= RigidbodyConstraints.FreezeRotation;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void FixedUpdate()
        {
            if (!UpdateVelocity && !UpdateRotation)
                return;

            Vector3 direction = Vector3.zero;
            foreach (SteeringBehaviour behaviour in steeringBehaviours)
                direction += behaviour.Behaviour.GetDirection() * behaviour.Strength;

            direction.y = 0;

            if (direction.sqrMagnitude > 1)
                direction = direction.normalized;

            if (UpdateVelocity)
            {
                Vector3 targetVelocity = direction * linealSpeed;
                Vector3 currentVelocity = rigidbody.velocity;
                if (targetVelocity.sqrMagnitude > currentVelocity.sqrMagnitude)
                    rigidbody.velocity = Vector3.MoveTowards(currentVelocity, targetVelocity, linealAcceleration * Time.fixedDeltaTime);
                else
                    rigidbody.velocity = Vector3.MoveTowards(currentVelocity, targetVelocity, linearBrackingSpeed * Time.fixedDeltaTime);
            }

            if (UpdateRotation && direction != Vector3.zero)
                rigidbody.rotation = Quaternion.RotateTowards(rigidbody.rotation, Quaternion.LookRotation(direction), angularSpeed * Time.fixedDeltaTime);
        }

#if UNITY_EDITOR
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnDrawGizmosSelected()
        {
            Vector3 direction = Vector3.zero;
            foreach (SteeringBehaviour behaviour in steeringBehaviours)
                direction += behaviour.Behaviour.GetDirection() * behaviour.Strength;

            direction.y = 0;

            Gizmos.color = Color.white;
            Vector3 position = transform.position;
            Gizmos.DrawLine(position, position + (direction.normalized * 3));
        }
#endif
    }
}
