using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// Makes the cat pop out of the background:
    ///   • a crisp outline (8 flat-colour copies of the current frame, 1 art-pixel around the cat)
    ///   • a soft glow behind it
    /// The outline/glow colour follows the tension: white when calm → hot orange/red when the multiplier gets scary,
    /// gold when you cashed out. Needs the "KittyCrash/SpriteSilhouette" material (the installer assigns it).
    /// Put it on the Cat (root).
    /// </summary>
    public class CatHighlight : MonoBehaviour
    {
        public Material silhouetteMaterial;
        [Tooltip("Outline thickness in art pixels.")]
        [Range(0.5f, 12f)] public float thickness = 1f;
        public Color calmColor = new Color(1f, 1f, 1f, 0.95f);
        public Color tenseColor = new Color(1f, 0.35f, 0.2f, 1f);
        public Color winColor = new Color(1f, 0.84f, 0.25f, 1f);

        [Header("Glow")]
        public bool glow = true;
        public float glowSize = 2.6f;
        [Range(0f, 1f)] public float glowAlpha = 0.35f;

        CatWalker walker;
        SpriteRenderer cat;
        readonly SpriteRenderer[] copies = new SpriteRenderer[8];
        SpriteRenderer glowSR;
        Color current;

        static readonly Vector2[] Dirs =
        {
            new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1),
            new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f), new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f)
        };

        void Start()
        {
            walker = GetComponent<CatWalker>();
            var visual = walker != null && walker.visual != null ? walker.visual : (transform.childCount > 0 ? transform.GetChild(0) : null);
            cat = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            if (cat == null) { enabled = false; return; }

            if (silhouetteMaterial == null)
            {
                var sh = Shader.Find("KittyCrash/SpriteSilhouette");
                if (sh != null) silhouetteMaterial = new Material(sh);
            }
            if (silhouetteMaterial != null)
            {
                for (int i = 0; i < copies.Length; i++)
                {
                    var go = new GameObject("Outline" + i);
                    go.transform.SetParent(cat.transform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sharedMaterial = silhouetteMaterial;
                    sr.sortingLayerID = cat.sortingLayerID;
                    sr.sortingOrder = cat.sortingOrder - 1;
                    copies[i] = sr;
                }
            }

            if (glow)
            {
                var g = new GameObject("CatGlow");
                g.transform.SetParent(transform, false);
                glowSR = g.AddComponent<SpriteRenderer>();
                glowSR.sprite = GlowSprite();
                glowSR.sortingLayerID = cat.sortingLayerID;
                glowSR.sortingOrder = cat.sortingOrder - 3;
            }
            current = calmColor;
        }

        void LateUpdate()
        {
            if (cat == null) return;
            var rm = RoundManager.Instance;

            // colour from the game state
            Color target = calmColor;
            if (rm != null)
            {
                if (rm.CashedOut) target = winColor;
                else if (rm.Phase == RoundPhase.Running && !rm.IsWarmingUp)
                    target = Color.Lerp(calmColor, tenseColor, Tension.Of(rm));
                else if (rm.Phase == RoundPhase.Ended && rm.HasBet) target = tenseColor;
            }
            current = Color.Lerp(current, target, Time.deltaTime * 4f);

            var sp = cat.sprite;
            float px = sp != null ? thickness / sp.pixelsPerUnit : 0.06f;
            for (int i = 0; i < copies.Length; i++)
            {
                var c = copies[i];
                if (c == null) continue;
                c.sprite = sp;
                c.flipX = cat.flipX;
                c.flipY = cat.flipY;
                c.enabled = cat.enabled && sp != null;
                c.color = current;
                c.transform.localPosition = (Vector3)(Dirs[i] * px);
            }

            if (glowSR != null)
            {
                var b = cat.bounds;
                glowSR.transform.position = new Vector3(b.center.x, b.center.y, transform.position.z);
                float pulse = 1f + 0.05f * Mathf.Sin(Time.time * (2f + 6f * (rm != null ? Tension.Of(rm) : 0f)));
                float s = Mathf.Max(b.size.x, b.size.y) * glowSize / 2f * pulse;
                glowSR.transform.localScale = new Vector3(s, s, 1f);
                glowSR.color = new Color(current.r, current.g, current.b, glowAlpha);
            }
        }

        static Sprite glowSprite;
        static Sprite GlowSprite()
        {
            if (glowSprite != null) return glowSprite;
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            glowSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N / 2f); // 2 units wide
            return glowSprite;
        }
    }

    /// <summary>One shared "how scary is it right now" number (0 calm … 1 panic) for the face, outline and vignette.</summary>
    public static class Tension
    {
        /// <summary>Extra tension added by CatTemptations while a temptation is near the cat.</summary>
        public static float TemptBoost;
        /// <summary>Consecutive wins (greed makes it scarier).</summary>
        public static int WinStreak;

        public static float Of(RoundManager rm)
        {
            if (rm == null || rm.Phase != RoundPhase.Running || rm.IsWarmingUp || rm.CashedOut) return 0f;
            // Chance the cat would already have been distracted by this multiplier (house edge 4%).
            float risk = 1f - 0.96f / Mathf.Max(1f, rm.Multiplier);
            float t = risk * (rm.HasBet ? 1f : 0.6f) + WinStreak * 0.06f + TemptBoost;
            return Mathf.Clamp01(t);
        }
    }
}
