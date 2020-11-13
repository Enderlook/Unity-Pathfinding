using AvalonStudios.Additions.Attributes.StylizedGUIs;

using System;
using UnityEngine;
using UnityEditor;

namespace AvalonStudios.Additions.Attributes
{
    public class StyledHeaderDrawer : MaterialPropertyDrawer
    {
        public readonly string header;

        public StyledHeaderDrawer(string header)
        {
            this.header = header;
        }

        public override void OnGUI(Rect position, MaterialProperty prop, String label, MaterialEditor materiaEditor)
        {
            if (prop.floatValue < 0)
                GUI.enabled = true;
            else
            {
                GUI.enabled = true;
                StylizedGUI.DrawInspectorHeader(position, header);
            }
        }

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor) =>
            prop.floatValue < 0 ? -2 : 40;
    }
}
