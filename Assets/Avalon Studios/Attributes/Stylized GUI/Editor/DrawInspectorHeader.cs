using UnityEngine;
using UnityEditor;

namespace AvalonStudios.Additions.Attributes.StylizedGUIs
{
    public partial class StylizedGUI
    {
        public static void DrawInspectorHeader(Rect rect, string banner, int fontSize = 0, float displacementY = 10)
        {
            Rect headerFullRect = new Rect(rect.position.x, rect.position.y + displacementY, rect.width, rect.height);
            Rect headerBeginRect = new Rect(headerFullRect.position.x, headerFullRect.position.y, 10, 20);
            Rect headerMidRect = new Rect(headerFullRect.position.x + 10, headerFullRect.position.y, headerFullRect.xMax - 32, 20);
            Rect headerEndRect = new Rect(headerFullRect.xMax - 10, headerFullRect.position.y, 10, 20);
            Rect titleRect = new Rect(headerFullRect.position.x, headerFullRect.position.y, headerFullRect.width, 18);

            if (EditorGUIUtility.isProSkin)
                GUI.color = GUIStylesConstants.DarkGrayColor;
            else
                GUI.color = GUIStylesConstants.WitheColor;

            GUIStyle catergoryImageB = new GUIStyle();
            catergoryImageB.normal.background = GUIStylesConstants.StyleBackground(GUIStylesConstants.HEADER_BEGIN);
            EditorGUI.LabelField(headerBeginRect, GUIContent.none, catergoryImageB);

            GUIStyle catergoryImageM = new GUIStyle();
            catergoryImageM.normal.background = GUIStylesConstants.StyleBackground(GUIStylesConstants.HEADER_MIDDLE);
            EditorGUI.LabelField(headerMidRect, GUIContent.none, catergoryImageM);

            GUIStyle catergoryImageE = new GUIStyle();
            catergoryImageE.normal.background = GUIStylesConstants.StyleBackground(GUIStylesConstants.HEADER_MIDDLE);
            EditorGUI.LabelField(headerEndRect, GUIContent.none, catergoryImageE);

            GUI.color = Color.white;
            GUIStyle stylesConstants = GUIStylesConstants.TitleStyle(fontSize == 0 ? 0 : fontSize);
            GUI.Label(titleRect, banner, stylesConstants);
        }
    }
}
