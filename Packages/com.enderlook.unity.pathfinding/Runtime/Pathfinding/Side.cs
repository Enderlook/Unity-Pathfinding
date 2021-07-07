using UnityEngine;

namespace Enderlook.Unity.Pathfinding2
{
    internal struct Side
    {
        public struct Left { }

        public struct Forward { }

        public struct Right { }

        public struct Backward { }
    }

    internal static class Utility
    {
        public readonly static bool UseMultithreading = Application.platform == RuntimePlatform.WebGLPlayer || SystemInfo.processorCount == 1;
    }
}