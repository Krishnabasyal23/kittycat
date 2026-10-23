using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// The Catino logos (Resources/KittyCrashLogos): the wordmark for the HUD and the emblem for the title screen.
    /// Returns null when a file is missing, so callers can fall back to plain text.
    /// </summary>
    public static class Logos
    {
        static Sprite wordmark, emblem;

        public static Sprite Wordmark => wordmark != null ? wordmark : (wordmark = Make("KittyCrashLogos/catino_wordmark"));
        public static Sprite Emblem => emblem != null ? emblem : (emblem = Make("KittyCrashLogos/catino_emblem"));

        static Sprite Make(string path)
        {
            var t = Resources.Load<Texture2D>(path);
            if (t == null) return null;
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
