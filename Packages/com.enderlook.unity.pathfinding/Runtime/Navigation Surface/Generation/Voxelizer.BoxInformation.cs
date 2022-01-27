using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Generation
{
    internal partial struct Voxelizer
    {
        private struct BoxInformation : IMinMax
        {
            public Vector3 Center;
            public Vector3 Size;
            public Quaternion Rotation;
            public Vector3 LossyScale;
            public Vector3 WorldPosition;

            public Vector3 Min { get; set; }
            public Vector3 Max { get; set; }

            public BoxInformation(Vector3 center, Vector3 size, Quaternion rotation, Vector3 lossyScale, Vector3 position)
            {
                Center = center;
                Size = size;
                Rotation = rotation;
                LossyScale = lossyScale;
                WorldPosition = position;
                Min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                Max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            }
        }
    }
}
