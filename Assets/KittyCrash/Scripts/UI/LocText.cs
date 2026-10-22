using UnityEngine;
using UnityEngine.UI;

namespace KittyCrash
{
    /// <summary>Keeps a fixed label (built once, e.g. "CAT SKIN") in the current language: LocText.Bind(text, "CAT SKIN").</summary>
    public class LocText : MonoBehaviour
    {
        string english;
        Text text;
        Loc.Lang shown;

        public static void Bind(Text text, string english)
        {
            var l = text.GetComponent<LocText>();
            if (l == null) l = text.gameObject.AddComponent<LocText>();
            l.text = text;
            l.english = english;
            l.shown = Loc.Current;
            text.text = Loc.E(english);
        }

        void LateUpdate()
        {
            if (text == null || shown == Loc.Current) return;
            shown = Loc.Current;
            text.text = Loc.E(english);
        }
    }
}
