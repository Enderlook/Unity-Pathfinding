using UnityEngine;

namespace AvalonStudios.Additions.Utils.InputsManager
{
    [System.Serializable]
    public class KeyInputManager
    {
        [SerializeField]
        private KeyCode[] keys = null;

        [SerializeField]
        private bool canBeHoldDown = false;

        public bool Execute()
        {
            bool isTrigger = false;
            foreach(KeyCode key in keys)
                isTrigger = canBeHoldDown ? Input.GetKey(key) : Input.GetKeyDown(key);

            return isTrigger;
        }
    }
}
