using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Utils
{
    internal static class Info
    {
        public static int ProcessorCount = SystemInfo.processorCount;
        public static bool SupportMultithreading = Application.platform != RuntimePlatform.WebGLPlayer && ProcessorCount > 1;
    }
}