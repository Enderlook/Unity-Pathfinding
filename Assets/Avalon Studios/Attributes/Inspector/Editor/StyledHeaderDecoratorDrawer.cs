using AvalonStudios.Additions.Attributes.StylizedGUIs;

using UnityEngine;
using UnityEditor;

namespace AvalonStudios.Additions.Attributes
{
    [CustomPropertyDrawer(typeof(StyledHeader))]
    public class StyledHeaderDecoratorDrawer : DecoratorDrawer
    {
        private StyledHeader styledHeader;

        public override void OnGUI(Rect position)
        {
            styledHeader = attribute as StyledHeader;
            StylizedGUI.DrawInspectorHeader(position, styledHeader.header, styledHeader.defaultFontSize ? 0 : styledHeader.fontSize);
        }

        public override float GetHeight() => 40;
    }
}
