using AvalonStudios.Additions.Attributes.StylizedGUIs;

using UnityEngine;
using UnityEditor;

namespace AvalonStudios.Additions.Attributes
{
    [CustomPropertyDrawer(typeof(Banner))]
    public class BannerDecoratorDrawer : DecoratorDrawer
    {
        private Banner banner;

        public override void OnGUI(Rect position)
        {
            banner = (Banner)attribute;
            StylizedGUI.DrawInspectorBanner(position, banner.title, banner.file);
        }

        public override float GetHeight() => 60;
    }
}
