using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KittyCrash
{
    public enum Festival { Easter, LunarNewYear, SummerFireworks, Halloween, Diwali, Christmas }

    /// <summary>
    /// Festivals on top of the four seasons. Each season picks one of its festivals (randomly, per season change):
    ///   Spring → Easter or Lunar New Year · Summer → Summer Fireworks · Autumn → Halloween or Diwali · Winter → Christmas
    /// A festival brings: a garland strung across the top of the view, festive props along the path (eggs, lanterns,
    /// diyas, jack-o'-lanterns, gift-wrapped trees…), sky life (fireworks, floating lanterns, bats, a big moon) and,
    /// for evening festivals, a dusk sky so the lights glow. Fireworks also burst on cash-outs and big multipliers.
    /// Press F (in the editor/game) to cycle festivals for testing. The installer fills in all sprites.
    /// </summary>
    public class FestivalSystem : MonoBehaviour
    {
        [Serializable]
        public class FestivalArt
        {
            public Festival festival;
            public string displayName;
            public Season season;
            public Sprite garland;
            public Sprite[] groundProps = new Sprite[0];
            public bool dusk;
            public bool fireworks;
            public bool skyLanterns;
            public bool bats;
            public bool moon;
            public Color glowColor = new Color(1f, 0.75f, 0.35f);
            public Color[] fireworkColors = new Color[0];
        }

        public static FestivalSystem Instance { get; private set; }

        public FestivalArt[] festivals = new FestivalArt[0];
        public Sprite skyLantern, batA, batB, moonSprite, glow, spark, dot;

        [Header("Feel")]
        public bool festivalsOn = true;
        public Vector2 propSpacing = new Vector2(7f, 14f);
        public Vector2 fireworkInterval = new Vector2(1.4f, 3.2f);
        [Range(0f, 1f)] public float duskStrength = 0.55f;
        public float groundY = -2.5f;

        public Festival Current { get; private set; }
        public FestivalArt CurrentArt => Find(Current);
        public event Action<Festival> OnFestivalChanged;

        Camera cam;
        RoundManager rm;
        Transform root, garlandRow;
        readonly List<SpriteRenderer> garlandTiles = new List<SpriteRenderer>();
        readonly List<Transform> props = new List<Transform>();
        readonly List<Transform> fading = new List<Transform>();
        readonly List<Floater> floaters = new List<Floater>();
        SpriteRenderer duskSky, duskWorld, moonSR;
        float nextPropX, fireworkTimer, lanternTimer, batTimer, festivalAlpha = 1f, duskNow;
        int lastMilestone;

        class Floater { public Transform t; public SpriteRenderer sr; public Vector2 v; public float age, life, phase; public bool flap; }

        void Awake() { Instance = this; }

        void Start()
        {
            cam = Camera.main;
            rm = RoundManager.Instance;
            var main = TreeSpawner.FindMain();
            if (main != null) groundY = main.groundY;

            root = new GameObject("FestivalLayer").transform;
            root.SetParent(transform, false);
            garlandRow = new GameObject("Garland").transform;
            garlandRow.SetParent(root, false);

            duskSky = MakeQuad("DuskSky", -96, new Color(0.10f, 0.08f, 0.28f, 0f));
            duskWorld = MakeQuad("DuskWorld", 90, new Color(0.08f, 0.07f, 0.2f, 0f));
            if (moonSprite != null)
            {
                moonSR = new GameObject("FestivalMoon").AddComponent<SpriteRenderer>();
                moonSR.transform.SetParent(root, false);
                moonSR.sprite = moonSprite;
                moonSR.sortingOrder = -95;
                moonSR.color = new Color(1f, 1f, 1f, 0f);
            }

            var ss = SeasonSystem.Instance;
            if (ss != null)
            {
                ss.OnSeasonChanged += s => PickFor(s);
                PickFor(ss.CurrentSeason);
            }
            else SetFestival(Festival.Christmas);

            if (rm != null)
            {
                rm.OnCashOut += (m, p) => { if (Art(f => f.fireworks)) for (int i = 0; i < 3; i++) Firework(RandomSkyPoint(), 1.3f); };
                rm.OnRoundStarted += () => lastMilestone = 0;
            }
            nextPropX = cam != null ? cam.transform.position.x : 0f;
        }

        // ── choosing ─────────────────────────────

        void PickFor(Season s)
        {
            var options = new List<FestivalArt>();
            foreach (var f in festivals) if (f != null && f.season == s) options.Add(f);
            if (options.Count == 0) return;
            SetFestival(options[UnityEngine.Random.Range(0, options.Count)].festival);
        }

        public void SetFestival(Festival f)
        {
            Current = f;
            var art = CurrentArt;
            // old props fade out, the path ahead fills with the new festival's things
            fading.AddRange(props);
            props.Clear();
            nextPropX = cam != null ? cam.transform.position.x + 2f : nextPropX;
            BuildGarland(art);
            var ss = SeasonSystem.Instance;
            if (ss != null && art != null) { var th = ss.Get(art.season); if (th != null) th.festiveName = art.displayName; }
            festivalAlpha = 0f;
            OnFestivalChanged?.Invoke(f);
        }

        public void NextFestival()
        {
            if (festivals.Length == 0) return;
            int i = Array.FindIndex(festivals, x => x.festival == Current);
            SetFestival(festivals[(i + 1) % festivals.Length].festival);
        }

        FestivalArt Find(Festival f)
        {
            foreach (var x in festivals) if (x != null && x.festival == f) return x;
            return null;
        }

        bool Art(Func<FestivalArt, bool> test) { var a = CurrentArt; return festivalsOn && a != null && test(a); }

        // ── per frame ────────────────────────────

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) NextFestival();
#else
            if (Input.GetKeyDown(KeyCode.F)) NextFestival();
#endif
            if (cam == null) { cam = Camera.main; if (cam == null) return; }
            var art = CurrentArt;
            festivalAlpha = Mathf.MoveTowards(festivalAlpha, festivalsOn ? 1f : 0f, Time.deltaTime * 0.8f);

            float h = cam.orthographicSize, w = h * cam.aspect;
            Vector3 c = cam.transform.position;

            // dusk for evening festivals
            float duskTarget = festivalsOn && art != null && art.dusk ? duskStrength : 0f;
            duskNow = Mathf.MoveTowards(duskNow, duskTarget, Time.deltaTime * 0.4f);
            Cover(duskSky, c, w, h, new Color(0.10f, 0.08f, 0.28f, duskNow));
            Cover(duskWorld, c, w, h, new Color(0.08f, 0.07f, 0.2f, duskNow * 0.35f));
            if (moonSR != null)
            {
                float ma = Art(f => f.moon) ? festivalAlpha : Mathf.Max(0f, moonSR.color.a - Time.deltaTime);
                moonSR.color = new Color(1f, 1f, 1f, ma);
                moonSR.transform.position = new Vector3(c.x + w * 0.55f, c.y + h * 0.62f, 0f);
                moonSR.transform.localScale = Vector3.one;
            }

            UpdateGarland(c, w, h);
            UpdateProps(c, w);
            UpdateSky(c, w, h);
            UpdateFloaters();
        }

        // ── garland across the top ───────────────

        void BuildGarland(FestivalArt art)
        {
            foreach (var t in garlandTiles) if (t != null) Destroy(t.gameObject);
            garlandTiles.Clear();
            if (art == null || art.garland == null) return;
            for (int i = 0; i < 14; i++)
            {
                var sr = new GameObject("GarlandTile").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(garlandRow, false);
                sr.sprite = art.garland;
                sr.sortingOrder = 46;
                garlandTiles.Add(sr);
            }
        }

        void UpdateGarland(Vector3 c, float w, float h)
        {
            if (garlandTiles.Count == 0) return;
            float tw = garlandTiles[0].sprite.bounds.size.x;
            if (tw <= 0.01f) return;
            float start = Mathf.Floor((c.x - w - tw) / tw) * tw;   // world-anchored: the string scrolls with the world
            float top = c.y + h;
            for (int i = 0; i < garlandTiles.Count; i++)
            {
                var sr = garlandTiles[i];
                float x = start + i * tw + tw * 0.5f;
                float sway = Mathf.Sin(Time.time * 1.3f + x * 0.7f) * 0.03f;
                sr.transform.position = new Vector3(x, top - sr.sprite.bounds.extents.y - 0.05f + sway, 0f);
                sr.color = new Color(1f, 1f, 1f, festivalAlpha);
                sr.enabled = festivalsOn;
            }
        }

        // ── props along the path ─────────────────

        void UpdateProps(Vector3 c, float w)
        {
            float right = c.x + w + 4f, left = c.x - w - 4f;
            var art = CurrentArt;
            if (festivalsOn && art != null && art.groundProps.Length > 0)
            {
                if (nextPropX < c.x - w) nextPropX = c.x + w + 1f;
                while (nextPropX < right)
                {
                    SpawnProp(art, nextPropX);
                    nextPropX += UnityEngine.Random.Range(propSpacing.x, propSpacing.y);
                }
            }
            for (int i = props.Count - 1; i >= 0; i--)
            {
                var t = props[i];
                if (t == null || t.position.x < left) { if (t != null) Destroy(t.gameObject); props.RemoveAt(i); }
            }
            for (int i = fading.Count - 1; i >= 0; i--)
            {
                var t = fading[i];
                if (t == null) { fading.RemoveAt(i); continue; }
                bool gone = true;
                foreach (var sr in t.GetComponentsInChildren<SpriteRenderer>())
                {
                    var col = sr.color; col.a = Mathf.Max(0f, col.a - Time.deltaTime * 1.2f); sr.color = col;
                    if (col.a > 0f) gone = false;
                }
                if (gone) { Destroy(t.gameObject); fading.RemoveAt(i); }
            }
            // ghosts bob, lantern posts sway a little
            foreach (var t in props)
            {
                if (t == null) continue;
                if (t.name.Contains("ghost")) t.position = new Vector3(t.position.x, groundY + 0.9f + Mathf.Sin(Time.time * 2f + t.position.x) * 0.15f, 0f);
            }
        }

        void SpawnProp(FestivalArt art, float x)
        {
            var sp = art.groundProps[UnityEngine.Random.Range(0, art.groundProps.Length)];
            if (sp == null) return;
            var holder = new GameObject("Festive_" + sp.name).transform;
            holder.SetParent(root, false);
            holder.position = new Vector3(x, groundY, 0f);
            int count = sp.name.Contains("diya") || sp.name.Contains("egg") ? UnityEngine.Random.Range(2, 4) : 1;
            for (int i = 0; i < count; i++)
                AddSprite(holder, sp, new Vector2(i * 0.75f, 0f), sp.name.Contains("ghost") ? 0.9f : 0f, art);
            if (sp.name.Contains("xmas_tree"))   // presents under the tree
                foreach (var g in art.groundProps)
                    if (g != null && g.name.Contains("gift")) AddSprite(holder, g, new Vector2(UnityEngine.Random.Range(-0.9f, 0.9f), 0f), 0f, art, -5);
            props.Add(holder);
        }

        void AddSprite(Transform holder, Sprite sp, Vector2 offset, float lift, FestivalArt art, int order = -6)
        {
            var sr = new GameObject(sp.name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(holder, false);
            sr.sprite = sp;
            sr.sortingOrder = order;
            sr.transform.localPosition = new Vector3(offset.x, offset.y + lift - sp.bounds.min.y, 0f);   // bottom on the ground
            sr.color = new Color(1f, 1f, 1f, 1f);
            bool glows = sp.name.Contains("diya") || sp.name.Contains("lantern") || sp.name.Contains("jack") || sp.name.Contains("kandil") || sp.name.Contains("xmas_tree");
            if (glows && glow != null)
            {
                var g = new GameObject("Glow").AddComponent<SpriteRenderer>();
                g.transform.SetParent(sr.transform, false);
                g.sprite = glow;
                g.sortingOrder = 91;   // above the dusk dimming so it really glows at night
                float s = Mathf.Max(sp.bounds.size.x, sp.bounds.size.y) * 1.6f / Mathf.Max(0.01f, glow.bounds.size.x);
                g.transform.localScale = Vector3.one * s;
                g.color = new Color(art.glowColor.r, art.glowColor.g, art.glowColor.b, 0.35f);
            }
        }

        // ── sky: fireworks, lanterns, bats ───────

        void UpdateSky(Vector3 c, float w, float h)
        {
            if (Art(f => f.fireworks))
            {
                fireworkTimer -= Time.deltaTime * (rm != null && rm.Phase == RoundPhase.Running ? 1.5f : 1f);
                if (fireworkTimer <= 0f) { Firework(RandomSkyPoint(), 1f); fireworkTimer = UnityEngine.Random.Range(fireworkInterval.x, fireworkInterval.y); }
                if (rm != null && rm.Phase == RoundPhase.Running && !rm.IsWarmingUp)
                {
                    int m = Mathf.FloorToInt(rm.Multiplier);
                    if (m >= 2 && m > lastMilestone) { lastMilestone = m; Firework(RandomSkyPoint(), 1.2f); }
                }
            }
            if (Art(f => f.skyLanterns) && skyLantern != null)
            {
                lanternTimer -= Time.deltaTime;
                if (lanternTimer <= 0f)
                {
                    lanternTimer = UnityEngine.Random.Range(1.8f, 3.6f);
                    var fl = NewFloater(skyLantern, new Vector3(c.x + UnityEngine.Random.Range(-w, w * 1.2f), groundY + 1.2f, 0f), -43, true);
                    fl.v = new Vector2(UnityEngine.Random.Range(-0.15f, 0.1f), UnityEngine.Random.Range(0.35f, 0.6f));
                    fl.life = 14f;
                }
            }
            if (Art(f => f.bats) && batA != null)
            {
                batTimer -= Time.deltaTime;
                if (batTimer <= 0f)
                {
                    batTimer = UnityEngine.Random.Range(3f, 6f);
                    float y = c.y + h * UnityEngine.Random.Range(0.2f, 0.6f);
                    for (int i = 0; i < 3; i++)
                    {
                        var fl = NewFloater(batA, new Vector3(c.x - w - 1f - i * 0.7f, y + UnityEngine.Random.Range(-0.4f, 0.4f), 0f), -27, false);
                        fl.v = new Vector2(UnityEngine.Random.Range(2.2f, 3f), 0f);
                        fl.flap = true;
                        fl.life = 9f;
                    }
                }
            }
        }

        Vector3 RandomSkyPoint()
        {
            if (cam == null) return Vector3.zero;
            float h = cam.orthographicSize, w = h * cam.aspect;
            var c = cam.transform.position;
            return new Vector3(c.x + UnityEngine.Random.Range(-w * 0.85f, w * 0.85f), c.y + h * UnityEngine.Random.Range(0.25f, 0.75f), 0f);
        }

        public void Firework(Vector3 at, float power)
        {
            var art = CurrentArt;
            Color col = art != null && art.fireworkColors.Length > 0 ? art.fireworkColors[UnityEngine.Random.Range(0, art.fireworkColors.Length)] : new Color(1f, 0.8f, 0.3f);
            int n = Mathf.RoundToInt(26 * power);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n + UnityEngine.Random.Range(-0.05f, 0.05f);
                float sp = UnityEngine.Random.Range(1.6f, 2.6f) * power;
                FxPool.Spawn(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp, col, 1f, UnityEngine.Random.Range(0.9f, 1.4f), -1.2f, -44, dot);
            }
            for (int i = 0; i < 8; i++)
                FxPool.Spawn(at, UnityEngine.Random.insideUnitCircle * 1.2f, Color.white, 1f, 0.6f, -0.6f, -43, spark);
        }

        Floater NewFloater(Sprite sp, Vector3 pos, int order, bool glows)
        {
            var sr = new GameObject("SkyFloater").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(root, false);
            sr.sprite = sp;
            sr.sortingOrder = order;
            sr.transform.position = pos;
            if (glows && glow != null)
            {
                var g = new GameObject("Glow").AddComponent<SpriteRenderer>();
                g.transform.SetParent(sr.transform, false);
                g.sprite = glow;
                g.sortingOrder = order - 1;
                g.transform.localScale = Vector3.one * (sp.bounds.size.y * 2.2f / Mathf.Max(0.01f, glow.bounds.size.y));
                g.color = new Color(1f, 0.7f, 0.3f, 0.45f);
            }
            var f = new Floater { t = sr.transform, sr = sr, phase = UnityEngine.Random.value * 10f };
            floaters.Add(f);
            return f;
        }

        void UpdateFloaters()
        {
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                if (f.t == null) { floaters.RemoveAt(i); continue; }
                f.age += Time.deltaTime;
                f.t.position += (Vector3)(f.v * Time.deltaTime) + new Vector3(Mathf.Sin(f.age * 1.5f + f.phase) * 0.004f, 0f, 0f);
                if (f.flap) f.sr.sprite = Mathf.Repeat(f.age * 8f, 2f) < 1f ? batA : (batB != null ? batB : batA);
                float a = Mathf.Clamp01(Mathf.Min(f.age / 0.6f, (f.life - f.age) / 1.5f));
                foreach (var sr in f.t.GetComponentsInChildren<SpriteRenderer>())
                {
                    var col = sr.color; col.a = sr == f.sr ? a : a * 0.45f; sr.color = col;
                }
                if (f.age >= f.life) { Destroy(f.t.gameObject); floaters.RemoveAt(i); }
            }
        }

        // ── helpers ──────────────────────────────

        SpriteRenderer MakeQuad(string n, int order, Color col)
        {
            var sr = new GameObject(n).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(root, false);
            sr.sprite = FxPool.Square;
            sr.sortingOrder = order;
            sr.color = col;
            return sr;
        }

        static void Cover(SpriteRenderer sr, Vector3 c, float w, float h, Color col)
        {
            if (sr == null) return;
            sr.color = col;
            sr.enabled = col.a > 0.001f;
            var b = sr.sprite != null ? sr.sprite.bounds.size : Vector3.one;
            sr.transform.position = new Vector3(c.x, c.y, 0f);
            sr.transform.localScale = new Vector3(w * 2.2f / Mathf.Max(0.001f, b.x), h * 2.2f / Mathf.Max(0.001f, b.y), 1f);
        }
    }
}
