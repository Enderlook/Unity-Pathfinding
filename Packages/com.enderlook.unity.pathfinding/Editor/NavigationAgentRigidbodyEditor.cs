using UnityEditor;

namespace Enderlook.Unity.Pathfinding
{
    [CustomEditor(typeof(NavigationAgentRigidbody))]
    public sealed class NavigationAgentRigidbodyEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            NavigationAgentRigidbody target_ = (NavigationAgentRigidbody)target;
            target_.ToInspector();
            DrawDefaultInspector();
            target_.FromInspector();
        }
    }
}
