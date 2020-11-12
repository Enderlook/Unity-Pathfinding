using System;

using UnityEngine;

namespace Assets.Enderlook.Unity.Pathfinding.Steerings
{
    [Serializable]
    public abstract class SteeringWithPrediction : Steering
    {
        [SerializeField, Tooltip("How many seconds in future should it predict.")]
        private float timePrediction;
        public float TimePrediction {
            get => timePrediction;
            set => timePrediction = ErrorMessage.NoNegativeGuard(nameof(TimePrediction), value);
        }
        protected SteeringWithPrediction(float distanceToEnable, float timePrediction)
            : base(distanceToEnable) => TimePrediction = timePrediction;
    }
}
