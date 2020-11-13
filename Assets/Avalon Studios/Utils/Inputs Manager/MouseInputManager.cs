using UnityEngine;

namespace AvalonStudios.Additions.Utils.InputsManager
{
    [System.Serializable]
    public class MouseInputManager
    {
        [SerializeField]
        private MouseButton button = MouseButton.None;

        [SerializeField]
        private bool canBeHoldDown = false;

        public bool Execute() => canBeHoldDown ? button != MouseButton.None && Input.GetMouseButton((int)button) : button != MouseButton.None && Input.GetMouseButtonDown((int)button);

    }
}
