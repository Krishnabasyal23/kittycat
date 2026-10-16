using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace KittyCrash
{
    /// <summary>
    /// The player's feelings, shown as a cat-face emoji in the top-right of the game view:
    ///   calm → happy → excited → nervous → anxious → PANIC as the multiplier (and the risk) climbs,
    ///   heart-eyes when you cash out, relieved while you watch the rest, crying when kitty gets distracted first.
    ///   A win streak makes it more anxious (greed!). Temptations near the cat spike it too.
    /// Also the "feel" layer for players and the audience:
    ///   • tension meter under the face   • heartbeat-red vignette around the game view when it gets scary
    ///   • milestone pop-ups (2x! 5x! 10x!) above the cat   • floating +payout / -wager numbers
    /// Builds its own UI at runtime; the installer fills in the face sprites.
    /// </summary>
    public class MoodFace : MonoBehaviour
    {
        [Header("Faces (Art/Mood)")]
        public Sprite calm;
        public Sprite happy, excited, nervous, anxious, panic, win, relieved, sad, surprised;

        [Header("Layout")]
        public float faceSize = 160f;
        public Vector2 margin = new Vector2(28f, 26f);
        [Range(0f, 1f)] public float vignetteStrength = 0.55f;
        public float[] milestones = { 2f, 3f, 5f, 10f, 25f, 50f, 100f };

        enum Mood { Calm, Happy, Excited, Nervous, Anxious, Panic, Win, Relieved, Sad, Surprised }

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        RoundManager rm;
        CatWalker cat;
        Camera cam;
        Font font, bold;

        RectTransform root, gameArea, faceRT, popRT, floatRT;
        Image faceImg, plateImg, vignetteImg, meterFill;
        Text label, subLabel, streakText, popText, floatText;

        Mood mood = Mood.Calm;
        float popT = 99f, faceChangeT = 99f, winHold, flashT = 99f, beat;
        Color flashColor;
        int nextMilestone;
        float floatT = 99f;
        Vector3 floatWorld, popWorld;

        void Start()
        {
            rm = RoundManager.Instance;
#if UNITY_2023_1_OR_NEWER
            cat = FindAnyObjectByType<CatWalker>();
            var ui = FindAnyObjectByType<CrashUI>();
#else
            cat = FindObjectOfType<CatWalker>();
            var ui = FindObjectOfType<CrashUI>();
#endif
            cam = Camera.main;
            font = ui != null && ui.font != null ? ui.font : DefaultFont();
            bold = ui != null && ui.boldFont != null ? ui.boldFont : font;
            Build();

            if (rm != null)
            {
                rm.OnRoundStarted += () => { nextMilestone = 0; winHold = 0f; };
                rm.OnCashOut += (m, payout) =>
                {
                    Tension.WinStreak++;
                    winHold = 2.2f;
                    Flash(new Color(1f, 0.82f, 0.25f), 0.5f);
                    Float("+" + payout.ToString("N2", Inv), new Color(1f, 0.85f, 0.3f));
                };
                rm.OnCrash += (c, d) =>
                {
                    if (rm.HasBet && !rm.CashedOut)
                    {
                        Tension.WinStreak = 0;
                        Flash(new Color(1f, 0.15f, 0.15f), 0.75f);
                        Float("-" + rm.Wager.ToString("N2", Inv), new Color(1f, 0.35f, 0.35f));
                    }
                };
            }
        }

        static Font DefaultFont()
        {
#if UNITY_2022_2_OR_NEWER
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }

        // ───────────────────────────── build ─────────────────────────────

        void Build()
        {
            var canvasGO = new GameObject("MoodCanvas", typeof(RectTransform));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 55;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            root = (RectTransform)canvasGO.transform;

            gameArea = NewRect("GameArea", root);
            Stretch(gameArea);

            // vignette (behind everything else in this canvas)
            vignetteImg = NewImage("Vignette", gameArea, VignetteSprite());
            Stretch(vignetteImg.rectTransform);
            vignetteImg.color = new Color(1f, 0.15f, 0.15f, 0f);

            // mood panel, top-right
            var panel = NewRect("MoodPanel", gameArea);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(-margin.x, -margin.y);
            panel.sizeDelta = new Vector2(faceSize + 70f, faceSize + 120f);

            plateImg = NewImage("Plate", panel, CircleSprite());
            var prt = plateImg.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.sizeDelta = new Vector2(faceSize + 34f, faceSize + 34f);
            prt.anchoredPosition = new Vector2(0f, 0f);
            plateImg.color = new Color(0.1f, 0.11f, 0.2f, 0.55f);

            faceImg = NewImage("Face", panel, calm);
            faceImg.preserveAspect = true;
            faceRT = faceImg.rectTransform;
            faceRT.anchorMin = faceRT.anchorMax = new Vector2(0.5f, 1f);
            faceRT.pivot = new Vector2(0.5f, 0.5f);
            faceRT.sizeDelta = new Vector2(faceSize, faceSize);
            faceRT.anchoredPosition = new Vector2(0f, -(faceSize + 34f) / 2f);

            label = NewText("Label", panel, bold, 30, TextAnchor.UpperCenter);
            Place(label.rectTransform, -(faceSize + 40f), 40f);
            AddOutline(label.gameObject);

            // tension meter
            var meterBg = NewImage("MeterBg", panel, WhiteSprite());
            meterBg.color = new Color(0f, 0f, 0f, 0.45f);
            var mrt = meterBg.rectTransform;
            mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 1f);
            mrt.pivot = new Vector2(0.5f, 1f);
            mrt.sizeDelta = new Vector2(faceSize + 20f, 12f);
            mrt.anchoredPosition = new Vector2(0f, -(faceSize + 82f));
            meterFill = NewImage("MeterFill", mrt, WhiteSprite());
            meterFill.type = Image.Type.Filled;
            meterFill.fillMethod = Image.FillMethod.Horizontal;
            meterFill.fillOrigin = 0;
            Stretch(meterFill.rectTransform);
            meterFill.fillAmount = 0f;

            subLabel = NewText("Sub", panel, font, 20, TextAnchor.UpperCenter);
            Place(subLabel.rectTransform, -(faceSize + 98f), 28f);
            AddOutline(subLabel.gameObject);
            subLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

            streakText = NewText("Streak", panel, bold, 20, TextAnchor.UpperCenter);
            Place(streakText.rectTransform, -(faceSize + 124f), 28f);
            streakText.color = new Color(1f, 0.7f, 0.25f);
            streakText.horizontalOverflow = HorizontalWrapMode.Overflow;
            AddOutline(streakText.gameObject);

            // pop-ups that follow the cat
            popText = NewText("Milestone", gameArea, bold, 64, TextAnchor.MiddleCenter);
            popRT = popText.rectTransform;
            popRT.sizeDelta = new Vector2(400f, 90f);
            popRT.anchorMin = popRT.anchorMax = new Vector2(0.5f, 0.5f);
            AddOutline(popText.gameObject, 3f);
            popText.enabled = false;

            floatText = NewText("FloatNumber", gameArea, bold, 46, TextAnchor.MiddleCenter);
            floatRT = floatText.rectTransform;
            floatRT.sizeDelta = new Vector2(500f, 70f);
            floatRT.anchorMin = floatRT.anchorMax = new Vector2(0.5f, 0.5f);
            AddOutline(floatText.gameObject, 3f);
            floatText.enabled = false;
        }

        // ───────────────────────────── update ─────────────────────────────

        void LateUpdate()
        {
            if (rm == null) { rm = RoundManager.Instance; if (rm == null) return; }
            if (cam == null) cam = Camera.main;

            // keep to the game view (above the betting dock)
            if (cam != null)
            {
                float y0 = cam.rect.y;
                gameArea.anchorMin = new Vector2(0f, y0);
                gameArea.anchorMax = new Vector2(1f, Mathf.Min(1f, cam.rect.y + cam.rect.height));
            }

            float tension = Tension.Of(rm);
            Mood m = Decide(tension);
            if (m != mood) { mood = m; faceChangeT = 0f; var sp = SpriteFor(m); if (sp != null) faceImg.sprite = sp; }

            // heartbeat: faster as tension rises
            float bpm = 60f + 110f * tension;
            beat += Time.deltaTime * bpm / 60f;
            float ph = beat - Mathf.Floor(beat);
            float thump = Mathf.Exp(-ph * 9f) + 0.6f * Mathf.Exp(-Mathf.Abs(ph - 0.22f) * 14f);

            // face motion: pop on change, heartbeat, shake when anxious, bob when happy
            faceChangeT += Time.deltaTime;
            float pop = faceChangeT < 0.25f ? 1f + 0.25f * Mathf.Sin(faceChangeT / 0.25f * Mathf.PI) : 1f;
            float beatScale = 1f + thump * 0.06f * tension;
            float shake = tension * tension * 9f;
            Vector2 jitter = shake > 0.3f
                ? new Vector2((Mathf.PerlinNoise(Time.time * 30f, 0f) - 0.5f) * 2f, (Mathf.PerlinNoise(0f, Time.time * 30f) - 0.5f) * 2f) * shake
                : Vector2.zero;
            float bob = (mood == Mood.Happy || mood == Mood.Win || mood == Mood.Excited) ? Mathf.Sin(Time.time * 6f) * 4f : 0f;
            if (mood == Mood.Sad) bob = -4f + Mathf.Sin(Time.time * 2f) * 2f;
            faceRT.localScale = Vector3.one * pop * beatScale;
            faceRT.anchoredPosition = new Vector2(jitter.x, -(faceSize + 34f) / 2f + jitter.y + bob);
            faceRT.localRotation = Quaternion.Euler(0f, 0f, mood == Mood.Win ? Mathf.Sin(Time.time * 8f) * 6f : jitter.x * 0.4f);

            // plate + meter colours
            Color tc = TensionColor(tension);
            Color plate = mood == Mood.Win || mood == Mood.Relieved ? new Color(1f, 0.8f, 0.3f, 0.5f)
                        : mood == Mood.Sad ? new Color(0.35f, 0.5f, 0.9f, 0.5f)
                        : new Color(tc.r * 0.6f, tc.g * 0.6f, tc.b * 0.6f, 0.35f + 0.25f * tension);
            plateImg.color = Color.Lerp(plateImg.color, plate, Time.deltaTime * 6f);
            meterFill.fillAmount = Mathf.Lerp(meterFill.fillAmount, tension, Time.deltaTime * 8f);
            meterFill.color = tc;

            // texts
            label.text = LabelFor(mood);
            label.color = mood == Mood.Sad ? new Color(0.6f, 0.75f, 1f) : mood == Mood.Win || mood == Mood.Relieved ? new Color(1f, 0.85f, 0.35f) : Color.Lerp(Color.white, tc, tension);
            subLabel.text = SubFor(tension);
            streakText.text = Tension.WinStreak >= 2 ? $"Win streak ×{Tension.WinStreak} - greed rising!" : "";

            // vignette: heartbeat when scary, flashes on win / loss
            float va = Mathf.Pow(tension, 1.5f) * vignetteStrength * (0.55f + 0.45f * thump);
            Color vc = new Color(1f, 0.12f, 0.12f, va);
            if (flashT < 1f)
            {
                flashT += Time.deltaTime / 0.9f;
                float fa = (1f - flashT) * flashColor.a;
                if (fa > va) vc = new Color(flashColor.r, flashColor.g, flashColor.b, fa);
            }
            vignetteImg.color = vc;

            UpdateMilestones();
            UpdateFloaters();
            if (winHold > 0f) winHold -= Time.deltaTime;
        }

        Mood Decide(float t)
        {
            switch (rm.Phase)
            {
                case RoundPhase.Running:
                    if (rm.IsWarmingUp) return rm.HasBet ? Mood.Happy : Mood.Calm;
                    if (rm.CashedOut) return winHold > 0f ? Mood.Win : Mood.Relieved;
                    if (!rm.HasBet) return t > 0.45f ? Mood.Nervous : Mood.Calm;
                    if (t < 0.15f) return Mood.Happy;
                    if (t < 0.38f) return Mood.Excited;
                    if (t < 0.55f) return Mood.Nervous;
                    if (t < 0.72f) return Mood.Anxious;
                    return Mood.Panic;
                case RoundPhase.Ended:
                    if (rm.HasBet && !rm.CashedOut) return Mood.Sad;
                    if (rm.CashedOut) return winHold > 0f ? Mood.Win : Mood.Relieved;
                    return Mood.Surprised;
                case RoundPhase.Starting:
                    return Mood.Happy;
                default:
                    if (rm.BetQueued) return Tension.WinStreak >= 2 ? Mood.Excited : Mood.Happy;
                    return Mood.Calm;
            }
        }

        string LabelFor(Mood m)
        {
            switch (m)
            {
                case Mood.Happy: return rm.Phase == RoundPhase.Running ? "Feeling good" : "Bet's in!";
                case Mood.Excited: return rm.Phase == RoundPhase.Running ? "Ooh, climbing!" : "Feeling lucky!";
                case Mood.Nervous: return "Getting nervous...";
                case Mood.Anxious: return "Cash out?!";
                case Mood.Panic: return "PANIC!!";
                case Mood.Win: return "YESSS!";
                case Mood.Relieved: return "Phew, banked it";
                case Mood.Sad: return "Nooo...";
                case Mood.Surprised: return "Whoa!";
                default: return rm.Phase == RoundPhase.Running ? "Just watching" : "Chillin'";
            }
        }

        string SubFor(float tension)
        {
            if (Time.time - CatTemptations.LastTemptTime < 1.4f && rm.Phase == RoundPhase.Running && !rm.CashedOut)
                return "Ignore " + CatTemptations.LastTemptName + ", kitty!";
            switch (rm.Phase)
            {
                case RoundPhase.Running:
                    if (rm.CashedOut) return "Won " + rm.Payout.ToString("N2", Inv);
                    if (!rm.HasBet || rm.IsWarmingUp) return "";
                    return "Risk " + Mathf.RoundToInt(Mathf.Clamp01(1f - 0.96f / Mathf.Max(1f, rm.Multiplier)) * 100f) + "%  -  " + rm.PotentialPayout.ToString("N2", Inv);
                case RoundPhase.Ended:
                    if (rm.HasBet && !rm.CashedOut) return "Lost " + rm.Wager.ToString("N2", Inv);
                    return "Kitty stopped at " + rm.Multiplier.ToString("0.00", Inv) + "x";
                default:
                    return rm.BetQueued ? "Waiting for kitty..." : "Place a bet!";
            }
        }

        Sprite SpriteFor(Mood m)
        {
            switch (m)
            {
                case Mood.Happy: return happy;
                case Mood.Excited: return excited;
                case Mood.Nervous: return nervous;
                case Mood.Anxious: return anxious;
                case Mood.Panic: return panic;
                case Mood.Win: return win;
                case Mood.Relieved: return relieved;
                case Mood.Sad: return sad;
                case Mood.Surprised: return surprised;
                default: return calm;
            }
        }

        static Color TensionColor(float t)
        {
            var green = new Color(0.35f, 0.9f, 0.5f);
            var yellow = new Color(1f, 0.85f, 0.3f);
            var red = new Color(1f, 0.3f, 0.25f);
            return t < 0.5f ? Color.Lerp(green, yellow, t * 2f) : Color.Lerp(yellow, red, (t - 0.5f) * 2f);
        }

        void Flash(Color c, float alpha) { flashColor = new Color(c.r, c.g, c.b, alpha); flashT = 0f; }

        // ───────────────────────────── pop-ups ─────────────────────────────

        void UpdateMilestones()
        {
            if (rm.Phase == RoundPhase.Running && !rm.IsWarmingUp && milestones != null)
            {
                while (nextMilestone < milestones.Length && rm.Multiplier >= milestones[nextMilestone])
                {
                    float ms = milestones[nextMilestone];
                    nextMilestone++;
                    if (rm.Multiplier < ms * 1.15f) // don't fire a backlog after a catch-up jump
                    {
                        popText.text = ms.ToString("0", Inv) + "x!";
                        popText.color = TensionColor(Mathf.Clamp01(Mathf.Log(ms) / Mathf.Log(10f)));
                        popT = 0f;
                        popWorld = CatHead();
                    }
                }
            }

            if (popT < 1.3f)
            {
                popT += Time.deltaTime;
                popText.enabled = true;
                float k = popT / 1.3f;
                float s = popT < 0.18f ? Mathf.Lerp(0.3f, 1.3f, popT / 0.18f) : Mathf.Lerp(1.3f, 1f, Mathf.Clamp01((popT - 0.18f) / 0.15f));
                popRT.localScale = Vector3.one * s;
                PlaceAtWorld(popRT, popWorld, 60f + k * 90f);
                var c = popText.color; c.a = 1f - Mathf.Clamp01((k - 0.6f) / 0.4f); popText.color = c;
            }
            else popText.enabled = false;
        }

        void Float(string text, Color c)
        {
            floatText.text = text;
            floatText.color = c;
            floatT = 0f;
            floatWorld = CatHead();
        }

        void UpdateFloaters()
        {
            if (floatT < 1.8f)
            {
                floatT += Time.deltaTime;
                floatText.enabled = true;
                float k = floatT / 1.8f;
                PlaceAtWorld(floatRT, floatWorld, 30f + k * 120f);
                floatRT.localScale = Vector3.one * (floatT < 0.15f ? Mathf.Lerp(0.5f, 1.15f, floatT / 0.15f) : 1f);
                var c = floatText.color; c.a = 1f - Mathf.Clamp01((k - 0.65f) / 0.35f); floatText.color = c;
            }
            else floatText.enabled = false;
        }

        Vector3 CatHead()
        {
            if (cat == null) return Vector3.zero;
            var sr = cat.visual != null ? cat.visual.GetComponent<SpriteRenderer>() : null;
            return sr != null ? new Vector3(sr.bounds.center.x, sr.bounds.center.y + sr.bounds.extents.y * 0.3f, 0f) : cat.transform.position + Vector3.up * 1.2f;
        }

        void PlaceAtWorld(RectTransform rt, Vector3 world, float upPixels)
        {
            if (cam == null) return;
            // follow the cat horizontally (camera moves), keep the height where it started
            if (cat != null) world.x = Mathf.Lerp(world.x, CatHead().x, 0.85f);
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(gameArea, sp, null, out var local))
                rt.anchoredPosition = local + new Vector2(0f, upPixels);
        }

        // ───────────────────────────── helpers ─────────────────────────────

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Image NewImage(string name, Transform parent, Sprite sprite)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        static Text NewText(string name, Transform parent, Font f, int size, TextAnchor align)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = f;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static void Place(RectTransform rt, float y, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(0f, h);
        }

        static void AddOutline(GameObject go, float d = 2f)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(0.08f, 0.06f, 0.14f, 0.85f);
            o.effectDistance = new Vector2(d, -d);
        }

        static Sprite white, circle, vignette;

        static Sprite WhiteSprite()
        {
            if (white != null) return white;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px); tex.Apply();
            white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            return white;
        }

        static Sprite CircleSprite()
        {
            if (circle != null) return circle;
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) * N * 0.5f);                 // crisp edge, anti-aliased
                    float glow = Mathf.Clamp01(1f - d) * 0.35f;
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Max(a * 0.9f, glow) * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return circle;
        }

        static Sprite VignetteSprite()
        {
            if (vignette != null) return vignette;
            const int W = 256, H = 144;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f) / W * 2f - 1f, dy = (y + 0.5f) / H * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.1f);
                    float a = Mathf.Clamp01((d - 0.55f) / 0.6f);
                    a = a * a;
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            vignette = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            return vignette;
        }
    }
}
