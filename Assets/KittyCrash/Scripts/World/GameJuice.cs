using System.Collections.Generic;
using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// Cat extras: soft shadow under the paws, dust puffs while running, a coin shower on cash-out,
    /// and a little "!" pop when distracted. Put it on the Cat (root).
    /// </summary>
    public class CatFx : MonoBehaviour
    {
        public float shadowWidth = 1.1f;
        public float dustEvery = 0.09f;
        public Color dustColor = new Color(0.86f, 0.8f, 0.7f, 0.7f);

        CatWalker walker;
        SpriteRenderer shadow;
        float dustTimer;

        void Start()
        {
            walker = GetComponent<CatWalker>();

            var go = new GameObject("Shadow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            shadow = go.AddComponent<SpriteRenderer>();
            shadow.sprite = FxPool.Ellipse;
            shadow.color = new Color(0f, 0f, 0f, 0.32f);
            shadow.sortingOrder = 9;
            go.transform.localScale = new Vector3(shadowWidth, shadowWidth * 0.22f, 1f);

            var rm = RoundManager.Instance;
            if (rm != null)
            {
                rm.OnCashOut += (m, p) => Coins();
                rm.OnCrash += (c, d) => Surprise();
            }
        }

        void Update()
        {
            if (walker == null) return;
            float sp = walker.CurrentSpeed;
            float bob = sp > 0.05f ? 1f - Mathf.Abs(Mathf.Sin(Time.time * (6f + sp * 2f))) * 0.12f : 1f;
            shadow.transform.localScale = new Vector3(shadowWidth * bob, shadowWidth * 0.22f * bob, 1f);

            if (sp > 1.2f)
            {
                dustTimer -= Time.deltaTime;
                if (dustTimer <= 0f)
                {
                    dustTimer = dustEvery * Mathf.Lerp(1.5f, 0.6f, Mathf.InverseLerp(1.2f, 5f, sp));
                    var pos = transform.position + new Vector3(-0.35f, 0.05f, 0f);
                    FxPool.Spawn(pos, new Vector2(Random.Range(-1.2f, -0.4f), Random.Range(0.3f, 0.9f)), dustColor,
                        Random.Range(0.12f, 0.22f), 0.55f, -0.5f, 12);
                }
            }
        }

        void Coins()
        {
            var c = transform.position + Vector3.up * 0.8f;
            for (int i = 0; i < 26; i++)
            {
                var v = new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(3.5f, 7f));
                var col = Random.value < 0.75f ? new Color(1f, 0.82f, 0.2f) : new Color(1f, 0.95f, 0.6f);
                FxPool.Spawn(c, v, col, Random.Range(0.12f, 0.2f), Random.Range(0.9f, 1.4f), 12f, 25);
            }
        }

        void Surprise()
        {
            var c = transform.position + Vector3.up * 1.2f;
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                FxPool.Spawn(c, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.2f, new Color(1f, 1f, 1f, 0.9f), 0.1f, 0.45f, 0f, 25);
            }
        }
    }

    /// <summary>Tiny pooled sprite-particle system (no ParticleSystem setup needed).</summary>
    public class FxPool : MonoBehaviour
    {
        class P { public Transform t; public SpriteRenderer sr; public Vector2 v; public float life, max, gravity, size; public Color c; }

        static FxPool instance;
        static Sprite square, ellipse;
        readonly List<P> live = new List<P>();
        readonly Stack<P> free = new Stack<P>();

        public static Sprite Square
        {
            get
            {
                if (square == null)
                {
                    var t = new Texture2D(2, 2) { filterMode = FilterMode.Point };
                    t.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
                    t.Apply();
                    square = Sprite.Create(t, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                }
                return square;
            }
        }

        public static Sprite Ellipse
        {
            get
            {
                if (ellipse == null)
                {
                    const int W = 32, H = 32;
                    var t = new Texture2D(W, H) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                    var px = new Color32[W * H];
                    for (int y = 0; y < H; y++)
                        for (int x = 0; x < W; x++)
                        {
                            float dx = (x + 0.5f) / W * 2f - 1f, dy = (y + 0.5f) / H * 2f - 1f;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            float f = Mathf.Clamp01(1f - d);
                            px[y * W + x] = new Color32(255, 255, 255, (byte)Mathf.Min(255f, f * f * 255f * 1.6f));
                        }
                    t.SetPixels32(px);
                    t.Apply();
                    ellipse = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), W);
                }
                return ellipse;
            }
        }

        static FxPool Get()
        {
            if (instance == null) instance = new GameObject("FxPool").AddComponent<FxPool>();
            return instance;
        }

        public static void Spawn(Vector3 pos, Vector2 velocity, Color color, float size, float life, float gravity, int order, Sprite sprite = null)
        {
            var pool = Get();
            var p = pool.free.Count > 0 ? pool.free.Pop() : pool.Make();
            p.sr.sprite = sprite != null ? sprite : Square;
            p.t.gameObject.SetActive(true);
            p.t.position = pos;
            p.t.localScale = Vector3.one * size;
            p.v = velocity; p.life = 0f; p.max = life; p.gravity = gravity; p.size = size; p.c = color;
            p.sr.color = color;
            p.sr.sortingOrder = order;
            pool.live.Add(p);
        }

        P Make()
        {
            var go = new GameObject("fx");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Square;
            return new P { t = go.transform, sr = sr };
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                p.life += dt;
                if (p.life >= p.max)
                {
                    p.t.gameObject.SetActive(false);
                    live.RemoveAt(i);
                    free.Push(p);
                    continue;
                }
                p.v.y -= p.gravity * dt;
                p.t.position += (Vector3)(p.v * dt);
                float k = 1f - p.life / p.max;
                p.t.localScale = Vector3.one * p.size * (0.4f + 0.6f * k);
                var c = p.c; c.a *= k; p.sr.color = c;
            }
        }
    }
}
