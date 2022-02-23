using Enderlook.Unity.Pathfinding.Steerings;
using Enderlook.Unity.Toolset.Attributes;

using System;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [Serializable]
    internal struct SteeringBehaviour
    {
        [SerializeField, RestrictType(typeof(ISteeringBehaviour)), Tooltip("Steering behaviour.")]
        private MonoBehaviour behaviour;
        public ISteeringBehaviour Behaviour
        {
            get
            {
                Debug.Assert(behaviour == null || behaviour is ISteeringBehaviour);
                return Unsafe.As<ISteeringBehaviour>(behaviour);
            }
        }

        [field: IsProperty, SerializeField, Tooltip("Factor that multiplies the effect of the behaviour in the agent.")]
        public float Strength { get; private set; }
    }
}
