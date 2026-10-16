using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace KittyCrash
{
    /// <summary>
    /// FOUR SEASONS, FOUR RULES (needs the KittyAPI season_rules.py update + USE API).
    ///  • While betting: the server's forecast for the next walk. The whole world spins through the seasons
    ///    like a slot reel and lands on it, and a rule card explains what this season changes.
    ///      SPRING  timing       golden butterfly flutters by: cash out while it's here = +25%
    ///      SUMMER  speed        Summer Zoomies: 40% chance the multiplier climbs way faster
    ///      FALL    information  leaf storms hide the multiplier: trust your gut
    ///      WINTER  patience     slow, sleepy walk: cash out at 2.00x+ = +10% hot cocoa bonus
    ///  • While walking: the events happen live in the scene, in sync with the server (and the phones).
    /// Put it on the Managers object.
    /// </summary>
    public class SeasonRules : MonoBehaviour
    {
        /// <summary>Live season moments for sound/FX: "butterfly", "leafstorm", "cocoa", "zoomies", "bonus".</summary>
        public static event Action<string> OnEvent;

        [Tooltip("Extra full cycles the season reel spins before landing.")]
        public int spinLaps = 1;
        public float spinSeconds = 2.4f;
        [Tooltip("Where the big multiplier is (top-centre of the game area). The leaf storm covers this spot.")]
        public Vector2 multiplierCenter = new Vector2(0f, -210f);
        public Vector2 multiplierSize = new Vector2(560f, 170f);
        [Tooltip("Rule card position (bottom-centre of the game area).")]
        public Vector2 cardPosition = new Vector2(0f, 34f);

        // ── server data ──
        [Serializable] class SeasonDto { public string season; public string title; public string axis; public string rule; }
        [Serializable] class ScheduleDto { public string round_id; public string season; public bool rush; public float growth_rate; public float lead_in_seconds; public bool rules_on; public float[] butterfly; public float[] leaf_storms; }

        static readonly Dictionary<string, Color> SeasonColor = new Dictionary<string, Color>
        {
            { "spring", new Color(1f, 0.62f, 0.82f) },
            { "summer", new Color(1f, 0.82f, 0.24f) },
            { "fall",   new Color(1f, 0.55f, 0.24f) },
            { "winter", new Color(0.62f, 0.82f, 1f) },
        };

        RoundManager rm;
        ApiBackend api;
        Camera cam;
        CatWalker cat;
        Font font;

        SeasonDto forecast;            // next walk (shown while betting)
        ScheduleDto schedule;          // this walk
        string scheduleFor;
        bool butterflyWas, cocoaWas, stormWas, rushWas;
        float cardPop, bannerT;

        // ── UI ──
        RectTransform area, card, pill, cover, fxLayer, butterfly;
        Image cardBg, pillBg, butterflyImg;
        Text cardSeason, cardTitle, cardRule, pillText, banner, coverText;
        Sprite[] butterflyFrames;
        Sprite leafSprite;
        readonly List<RectTransform> coverLeaves = new List<RectTransform>();
        readonly List<Leaf> stormLeaves = new List<Leaf>();
        readonly List<Spark> sparks = new List<Spark>();

        class Leaf { public RectTransform rt; public Vector2 pos, vel; public float spin; }
        class Spark { public RectTransform rt; public Image img; public Vector2 vel; public float t; }

        void Start()
        {
            rm = RoundManager.Instance != null ? RoundManager.Instance : GetComponent<RoundManager>();
            cam = Camera.main;
            font = Resources.Load<Font>("KittyCrashFonts/Fredoka-Bold");
            if (font == null)
            {
#if UNITY_2022_2_OR_NEWER
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            }
#if UNITY_2023_1_OR_NEWER
            var critters = FindAnyObjectByType<CritterSpawner>();
            cat = FindAnyObjectByType<CatWalker>();
#else
            var critters = FindObjectOfType<CritterSpawner>();
            cat = FindObjectOfType<CatWalker>();
#endif
            if (critters != null && critters.butterflyFrames != null && critters.butterflyFrames.Length >= 2)
                butterflyFrames = new[] { critters.butterflyFrames[0], critters.butterflyFrames[1] };
            var ss = SeasonSystem.Instance;
            if (ss != null) leafSprite = ss.Get(Season.Autumn).weatherSprite;

            Build();
            if (rm != null)
            {
                rm.OnPhaseChanged += PhaseChanged;
                rm.OnCashOut += CashedOut;
                if (rm.Phase == RoundPhase.Idle) PhaseChanged(RoundPhase.Idle);
            }
        }

        void OnDestroy()
        {
            if (rm == null) return;
            rm.OnPhaseChanged -= PhaseChanged;
            rm.OnCashOut -= CashedOut;
        }

        bool Online()
        {
            if (rm == null || !rm.useApi) return false;
            if (api == null) api = rm.apiBackend != null ? rm.apiBackend : rm.GetComponent<ApiBackend>();
            return api != null && !string.IsNullOrEmpty(api.SessionId);
        }

        // ── round flow ─────────────────────────────

        void PhaseChanged(RoundPhase p)
        {
            if (p == RoundPhase.Idle)
            {
                schedule = null;
                scheduleFor = null;
                if (!Online())
                {
                    forecast = null;
                    if (SeasonSystem.Instance != null && SeasonSystem.Instance.externalControl) SeasonSystem.Instance.ReleaseControl();
                    return;
                }
                StartCoroutine(Get("/api/season/next", text =>
                {
                    var f = JsonUtility.FromJson<SeasonDto>(text);
                    if (f == null || string.IsNullOrEmpty(f.season)) return;
                    forecast = f;
                    cardPop = 0f;
                    if (SeasonSystem.Instance != null) SeasonSystem.Instance.SpinTo(ToSeason(f.season), spinLaps, spinSeconds);
                }));
            }
        }

        void NextSeasonPicked(string text)
        {
            var f = JsonUtility.FromJson<SeasonDto>(text);
            if (f == null || string.IsNullOrEmpty(f.season)) return;
            forecast = f;
            cardPop = 0f;
            if (rm.Phase == RoundPhase.Idle && SeasonSystem.Instance != null) SeasonSystem.Instance.SpinTo(ToSeason(f.season), 0, 0.9f);
            else ShowBanner("NEXT WALK: " + Pretty(f.season), Col(f.season));
        }

        void FetchSchedule(string roundId)
        {
            scheduleFor = roundId;
            StartCoroutine(Get("/api/rounds/" + roundId + "/season", text =>
            {
                var s = JsonUtility.FromJson<ScheduleDto>(text);
                if (s == null || s.round_id != rm.RoundId) return;
                schedule = s;
                // land on the walk's real season (if the forecast was skipped, e.g. first round)
                var ss = SeasonSystem.Instance;
                if (ss != null && !ss.IsSpinning && ss.DominantSeason != ToSeason(s.season)) ss.SpinTo(ToSeason(s.season), 0, 1.2f);
            }));
        }

        void CashedOut(float multiplier, decimal payout)
        {
            if (schedule == null || !schedule.rules_on) return;
            float t = rm.RunElapsed;
            if (schedule.season == "spring" && Inside(schedule.butterfly, 0, t)) { ShowBanner("BUTTERFLY BONUS  +25%", SeasonColor["spring"]); OnEvent?.Invoke("bonus"); }
            else if (schedule.season == "winter" && multiplier >= 2f) { ShowBanner("HOT COCOA BONUS  +10%", SeasonColor["winter"]); OnEvent?.Invoke("bonus"); }
        }

        // ── every frame ────────────────────────────

        void Update()
        {
            // N on the game screen = choose the next walk's season (the server decides, so phones follow instantly)
            if (InputReader.DebugNextSeason() && Online()) StartCoroutine(Request("POST", "/api/season/next", "{}", NextSeasonPicked));

            if (cam == null) cam = Camera.main;
            if (cam != null) { area.anchorMin = new Vector2(0f, cam.rect.y); area.anchorMax = new Vector2(1f, Mathf.Min(1f, cam.rect.y + cam.rect.height)); }

            bool running = rm != null && rm.Phase == RoundPhase.Running;
            if (running && rm.HasBet && !string.IsNullOrEmpty(rm.RoundId) && scheduleFor != rm.RoundId && Online()) FetchSchedule(rm.RoundId);

            float t = rm != null ? rm.RunElapsed : 0f;
            bool live = running && schedule != null && schedule.rules_on && schedule.round_id == rm.RoundId;
            bool fly = live && schedule.season == "spring" && Inside(schedule.butterfly, 0, t);
            bool storm = live && schedule.season == "fall" && InsideAny(schedule.leaf_storms, t);
            bool warm = rm != null && rm.IsWarmingUp;
            bool rush = live && schedule.season == "summer" && schedule.rush && !warm;
            bool cocoa = live && schedule.season == "winter" && rm.Multiplier >= 2f && !rm.CashedOut;

            UpdateCard(running, live);
            UpdateButterfly(fly);
            UpdateStorm(storm);

            if (fly && !butterflyWas && rm.HasBet && !rm.CashedOut) ShowBanner("GOLDEN BUTTERFLY!  CASH OUT NOW  +25%", SeasonColor["spring"]);
            if (cocoa && !cocoaWas) ShowBanner("HOT COCOA ZONE  +10%", SeasonColor["winter"]);
            if (fly && !butterflyWas) OnEvent?.Invoke("butterfly");
            if (storm && !stormWas) OnEvent?.Invoke("leafstorm");
            if (cocoa && !cocoaWas) OnEvent?.Invoke("cocoa");
            if (rush && !rushWas) OnEvent?.Invoke("zoomies");
            butterflyWas = fly;
            cocoaWas = cocoa;
            stormWas = storm;
            rushWas = rush;
            if (rush && Mathf.Repeat(Time.time, 0.6f) < 0.3f && bannerT > 1.6f) { banner.text = "SUMMER ZOOMIES!"; banner.color = SeasonColor["summer"]; banner.enabled = true; }
            else if (bannerT > 1.6f) banner.enabled = false;

            bannerT += Time.deltaTime;
            if (bannerT < 1.6f)
            {
                float s = bannerT < 0.15f ? Mathf.Lerp(0.6f, 1.15f, bannerT / 0.15f) : Mathf.Lerp(1.15f, 1f, (bannerT - 0.15f) * 4f);
                banner.rectTransform.localScale = Vector3.one * s;
            }
            UpdateSparks();
        }

        void UpdateCard(bool running, bool live)
        {
            // Betting: big card with the forecast. Walking: a small pill with this walk's rule.
            bool showCard = !running && forecast != null && rm != null && rm.Phase == RoundPhase.Idle;
            card.gameObject.SetActive(showCard);
            if (showCard)
            {
                var col = Col(forecast.season);
                bool spinning = SeasonSystem.Instance != null && SeasonSystem.Instance.IsSpinning;
                cardSeason.text = spinning ? "SPINNING THE SEASONS..." : "NEXT WALK:  " + Pretty(forecast.season);
                cardSeason.color = spinning ? Color.white : col;
                cardTitle.text = spinning ? "" : forecast.title.ToUpperInvariant() + "   -   changes the " + forecast.axis.ToUpperInvariant();
                cardRule.text = spinning ? "" : forecast.rule;
                cardBg.color = new Color(col.r * 0.18f, col.g * 0.18f, col.b * 0.22f, 0.92f);
                cardPop += Time.deltaTime;
                float s = spinning ? 0.96f + Mathf.Sin(Time.time * 18f) * 0.02f : (cardPop < 0.2f ? 1f : Mathf.Lerp(1.08f, 1f, Mathf.Clamp01((cardPop - 0.2f) * 5f)));
                card.localScale = Vector3.one * s;
                if (spinning) cardPop = 0.2f;
            }

            bool showPill = running && live;
            pill.gameObject.SetActive(showPill);
            if (showPill)
            {
                var col = Col(schedule.season);
                pillText.text = Pretty(schedule.season) + "  -  " + RuleShort(schedule);
                pillText.color = col;
                pillBg.color = new Color(col.r * 0.15f, col.g * 0.15f, col.b * 0.2f, 0.85f);
                pill.sizeDelta = new Vector2(pillText.preferredWidth + 36f, 36f);
            }
        }

        void UpdateButterfly(bool on)
        {
            butterfly.gameObject.SetActive(on);
            if (!on) return;
            Vector2 head = CatHead();
            float tt = Time.time;
            float ui = CatSize.Ui;
            Vector2 pos = head + new Vector2(110f + Mathf.Sin(tt * 2.3f) * 70f, 60f + Mathf.Sin(tt * 4.1f) * 26f) * ui;
            butterfly.anchoredPosition = pos;
            if (butterflyFrames != null) butterflyImg.sprite = butterflyFrames[Mathf.FloorToInt(tt * 10f) % 2];
            float glow = 0.85f + Mathf.Sin(tt * 12f) * 0.15f;
            butterflyImg.color = new Color(1f, 0.85f * glow + 0.1f, 0.25f, 1f);
            butterfly.localScale = Vector3.one * (1.1f + Mathf.Sin(tt * 6f) * 0.08f) * ui;
            if (UnityEngine.Random.value < Time.deltaTime * 30f) Sparkle(pos);
        }

        void UpdateStorm(bool on)
        {
            cover.gameObject.SetActive(on);
            if (on)
            {
                float tt = Time.time;
                for (int i = 0; i < coverLeaves.Count; i++)
                {
                    var l = coverLeaves[i];
                    float a = tt * (0.8f + (i % 5) * 0.25f) + i * 1.7f;
                    l.anchoredPosition = new Vector2(Mathf.Sin(a) * multiplierSize.x * 0.42f, Mathf.Cos(a * 1.3f) * multiplierSize.y * 0.36f);
                    l.localRotation = Quaternion.Euler(0f, 0f, a * 90f);
                }
                coverText.text = "?.??x";
            }

            // gusts of leaves across the whole scene
            if (on && stormLeaves.Count < 70 && UnityEngine.Random.value < Time.deltaTime * 60f) SpawnStormLeaf();
            float w = fxLayer.rect.width, h = fxLayer.rect.height;
            for (int i = stormLeaves.Count - 1; i >= 0; i--)
            {
                var l = stormLeaves[i];
                l.pos += l.vel * Time.deltaTime;
                l.vel.y += Mathf.Sin(Time.time * 3f + i) * 40f * Time.deltaTime;
                l.rt.anchoredPosition = l.pos;
                l.rt.localRotation = Quaternion.Euler(0f, 0f, l.rt.localEulerAngles.z + l.spin * Time.deltaTime);
                if (l.pos.x < -w * 0.5f - 60f || l.pos.y < -h * 0.5f - 60f)
                {
                    Destroy(l.rt.gameObject);
                    stormLeaves.RemoveAt(i);
                }
            }
        }

        void SpawnStormLeaf()
        {
            float w = fxLayer.rect.width, h = fxLayer.rect.height;
            var rt = R("Leaf", fxLayer);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            if (leafSprite != null) img.sprite = leafSprite;
            Color[] cols = { new Color(1f, 0.55f, 0.24f), new Color(0.91f, 0.64f, 0.23f), new Color(0.77f, 0.31f, 0.16f), new Color(1f, 0.7f, 0.36f) };
            img.color = cols[UnityEngine.Random.Range(0, cols.Length)];
            float size = UnityEngine.Random.Range(18f, 34f);
            rt.sizeDelta = new Vector2(size, size * (leafSprite != null ? 1f : 0.6f));
            var l = new Leaf
            {
                rt = rt,
                pos = new Vector2(w * 0.5f + 40f, UnityEngine.Random.Range(-h * 0.3f, h * 0.5f)),
                vel = new Vector2(UnityEngine.Random.Range(-900f, -500f), UnityEngine.Random.Range(-160f, -40f)),
                spin = UnityEngine.Random.Range(-400f, 400f),
            };
            rt.anchoredPosition = l.pos;
            stormLeaves.Add(l);
        }

        void Sparkle(Vector2 at)
        {
            var rt = R("Spark", fxLayer);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 0.9f, 0.4f, 1f);
            rt.sizeDelta = new Vector2(7f, 7f);
            rt.anchoredPosition = at + UnityEngine.Random.insideUnitCircle * 14f;
            rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            sparks.Add(new Spark { rt = rt, img = img, vel = new Vector2(UnityEngine.Random.Range(-60f, -20f), UnityEngine.Random.Range(-40f, 20f)) });
        }

        void UpdateSparks()
        {
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var s = sparks[i];
                s.t += Time.deltaTime;
                s.rt.anchoredPosition += s.vel * Time.deltaTime;
                var c = s.img.color; c.a = 1f - s.t / 0.7f; s.img.color = c;
                if (s.t >= 0.7f) { Destroy(s.rt.gameObject); sparks.RemoveAt(i); }
            }
        }

        void ShowBanner(string text, Color color)
        {
            banner.text = text;
            banner.color = color;
            banner.enabled = true;
            bannerT = 0f;
        }

        // ── helpers ────────────────────────────────

        static bool Inside(float[] w, int i, float t) => w != null && w.Length >= i + 2 && t >= w[i] && t < w[i + 1];

        static bool InsideAny(float[] w, float t)
        {
            if (w == null) return false;
            for (int i = 0; i + 1 < w.Length; i += 2) if (t >= w[i] && t < w[i + 1]) return true;
            return false;
        }

        static Season ToSeason(string s)
        {
            switch (s)
            {
                case "summer": return Season.Summer;
                case "fall": return Season.Autumn;
                case "winter": return Season.Winter;
                default: return Season.Spring;
            }
        }

        static Color Col(string s) => s != null && SeasonColor.TryGetValue(s, out var c) ? c : Color.white;
        static string Pretty(string s) => string.IsNullOrEmpty(s) ? "" : s.ToUpperInvariant();

        static string RuleShort(ScheduleDto s)
        {
            switch (s.season)
            {
                case "spring": return "catch the golden butterfly: +25%";
                case "summer": return s.rush ? "ZOOMIES! climbing fast" : "no zoomies this time";
                case "fall": return "leaf storms hide the multiplier";
                case "winter": return "hold to 2.00x for +10% cocoa";
                default: return "";
            }
        }

        Vector2 CatHead()
        {
            if (cat == null || cam == null) return Vector2.zero;
            var sr = cat.visual != null ? cat.visual.GetComponent<SpriteRenderer>() : null;
            Vector3 world = sr != null ? new Vector3(sr.bounds.center.x, sr.bounds.max.y, 0f) : cat.transform.position + Vector3.up * 1.5f;
            Vector3 screen = PixelGrid.WorldToScreen(cam, world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(fxLayer, screen, null, out var local);
            return local;
        }

        IEnumerator Get(string path, Action<string> ok) => Request("GET", path, null, ok);

        IEnumerator Request(string method, string path, string body, Action<string> ok)
        {
            if (!Online()) yield break;
            using (var req = new UnityWebRequest(api.baseUrl.TrimEnd('/') + path, method))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                if (body != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                req.SetRequestHeader("X-Session-ID", api.SessionId);
                req.timeout = 8;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) ok(req.downloadHandler.text);
            }
        }

        // ── building the UI ────────────────────────

        void Build()
        {
            var cgo = new GameObject("SeasonCanvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 55;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            area = R("GameArea", cgo.transform);
            Stretch(area);
            fxLayer = R("Fx", area);
            Stretch(fxLayer);

            // forecast card (while betting)
            card = R("SeasonCard", area);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0f);
            card.anchoredPosition = cardPosition;
            card.sizeDelta = new Vector2(700f, 108f);
            cardBg = card.gameObject.AddComponent<Image>();
            cardBg.raycastTarget = false;
            card.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2f, -2f);
            cardSeason = T("Season", card, 27, Color.white);
            Place(cardSeason.rectTransform, -6f, 34f);
            cardTitle = T("Title", card, 18, new Color(1f, 0.95f, 0.85f));
            Place(cardTitle.rectTransform, -42f, 24f);
            cardRule = T("Rule", card, 18, new Color(0.85f, 0.84f, 0.95f));
            Place(cardRule.rectTransform, -70f, 26f);
            card.gameObject.SetActive(false);

            // rule pill (while walking)
            pill = R("SeasonPill", area);
            pill.anchorMin = pill.anchorMax = pill.pivot = new Vector2(0.5f, 0f);
            pill.anchoredPosition = cardPosition + new Vector2(0f, 6f);
            pillBg = pill.gameObject.AddComponent<Image>();
            pillBg.raycastTarget = false;
            pillText = T("Text", pill, 18, Color.white);
            Stretch(pillText.rectTransform);
            pill.gameObject.SetActive(false);

            // banner for live events
            banner = T("Banner", area, 32, Color.white);
            var br = banner.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(0.5f, 0f);
            br.anchoredPosition = cardPosition + new Vector2(0f, 74f);
            br.sizeDelta = new Vector2(1200f, 60f);
            banner.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3f, -3f);
            banner.enabled = false;

            // leaf storm cover over the big multiplier
            cover = R("LeafCover", area);
            cover.anchorMin = cover.anchorMax = cover.pivot = new Vector2(0.5f, 1f);
            cover.anchoredPosition = multiplierCenter + new Vector2(0f, multiplierSize.y * 0.5f);
            cover.sizeDelta = multiplierSize;
            var cbg = cover.gameObject.AddComponent<Image>();
            cbg.color = new Color(0.45f, 0.2f, 0.08f, 0.92f);
            cbg.raycastTarget = false;
            var leafHolder = R("Leaves", cover);
            leafHolder.anchorMin = leafHolder.anchorMax = new Vector2(0.5f, 0.5f);
            Color[] cols = { new Color(1f, 0.55f, 0.24f), new Color(0.91f, 0.64f, 0.23f), new Color(0.77f, 0.31f, 0.16f), new Color(1f, 0.72f, 0.36f) };
            for (int i = 0; i < 26; i++)
            {
                var l = R("Leaf", leafHolder);
                var img = l.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                if (leafSprite != null) img.sprite = leafSprite;
                img.color = cols[i % cols.Length];
                float size = 26f + (i % 4) * 8f;
                l.sizeDelta = new Vector2(size, size * (leafSprite != null ? 1f : 0.6f));
                coverLeaves.Add(l);
            }
            coverText = T("Text", cover, 78, new Color(1f, 0.8f, 0.5f));
            Stretch(coverText.rectTransform);
            coverText.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3f, -3f);
            cover.gameObject.SetActive(false);

            // golden butterfly
            butterfly = R("GoldenButterfly", fxLayer);
            butterfly.sizeDelta = new Vector2(64f, 64f);
            butterflyImg = butterfly.gameObject.AddComponent<Image>();
            butterflyImg.raycastTarget = false;
            butterflyImg.preserveAspect = true;
            if (butterflyFrames != null) butterflyImg.sprite = butterflyFrames[0];
            butterfly.gameObject.SetActive(false);
        }

        static RectTransform R(string n, Transform p)
        {
            var go = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(p, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

        static void Place(RectTransform r, float top, float height)
        {
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, top); r.sizeDelta = new Vector2(0f, height);
        }

        Text T(string n, Transform p, int size, Color c)
        {
            var t = R(n, p).gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.color = c; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
