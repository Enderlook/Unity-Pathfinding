using System;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    [CreateAssetMenu(fileName = "Baked Navigation", menuName = "Enderlook/Pathfinding/Baked Navigation")]
    public sealed class NavigationBaked : ScriptableObject
    {
        [SerializeField, HideInInspector]
        internal byte[] content;

        internal void Clear() => content = Array.Empty<byte>();
    }
}