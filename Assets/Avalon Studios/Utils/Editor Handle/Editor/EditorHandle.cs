using AvalonStudios.Additions.Extensions;
using AvalonStudios.Additions.Utils.Assets;

using UnityEngine;
using UnityEditor;

namespace AvalonStudios.Additions.Utils.EditorHandle
{
    public sealed class EditorHandle
    {
        public static Rect GetRectOfInspector
        {
            get
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                Rect scale = GUILayoutUtility.GetLastRect();
                return scale;
            }
        }

        public static void OpenScriptInIDE(string file)
        {
            MonoScript script = AssetDatabaseHandle.FindAssetByName<MonoScript>(file);
            if (!script.IsNull())
                AssetDatabase.OpenAsset(script);
        }
    }
}
