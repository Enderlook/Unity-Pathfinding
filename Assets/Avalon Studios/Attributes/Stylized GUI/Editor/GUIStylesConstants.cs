using AvalonStudios.Additions.Utils.Assets;

using UnityEngine;
using UnityEditor;

namespace AvalonStudios.Additions.Attributes.StylizedGUIs
{
    public static class GUIStylesConstants
    {
        private const string PATH_FOLDER_PACKAGE = "Assets/Avalon Studios/Art/Textures";

        public const string BANNER_BEGIN = "Banner_Begin";
        public const string BANNER_BEGIN_02 = "Banner_Begin_02";
        public const string BANNER_BEGIN_03 = "Banner_Begin_03";

        public const string BANNER_MIDDLE = "Banner_Middle";
        public const string BANNER_MIDDLE_02 = "Banner_Middle_02";

        public const string BANNER_END = "Banner_End";
        public const string BANNER_END_02 = "Banner_End_02";
        public const string BANNER_END_03 = "Banner_End_03";

        public const string HEADER_BEGIN = "Header_Begin";
        public const string HEADER_MIDDLE = "Header_Middle";
        public const string HEADER_END = "Header_End";
        public const string VERTICAL_LINE_01 = "Vertical_Line_01";
        public const string ICON_EDIT = "Icon_Edit_02";

        public static Texture2D StyleBackground(string nameTexture)
            => AssetDatabaseHandle.FindAssetByName<Texture2D>(nameTexture, PATH_FOLDER_PACKAGE);

        // Colors

        public static Color WitheColor => new Color(1, 1, 1, 1);

        public static Color DarkGrayColor => new Color(.12f, .12f, .12f, 1);

        public static Color LightGreyColor => new Color(.83f, .83f, .83f, 1);

        public static Color MediumGrayColor => new Color(.55f, .55f, .55f, 1);

        public static Color MediumDarkGrayColor => new Color(.35f, .35f, .35f, 1);

        public static Color MediumStrongDarkGrayColor => new Color(0.27f, 0.27f, 0.27f);

        public static Color BlueColor => new Color(0, 0, 1, 1);

        // ---

        public static GUIStyle TitleStyle(int fontSize = 0, FontStyle fontStyle = FontStyle.Bold)
        {
            GUIStyle guiStyle = new GUIStyle();

            if (EditorGUIUtility.isProSkin)
                guiStyle.normal.textColor = GUIStylesConstants.LightGreyColor;
            else
                guiStyle.normal.textColor = GUIStylesConstants.MediumStrongDarkGrayColor;

            guiStyle.richText = true;
            guiStyle.alignment = TextAnchor.MiddleCenter;
            guiStyle.fontStyle = fontStyle;

            if (fontSize != 0)
                guiStyle.fontSize = fontSize;

            return guiStyle;
        }

        public static GUIStyle TitleStyle(Color fontColor, int fontSize = 0, FontStyle fontStyle = FontStyle.Bold)
        {
            GUIStyle guiStyle = new GUIStyle();

            guiStyle.normal.textColor = fontColor;

            guiStyle.richText = true;
            guiStyle.alignment = TextAnchor.MiddleCenter;
            guiStyle.fontStyle = fontStyle;

            if (fontSize != 0)
                guiStyle.fontSize = fontSize;

            return guiStyle;
        }

        public static GUIStyle TitleStyle(Font font, int fontSize = 0, FontStyle fontStyle = FontStyle.Normal)
        {
            GUIStyle guiStyle = new GUIStyle();

            if (EditorGUIUtility.isProSkin)
                guiStyle.normal.textColor = GUIStylesConstants.LightGreyColor;
            else
                guiStyle.normal.textColor = GUIStylesConstants.MediumStrongDarkGrayColor;

            guiStyle.richText = true;
            guiStyle.alignment = TextAnchor.MiddleCenter;
            guiStyle.font = font;
            guiStyle.fontStyle = fontStyle;

            if (fontSize != 0)
                guiStyle.fontSize = fontSize;

            return guiStyle;
        }

        public static GUIStyle TitleStyle(Font font, Color fontColor, int fontSize = 0, FontStyle fontStyle = FontStyle.Normal)
        {
            GUIStyle guiStyle = new GUIStyle();

            guiStyle.normal.textColor = fontColor;
            guiStyle.richText = true;
            guiStyle.alignment = TextAnchor.MiddleCenter;
            guiStyle.font = font;
            guiStyle.fontStyle = fontStyle;

            if (fontSize != 0)
                guiStyle.fontSize = fontSize;

            return guiStyle;
        }
    }
}
