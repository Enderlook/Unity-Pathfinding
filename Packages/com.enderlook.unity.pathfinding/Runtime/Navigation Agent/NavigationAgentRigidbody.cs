using Enderlook.Unity.Pathfinding.Steerings;
using Enderlook.Unity.Pathfinding.Utils;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Navigation agent movement system.
    /// </summary>
    [AddComponentMenu("Enderlook/Pathfinding/Navigation Agent Rigidbody"), RequireComponent(typeof(Rigidbody)), DisallowMultipleComponent, DefaultExecutionOrder(ExecutionOrder.NavigationAgent)]
    public sealed class NavigationAgentRigidbody : MonoBehaviour
    {
        [SerializeField, Tooltip("Steering behaviours that determines the movement of this agent.")]
        private SteeringBehaviour[] steeringBehaviours;

        [Header("Movement Configuration")]
        [SerializeField, Min(0), Tooltip("Determines the maximum speed of the agent in m/s.")]
        private float linealSpeed;
        public float LinealSpeed {
            get => linealSpeed;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                linealSpeed = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the acceleration of the lineal speed in m/s^2.")]
        private float linealAcceleration;
        public float LinealAcceleration {
            get => linealAcceleration;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                linealAcceleration = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the deacceleration of the lineal speed during braking in m/s^2.")]
        private float linearBrackingSpeed;
        public float LinearBrackingSpeed {
            get => linearBrackingSpeed;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                linearBrackingSpeed = value;
            }
        }

        [SerializeField, Min(0), Tooltip("Determines the turning speed deg/s and angular deacceleration in deg/s^2.")]
        private float angularSpeed;
        public float AngularSpeed {
            get => angularSpeed;
            set
            {
                if (value < 0) ThrowHelper.ThrowArgumentOutOfRangeException_ValueCannotBeNegative();
                angularSpeed = value;
            }
        }

        /// <summary>
        /// Determines if the agent has control over the rigidbody's position and velocity.
        /// </summary>
        public bool UpdateMovement { get; set; } = true;

        /// <summary>
        /// Determines if the agent has control over the rigidbody's rotation and angular velocity.
        /// </summary>
        public bool UpdateRotation { get; set; } = true;

        /// <summary>
        /// If <see langword="true"/>, agent will deaccelerate until reach 0 velocity and won't increase it's velocity until this property becomes <see langword="false"/>.
        /// </summary>
        public bool Brake { get; set; }

        /// <summary>
        /// If <see langword="true"/>, agent will rotate even if <see cref="Brake"/> is <see langword="true"/> and velocity reached 0.
        /// </summary>
        public bool RotateEvenWhenBraking { get; set; }

        private new Rigidbody rigidbody;
        private (ISteeringBehaviour Behaviour, float Strength)[] allSteeringBehaviours;
        private int steeringBehavioursCount;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void Awake()
        {
            rigidbody = GetComponent<Rigidbody>();

            SteeringBehaviour[] steeringBehaviours = this.steeringBehaviours;
            int count = steeringBehavioursCount = steeringBehaviours.Length;
            (ISteeringBehaviour Behaviour, float Strength)[] array = allSteeringBehaviours = new (ISteeringBehaviour Behaviour, float Strength)[count];
            for (int i = 0; i < steeringBehaviours.Length; i++)
            {
                SteeringBehaviour steeringBehaviour = steeringBehaviours[i];
                array[i] = (steeringBehaviour.Behaviour, steeringBehaviour.Strength);
            }
#if !UNITY_EDITOR
            this.steeringBehaviours = null;
#endif
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void FixedUpdate()
        {
            if (!UpdateMovement && !UpdateRotation)
                return;

            Vector3 direction = Vector3.zero;
            float directionSqrMagnitude = 0;
            if (!Brake || RotateEvenWhenBraking)
            {
                int count = steeringBehavioursCount;
                (ISteeringBehaviour Behaviour, float Strength)[] array = allSteeringBehaviours;
                if (unchecked((uint)count) > (uint)array.Length)
                {
                    Debug.Assert(false, "Index out of range.");
                    return;
                }

                for (int i = 0; i < count; i++)
                {
                    (ISteeringBehaviour Behaviour, float Strength) behaviour = array[i];
                    direction += behaviour.Behaviour.GetDirection() * behaviour.Strength;
                }

                direction.y = 0;

                if ((directionSqrMagnitude = direction.sqrMagnitude) > 1)
                    direction = direction.normalized;
            }

            if (UpdateMovement)
            {
                Vector3 targetVelocity = (Brake ? Vector3.zero : direction) * linealSpeed;
                Vector3 currentVelocity = rigidbody.velocity;
                float acceleration;
                if (targetVelocity.sqrMagnitude > currentVelocity.sqrMagnitude)
                    acceleration = linealAcceleration * Time.fixedDeltaTime;
                else
                    acceleration = linearBrackingSpeed * Time.fixedDeltaTime;
                rigidbody.velocity = Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration);
            }

            if (UpdateRotation)
            {
                float maxDegreesDelta = angularSpeed * Time.fixedDeltaTime;
                if (directionSqrMagnitude < 1)
                    maxDegreesDelta *= Mathf.Sqrt(directionSqrMagnitude);
                Quaternion to;
                if ((rigidbody.constraints & RigidbodyConstraints.FreezePosition) != RigidbodyConstraints.FreezePosition)
                {
                    Vector3 angularVelocity = rigidbody.angularVelocity;
                    if (angularVelocity.x > 0 || angularVelocity.y > 0 || angularVelocity.z > 0)
                    {
                        Vector3 normalized = angularVelocity.normalized;
                        float magnitude = angularVelocity.magnitude;
                        if (magnitude > maxDegreesDelta)
                        {
                            rigidbody.angularVelocity = normalized * (magnitude - maxDegreesDelta);
                            return;
                        }
                        else
                        {
                            rigidbody.angularVelocity = Vector3.zero;
                            maxDegreesDelta -= magnitude;
                        }
                    }

                    to = direction != Vector3.zero ? Quaternion.LookRotation(direction) : Quaternion.identity;
                }
                else if (direction != Vector3.zero)
                    to = Quaternion.LookRotation(direction);
                else
                    return;
                rigidbody.rotation = Quaternion.RotateTowards(rigidbody.rotation, to, maxDegreesDelta);
            }
        }

        /// <summary>
        /// Sets an <see cref="ISteeringBehaviour"/> that this agent will use.
        /// </summary>
        /// <param name="steeringBehaviour"><see cref="ISteeringBehaviour"/> to accept.</param>
        /// <param name="strength">This value multiplies the result of <see cref="ISteeringBehaviour.GetDirection"/>.</param>
        public void SetSteeringBehaviour(ISteeringBehaviour steeringBehaviour, float strength)
        {
            if (steeringBehaviour == null) ThrowHelper.ThrowArgumentNullException_SteeringBehaviour();

            int count = steeringBehavioursCount;
            (ISteeringBehaviour Behaviour, float Strength)[] array = allSteeringBehaviours;
            if (unchecked((uint)count) > (uint)array.Length)
            {
                Debug.Assert(false, "Index out of range.");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                if (array[i].Behaviour == steeringBehaviour)
                {
                    if (strength == 0)
                    {
                        count--;
                        steeringBehavioursCount = count;
                        if (i < count)
                            Array.Copy(array, i + 1, array, i, count - i);
                        array[count].Behaviour = null;
                        return;
                    }
                    array[i].Strength = strength;
                    return;
                }
            }

            if (strength == 0)
                return;

            if ((uint)count < (uint)array.Length)
            {
                steeringBehavioursCount = count + 1;
                array[count] = (steeringBehaviour, strength);
            }
            else
                AddWithResize(steeringBehaviour, strength);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void AddWithResize(ISteeringBehaviour steeringBehaviour, float strength)
        {
            int count = steeringBehavioursCount;
            (ISteeringBehaviour Behaviour, float Strength)[] array = allSteeringBehaviours;
            Array.Resize(ref array, array.Length * 2);
            allSteeringBehaviours = array;
            steeringBehavioursCount = count + 1;
            array[count] = (steeringBehaviour, strength);
        }

#if UNITY_EDITOR
        internal void ToInspector()
        {
            (ISteeringBehaviour Behaviour, float Strength)[] allSteeringBehaviours = this.allSteeringBehaviours;
            if (allSteeringBehaviours is null)
                return;
            SteeringBehaviour[] tmp = ArrayPool<SteeringBehaviour>.Shared.Rent(allSteeringBehaviours.Length);
            int count = 0;
            for (int i = 0; i < allSteeringBehaviours.Length; i++)
            {
                (ISteeringBehaviour Behaviour, float Strength) tuple = allSteeringBehaviours[i];
                if (tuple.Behaviour is MonoBehaviour monoBehaviour && monoBehaviour != null)
                    tmp[count++] = new SteeringBehaviour(monoBehaviour, tuple.Strength);
            }
            SteeringBehaviour[] steeringBehaviours = this.steeringBehaviours;
            if (steeringBehaviours?.Length == count)
                Array.Copy(tmp, steeringBehaviours, count);
            else
                this.steeringBehaviours = tmp.AsSpan(0, count).ToArray();
        }

        internal void FromInspector()
        {
            (ISteeringBehaviour Behaviour, float Strength)[] allSteeringBehaviours = this.allSteeringBehaviours;
            if (allSteeringBehaviours is null)
                return;
            SteeringBehaviour[] steeringBehaviours = this.steeringBehaviours;
            int count = steeringBehavioursCount;
            for (int i = 0; i < allSteeringBehaviours.Length; i++)
            {
                var tuple = allSteeringBehaviours[i];
                if (!(tuple.Behaviour is MonoBehaviour))
                    continue;
                for (int j = 0; j < steeringBehaviours.Length; j++)
                {
                    var item = steeringBehaviours[j];
                    if (tuple.Behaviour == item.Behaviour)
                    {
                        allSteeringBehaviours[i].Strength = item.Strength;
                        goto next;
                    }
                }
                count--;
                if (i < count)
                    Array.Copy(allSteeringBehaviours, i + 1, allSteeringBehaviours, i, count - i);
                allSteeringBehaviours[count].Behaviour = null;
                next:;
            }
            steeringBehavioursCount = count;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnDrawGizmosSelected()
        {
            Vector3 direction = Vector3.zero;
            if (steeringBehaviours is SteeringBehaviour[] behaviours_)
            {
                foreach (SteeringBehaviour behaviour in behaviours_)
                {
                    direction += behaviour.Behaviour.GetDirection() * behaviour.Strength;
                    behaviour.Behaviour.DrawGizmos();
                }
            }
            else
            {
                int count = steeringBehavioursCount;
                (ISteeringBehaviour Behaviour, float Strength)[] array = allSteeringBehaviours;
                if (unchecked((uint)count) > (uint)array.Length)
                {
                    Debug.Assert(false, "Index out of range.");
                    return;
                }

                for (int i = 0; i < count; i++)
                {
                    (ISteeringBehaviour Behaviour, float Strength) behaviour = array[i];
                    direction += behaviour.Behaviour.GetDirection() * behaviour.Strength;
                    behaviour.Behaviour.DrawGizmos();
                }
            }

            direction.y = 0;

            Gizmos.color = Color.white;
            Vector3 position = transform.position;
            Gizmos.DrawLine(position, position + (direction.normalized * 3));
        }
#endif
    }
}
