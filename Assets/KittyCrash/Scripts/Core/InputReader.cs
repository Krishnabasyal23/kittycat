using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KittyCrash
{
    /// <summary>Keyboard shortcuts. Works with the new Input System or the old Input Manager.</summary>
    public static class InputReader
    {
        /// <summary>True while the title screen or a menu page is open (set by GameMenu).</summary>
        public static bool MenuOpen;

        /// <summary>True while a text field has focus or a menu is open: game shortcuts must ignore the keyboard then.</summary>
        public static bool Typing => FieldFocused || MenuOpen;

        public static bool FieldFocused
        {
            get
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                var go = es != null ? es.currentSelectedGameObject : null;
                var field = go != null ? go.GetComponent<UnityEngine.UI.InputField>() : null;
                return field != null && field.isFocused;
            }
        }

        /// <summary>Space / Enter: place bet during betting, cash out while walking.</summary>
        public static bool ActionPressed()
        {
            if (Typing) return false;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#endif
        }

        /// <summary>N: jump to the next season transition (dev/testing).</summary>
        public static bool DebugNextSeason()
        {
            if (Typing) return false;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && k.nKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.N);
#endif
        }
    }
}
