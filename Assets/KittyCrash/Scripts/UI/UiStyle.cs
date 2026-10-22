using UnityEngine;
using UnityEngine.UI;

namespace KittyCrash
{
    /// <summary>
    /// The game HUD's look (CrashUI) for the runtime-built panels (wallet, menu): the same rounded card sprite,
    /// dock colours, orange accent edge, button tints and the HUD's header font.
    /// </summary>
    public static class UiStyle
    {
        public static readonly Color Panel = new Color(0.06f, 0.07f, 0.11f, 0.98f);   // CrashUI panel
        public static readonly Color Card = new Color(0.11f, 0.12f, 0.18f, 1f);       // dock cards
        public static readonly Color CardLight = new Color(0.15f, 0.16f, 0.24f, 1f);  // CrashUI panelLight
        public static readonly Color Accent = new Color(1f, 0.66f, 0.18f, 1f);
        public static readonly Color Field = new Color(0.07f, 0.075f, 0.11f, 1f);

        static Sprite rounded;

        /// <summary>CrashUI's rounded-corner sprite (sliced, so any size keeps crisp corners).</summary>
        public static Sprite Rounded => rounded != null ? rounded : rounded = MakeRounded(128, 32, 200f);

        /// <summary>The HUD's bold font (the scene's CrashUI), or the fallback when there is none.</summary>
        public static Font HeaderFont(Font fallback)
        {
            var hud = Object.FindAnyObjectByType<CrashUI>();
            return hud != null && hud.boldFont != null ? hud.boldFont : fallback;
        }

        public static void Round(Image img, Color color)
        {
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
        }

        /// <summary>Rounded background plus the HUD's hover / pressed / disabled tints.</summary>
        public static void Style(Button b, Image bg, Color color)
        {
            Round(bg, color);
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.selectedColor = Color.white;
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.55f);
            b.colors = cb;
        }

        /// <summary>The thin orange line along the top of a panel, like the betting dock's edge.</summary>
        public static void AccentEdge(RectTransform panel)
        {
            var go = new GameObject("AccentEdge", typeof(RectTransform));
            go.transform.SetParent(panel, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(18f, -5f); r.offsetMax = new Vector2(-18f, -1f);
            var img = go.AddComponent<Image>();
            img.color = new Color(Accent.r, Accent.g, Accent.b, 0.8f);
            img.raycastTarget = false;
        }

        /// <summary>A soft drop shadow, as on the HUD's titles.</summary>
        public static void Shadow(Graphic g, float d = 2f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.55f);
            s.effectDistance = new Vector2(d, -d);
        }

        static Sprite MakeRounded(int size, int radius, float ppu)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }
    }
}
