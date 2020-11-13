using AvalonStudios.Additions.Utils.EditorHandle;
using AvalonStudios.Additions.Utils.Assets;

using UnityEngine;
using UnityEditor;

namespace AvalonStudios.Additions.Attributes.StylizedGUIs
{
    public partial class StylizedGUI
    {
        public static void DrawInspectorBanner(Rect rect, string title, string file, float displacementY = 10, float displacementTitle = 10, float displacementIcon = 5)
        {
            Rect bannerFullRect = new Rect(rect.position.x, rect.position.y + displacementY, rect.width, rect.height);
            Rect bannerBeginRect = new Rect(bannerFullRect.position.x, bannerFullRect.position.y, 20, 40);
            Rect bannerMidRect = new Rect(bannerFullRect.position.x + 20, bannerFullRect.position.y, bannerFullRect.xMax - 54, 40);
            Rect bannerEndRect = new Rect(bannerFullRect.xMax - 20, bannerFullRect.position.y, 20, 40);
            Rect iconRect = new Rect(bannerFullRect.xMax - 36, bannerFullRect.position.y + displacementIcon, 30, 30);
            Rect titleRect = new Rect(bannerFullRect.position.x, bannerFullRect.position.y + displacementTitle, bannerFullRect.width, 18);

            Color buttonColor = new Color(0, 0, 0, 1);

            if (EditorGUIUtility.isProSkin)
            {
                buttonColor = GUIStylesConstants.LightGreyColor;
                GUI.color = GUIStylesConstants.DarkGrayColor;
            }
            else
            {
                GUI.color = GUIStylesConstants.WitheColor;
                buttonColor = GUIStylesConstants.DarkGrayColor;
            }

            GUI.DrawTexture(bannerBeginRect, GUIStylesConstants.StyleBackground(GUIStylesConstants.BANNER_BEGIN_03), ScaleMode.StretchToFill, true);
            GUI.DrawTexture(bannerMidRect, GUIStylesConstants.StyleBackground(GUIStylesConstants.BANNER_MIDDLE_02), ScaleMode.StretchToFill, true);
            GUI.DrawTexture(bannerEndRect, GUIStylesConstants.StyleBackground(GUIStylesConstants.BANNER_END_03), ScaleMode.StretchToFill, true);

            GUI.color = Color.white;
            GUIStyle stylesConstants = GUIStylesConstants.TitleStyle(15);
            GUI.Label(titleRect, $"{title}", stylesConstants);

            GUI.color = buttonColor;
            GUIStyle buttonStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter
            };
            if (GUI.Button(iconRect, new GUIContent(GUIStylesConstants.StyleBackground(GUIStylesConstants.ICON_EDIT), $"Edit {file}"), buttonStyle))
                EditorHandle.OpenScriptInIDE(file);
            GUI.color = Color.white;
        }

        public static void DrawInspectorBanner(Rect rect, string title, string file, Color bannerColor, Color fontColor, string bannerBeginTex, string bannerMidTex, string bannerEndTex,
            float displacementY = 10, float displacementTitle = 10, float displacementIcon = 5)
        {
            Rect bannerFullRect = new Rect(rect.position.x, rect.position.y + displacementY, rect.width, rect.height);
            Rect bannerBeginRect = new Rect(bannerFullRect.position.x, bannerFullRect.position.y, 20, 40);
            Rect bannerMidRect = new Rect(bannerFullRect.position.x + 20, bannerFullRect.position.y, bannerFullRect.xMax - 54, 40);
            Rect bannerEndRect = new Rect(bannerFullRect.xMax - 20, bannerFullRect.position.y, 20, 40);
            Rect iconRect = new Rect(bannerFullRect.xMax - 36, bannerFullRect.position.y + displacementIcon, 30, 30);
            Rect titleRect = new Rect(bannerFullRect.position.x, bannerFullRect.position.y + displacementTitle, bannerFullRect.width, 18);

            Color buttonColor = fontColor;
            GUI.color = bannerColor;

            GUI.DrawTexture(bannerBeginRect, GUIStylesConstants.StyleBackground(bannerBeginTex), ScaleMode.StretchToFill, true);
            GUI.DrawTexture(bannerMidRect, GUIStylesConstants.StyleBackground(bannerMidTex), ScaleMode.StretchToFill, true);
            GUI.DrawTexture(bannerEndRect, GUIStylesConstants.StyleBackground(bannerEndTex), ScaleMode.StretchToFill, true);

            GUI.color = Color.white;
            GUIStyle stylesConstants = GUIStylesConstants.TitleStyle(fontColor, 15);
            GUI.Label(titleRect, $"{title}", stylesConstants);

            GUI.color = buttonColor;
            GUIStyle buttonStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter
            };
            if (GUI.Button(iconRect, new GUIContent(GUIStylesConstants.StyleBackground(GUIStylesConstants.ICON_EDIT), $"Edit {file}"), buttonStyle))
                EditorHandle.OpenScriptInIDE(file);
            GUI.color = Color.white;
        }
    }
}
