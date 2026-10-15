using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KittyCrash
{
    /// <summary>Keyboard shortcuts. Works with the new Input System or the old Input Manager.</summary>
    public static class InputReader
    {
        /// <summary>Space / Enter: place bet during betting, cash out while walking.</summary>
        public static bool ActionPressed()
        {
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
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && k.nKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.N);
#endif
        }
    }
}
