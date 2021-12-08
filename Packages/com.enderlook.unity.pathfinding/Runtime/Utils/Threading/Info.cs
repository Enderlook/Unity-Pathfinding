using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Utils
{
    internal static class Info
    {
        public static readonly int ProcessorCount = SystemInfo.processorCount;
#if UNITY_WEBGL && !UNITY_EDITOR
        public const bool SupportMultithreading = false;
#else
        public const bool SupportMultithreading = false;
#endif
    }
}