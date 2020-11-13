using UnityEngine;
using UnityEditor;

namespace AvalonStudios.Additions.Attributes.StylizedGUIs
{
    public partial class StylizedGUI
    {
        public static void DrawInspectorSpace(Rect rect, float displacementX = 0, float displacementY = 10, float width = 0, float height = 0)
        {
            Rect separator = new Rect(rect.position.x + displacementX, rect.position.y + displacementY, rect.width + width, rect.height + height);

            if (EditorGUIUtility.isProSkin)
                GUI.color = GUIStylesConstants.WitheColor;
            else
                GUI.color = GUIStylesConstants.DarkGrayColor;

            GUIStyle separatorTex = new GUIStyle();
            separatorTex.normal.background = GUIStylesConstants.StyleBackground(GUIStylesConstants.VERTICAL_LINE_01);
            EditorGUI.LabelField(separator, GUIContent.none, separatorTex);
            GUI.color = Color.white;
        }

        public static void DrawInspectorSpace(Rect rect, float displacementX = 0, float displacementY = 10, float width = 0, float height = 0, float space = 0)
        {
            Rect separator = new Rect(rect.position.x + displacementX, rect.position.y + displacementY, rect.width + width, rect.height + height);

            if (EditorGUIUtility.isProSkin)
                GUI.color = GUIStylesConstants.WitheColor;
            else
                GUI.color = GUIStylesConstants.DarkGrayColor;

            GUIStyle separatorTex = new GUIStyle();
            separatorTex.normal.background = GUIStylesConstants.StyleBackground(GUIStylesConstants.VERTICAL_LINE_01);
            separatorTex.fixedHeight = separator.height;
            EditorGUI.LabelField(separator, GUIContent.none, separatorTex);
            GUILayout.Space(space);
            GUI.color = Color.white;
        }
    }
}
