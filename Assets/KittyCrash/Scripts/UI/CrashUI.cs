using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace KittyCrash
{
    /// <summary>
    /// Proper crash-game UI, built entirely from code at runtime (no prefabs, no TextMeshPro setup):
    ///   • top bar: logo + live/offline badge, season pill with festival, credits card, history pills
    ///   • centre: huge multiplier (colour ramps with the value, pulses, shakes on crash) + status line + warm-up bar
    ///   • bottom-left: bet card (amount, 1/2 · x2 · +10 · MAX chips, auto cash-out, big BET / CASH OUT button)
    ///   • bottom-right: live curve graph with grid labels and a cash-out marker
    ///   • win / loss toasts
    /// Put it on Managers (the installer does this). It switches the old debug HUD off automatically.
    /// Keys: Space / Enter = bet or cash out.
    /// </summary>
    public class CrashUI : MonoBehaviour
    {
        [Header("Fonts (Pixelify Sans is in Art/Fonts)")]
        public Font font;
        public Font boldFont;

        [Header("Colours")]
        public Color accent = new Color(1f, 0.66f, 0.18f);
        public Color win = new Color(0.30f, 0.90f, 0.50f);
        public Color lose = new Color(1f, 0.36f, 0.38f);
        public Color panel = new Color(0.06f, 0.07f, 0.11f, 0.86f);
        public Color panelLight = new Color(0.15f, 0.16f, 0.24f, 1f);
        public Color textMain = new Color(0.97f, 0.97f, 1f);
        public Color textMuted = new Color(0.64f, 0.67f, 0.78f);

        public bool disableOldHud = true;
        public string startingBet = "10";
        public string startingAuto = "2.00";

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        RoundManager rm;
        CatWalker cat;
        Sprite rounded, pillSprite;

        Text creditsText, seasonText, festText, apiText, apiToggleText, multText, statusText, actionText, toastText, errorText;
        Image seasonPill, actionBg, warmFill, apiToggleBg;
        GameObject warmBar;
        InputField betInput, autoInput;
        Toggle autoToggle;
        Button actionBtn, apiToggleBtn;
        RectTransform historyRow, multRT, toastRT;
        CanvasGroup toastCG;
        CrashGraph graph;
        Text[] graphLabels = new Text[4];
        Selectable[] betControls;

        Vector2 multBase, toastBase;
        float toastT = 99f, shakeT;
        int historyCount = -1;
        float historyFirst = -1f;

        // ── lifecycle ─────────────────────────────

        void Start()
        {
            rm = RoundManager.Instance;
#if UNITY_2023_1_OR_NEWER
            cat = FindAnyObjectByType<CatWalker>();
            var oldHud = FindAnyObjectByType<CrashHUD>();
#else
            cat = FindObjectOfType<CatWalker>();
            var oldHud = FindObjectOfType<CrashHUD>();
#endif
            if (disableOldHud && oldHud != null) oldHud.enabled = false;

            if (font == null)
            {
#if UNITY_2022_2_OR_NEWER
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            }
            if (boldFont == null) boldFont = font;

            Build();

            if (rm != null)
            {
                rm.OnCashOut += HandleCashOut;
                rm.OnCrash += HandleCrash;
            }
        }

        void OnDestroy()
        {
            if (rm == null) return;
            rm.OnCashOut -= HandleCashOut;
            rm.OnCrash -= HandleCrash;
        }

        void HandleCashOut(float mult, decimal payout)
        {
            Toast("+" + payout.ToString("N2", Inv), win);
        }

        void HandleCrash(float crash, string distraction)
        {
            shakeT = 0.45f;
            if (!rm.CashedOut) Toast("-" + rm.Wager.ToString("N2", Inv), lose);
        }

        // ── per frame ─────────────────────────────

        void Update()
        {
            if (rm == null) { rm = RoundManager.Instance; if (rm == null) return; }

            bool typing = (betInput != null && betInput.isFocused) || (autoInput != null && autoInput.isFocused);
            if (!typing && InputReader.ActionPressed()) OnAction();

            UpdateTopBar();
            UpdateCentre();
            UpdateBetCard();
            UpdateGraph();
            UpdateHistory();
            UpdateToast();
        }

        void UpdateTopBar()
        {
            creditsText.text = rm.Balance.ToString("N2", Inv);

            var ss = SeasonSystem.Instance;
            if (ss != null)
            {
                seasonText.text = ss.InTransition
                    ? $"{ss.CurrentSeason.ToString().ToUpper()}  >  {ss.NextSeason.ToString().ToUpper()}"
                    : ss.CurrentSeason.ToString().ToUpper();
                var c = Color.Lerp(SeasonColor(ss.CurrentSeason), SeasonColor(ss.NextSeason), ss.Blend);
                seasonPill.color = c;
                string fest = ss.Get(ss.DominantSeason).festiveName;
                festText.text = string.IsNullOrEmpty(fest) ? "" : "~ " + fest + " ~";
            }

            if (rm.useApi)
            {
                string mode = rm.apiBackend != null && !string.IsNullOrEmpty(rm.apiBackend.RngMode) ? rm.apiBackend.RngMode : "api";
                apiText.text = rm.Phase == RoundPhase.Offline ? "o  API OFFLINE - retrying" : $"o  LIVE  -  {mode} RNG";
                apiText.color = rm.Phase == RoundPhase.Offline ? lose : win;
            }
            else
            {
                apiText.text = "o  PRACTICE MODE (offline)";
                apiText.color = textMuted;
            }
            bool canSwitch = rm.Phase == RoundPhase.Idle || rm.Phase == RoundPhase.Offline || rm.Phase == RoundPhase.Ended;
            apiToggleBtn.interactable = canSwitch;
            apiToggleText.text = rm.useApi ? "USE OFFLINE" : "USE API";
        }

        void UpdateCentre()
        {
            string big, sub;
            Color c = textMain;
            float pulse = 0f;

            switch (rm.Phase)
            {
                case RoundPhase.Connecting: big = "..."; sub = "Connecting..."; c = textMuted; break;
                case RoundPhase.Offline: big = "OFFLINE"; sub = $"Can't reach the server - retrying in {Mathf.CeilToInt(rm.PhaseTimeLeft)}s"; c = lose; break;
                case RoundPhase.Idle: big = "1.00x"; sub = "Place a bet to send kitty walking"; c = textMuted; break;
                case RoundPhase.Starting: big = "1.00x"; sub = "Kitty is stretching...  rolling the quantum dice"; c = textMuted; break;
                case RoundPhase.Running:
                    float m = CrashMath.Floor2(rm.Multiplier);
                    big = m.ToString("0.00", Inv) + "x";
                    if (rm.IsWarmingUp) { c = textMuted; sub = $"Kitty is warming up...  multiplier starts in {rm.WarmupLeft:0.0}s"; }
                    else
                    {
                        c = rm.CashedOut ? win : MultColor(m);
                        pulse = Mathf.Clamp01((m - 1f) / 4f);
                        sub = cat != null ? $"Kitty has walked {cat.DistanceThisRound:0.0} m" : "Kitty is walking...";
                        if (cat != null && cat.IsFidgeting) sub += "   ...sniff sniff";
                        if (rm.CashedOut) sub = $"You cashed out at {rm.CashOutMultiplier:0.00}x - watch where kitty stops!";
                    }
                    break;
                default:
                    big = rm.Multiplier.ToString("0.00", Inv) + "x";
                    c = lose;
                    var d = cat != null ? Distractions.Pretty(cat.LastDistraction) : "something shiny";
                    sub = $"DISTRACTED by {d}!";
                    break;
            }

            multText.text = big;
            multText.color = c;
            float s = 1f + pulse * 0.035f * Mathf.Sin(Time.time * 9f);
            multRT.localScale = new Vector3(s, s, 1f);

            if (shakeT > 0f)
            {
                shakeT -= Time.deltaTime;
                float k = Mathf.Clamp01(shakeT / 0.45f) * 18f;
                multRT.anchoredPosition = multBase + new Vector2(Random.Range(-k, k), Random.Range(-k, k));
            }
            else multRT.anchoredPosition = multBase;

            statusText.text = sub;

            bool warming = rm.IsWarmingUp;
            warmBar.SetActive(warming);
            if (warming && rm.WarmupSeconds > 0f)
            {
                var rt = warmFill.rectTransform;
                rt.anchorMax = new Vector2(1f - rm.WarmupLeft / rm.WarmupSeconds, 1f);
            }
        }

        void UpdateBetCard()
        {
            bool editable = rm.Phase == RoundPhase.Idle;
            foreach (var sel in betControls) sel.interactable = editable;

            string label;
            Color bg;
            bool clickable = false;
            switch (rm.Phase)
            {
                case RoundPhase.Idle: label = "BET"; bg = win; clickable = true; break;
                case RoundPhase.Starting: label = "STARTING..."; bg = panelLight; break;
                case RoundPhase.Running:
                    if (rm.CashedOut) { label = "WON  +" + rm.Payout.ToString("N2", Inv); bg = panelLight; }
                    else if (rm.CashOutPending) { label = "CASHING OUT..."; bg = accent * 0.8f; }
                    else if (rm.IsWarmingUp) { label = $"WARMING UP  {rm.WarmupLeft:0.0}s"; bg = panelLight; }
                    else { label = "CASH OUT  " + rm.PotentialPayout.ToString("N2", Inv); bg = accent; clickable = true; }
                    break;
                case RoundPhase.Ended:
                    label = rm.CashedOut ? "WON  +" + rm.Payout.ToString("N2", Inv) : "LOST  -" + rm.Wager.ToString("N2", Inv);
                    bg = panelLight; break;
                default: label = "OFFLINE"; bg = panelLight; break;
            }
            bg.a = 1f;
            actionText.text = label;
            actionText.color = clickable ? new Color(0.06f, 0.07f, 0.1f) : textMuted;
            actionBg.color = bg;
            actionBtn.interactable = clickable;

            errorText.text = rm.LastError ?? "";
        }

        void UpdateGraph()
        {
            float elapsed = 0f;
            Color line = accent;
            if (rm.Phase == RoundPhase.Running && !rm.IsWarmingUp) elapsed = CrashMath.SecondsFor(rm.Multiplier, rm.GrowthRate);
            if (rm.Phase == RoundPhase.Ended) { elapsed = CrashMath.SecondsFor(rm.Multiplier, rm.GrowthRate); line = lose; }
            if (rm.CashedOut && rm.Phase == RoundPhase.Running) line = win;

            graph.SetState(rm.GrowthRate, elapsed, rm.CashedOut ? rm.CashOutMultiplier : -1f, line);
            for (int i = 0; i < 4; i++)
                graphLabels[i].text = (1f + (graph.MaxMult - 1f) * i / 3f).ToString("0.0", Inv) + "x";
        }

        void UpdateHistory()
        {
            var h = rm.History;
            float first = h.Count > 0 ? h[0] : -1f;
            if (h.Count == historyCount && Mathf.Approximately(first, historyFirst)) return;
            historyCount = h.Count;
            historyFirst = first;

            for (int i = historyRow.childCount - 1; i >= 0; i--) Destroy(historyRow.GetChild(i).gameObject);
            for (int i = Mathf.Min(h.Count, 10) - 1; i >= 0; i--) // oldest left, newest right
            {
                float v = h[i];
                Color c = v >= 10f ? new Color(1f, 0.8f, 0.25f) : v >= 2f ? win : lose;
                var pill = Panel("Pill", historyRow, new Color(c.r * 0.25f, c.g * 0.25f, c.b * 0.25f, 0.95f), pillSprite);
                var le = pill.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 104; le.preferredHeight = 40;
                var t = Label("V", pill.transform, v.ToString("0.00", Inv) + "x", 22, c, TextAnchor.MiddleCenter, true);
                Stretch(t.rectTransform);
            }
        }

        void UpdateToast()
        {
            toastT += Time.deltaTime;
            float a = 1f - Mathf.Clamp01((toastT - 0.7f) / 0.6f);
            toastCG.alpha = a;
            float pop = toastT < 0.15f ? Mathf.Lerp(0.6f, 1.15f, toastT / 0.15f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((toastT - 0.15f) / 0.2f));
            toastRT.localScale = new Vector3(pop, pop, 1f);
            toastRT.anchoredPosition = toastBase + new Vector2(0f, toastT * 70f);
        }

        void Toast(string text, Color c)
        {
            toastText.text = text;
            toastText.color = c;
            toastT = 0f;
        }

        // ── actions ───────────────────────────────

        void OnAction()
        {
            if (rm == null) return;
            if (rm.Phase == RoundPhase.Idle)
            {
                if (!decimal.TryParse(betInput.text, NumberStyles.Number, Inv, out var wager)) { rm.LastError = "Enter a valid bet"; return; }
                float auto = 0f;
                if (autoToggle.isOn) float.TryParse(autoInput.text, NumberStyles.Float, Inv, out auto);
                rm.PlaceBet(wager, auto);
            }
            else if (rm.Phase == RoundPhase.Running && !rm.IsWarmingUp) rm.CashOut();
        }

        void Chip(string op)
        {
            decimal.TryParse(betInput.text, NumberStyles.Number, Inv, out var v);
            switch (op)
            {
                case "half": v /= 2m; break;
                case "double": v *= 2m; break;
                case "plus10": v += 10m; break;
                case "max": v = rm.Balance; break;
            }
            if (v < rm.MinBet) v = rm.MinBet;
            if (v > rm.MaxBet) v = rm.MaxBet;
            betInput.text = (System.Math.Floor(v * 100m) / 100m).ToString("0.##", Inv);
        }

        // ── building the UI ───────────────────────

        void Build()
        {
            EnsureEventSystem();
            rounded = MakeRounded(64, 16);
            pillSprite = MakeRounded(64, 30);

            var canvasGO = new GameObject("CrashUI Canvas", typeof(RectTransform));
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            var root = canvasGO.transform;

            // ── top-left: logo + mode badge + switch ──
            var logo = Label("Logo", root, "KITTY CRASH", 58, accent, TextAnchor.UpperLeft, true);
            Place(logo.rectTransform, new Vector2(0, 1), new Vector2(40, -24), new Vector2(620, 70));
            AddShadow(logo.gameObject, 4);
            apiText = Label("Mode", root, "", 22, win, TextAnchor.UpperLeft, false);
            Place(apiText.rectTransform, new Vector2(0, 1), new Vector2(44, -94), new Vector2(560, 30));
            apiToggleBtn = ButtonWithLabel("ApiToggle", root, "USE API", 18, out apiToggleBg, out apiToggleText, () =>
            {
                if (rm != null) rm.SetUseApi(!rm.useApi);
            });
            apiToggleBg.color = panelLight;
            apiToggleText.color = textMuted;
            Place((RectTransform)apiToggleBtn.transform, new Vector2(0, 1), new Vector2(44, -130), new Vector2(170, 36));

            // ── top-centre: season pill ──
            seasonPill = Panel("Season", root, new Color(0.4f, 0.7f, 0.4f), pillSprite);
            Place(seasonPill.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(460, 60), new Vector2(0.5f, 1));
            seasonText = Label("SeasonText", seasonPill.transform, "", 30, Color.white, TextAnchor.MiddleCenter, true);
            Stretch(seasonText.rectTransform);
            AddShadow(seasonText.gameObject, 2);
            festText = Label("Festival", root, "", 24, textMain, TextAnchor.UpperCenter, false);
            Place(festText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -96), new Vector2(600, 32), new Vector2(0.5f, 1));
            AddShadow(festText.gameObject, 2);

            // ── top-right: credits card ──
            var creditsCard = Panel("Credits", root, panel, rounded);
            Place(creditsCard.rectTransform, new Vector2(1, 1), new Vector2(-40, -24), new Vector2(340, 88), new Vector2(1, 1));
            var cl = Label("Label", creditsCard.transform, "CREDITS", 20, textMuted, TextAnchor.UpperLeft, false);
            Place(cl.rectTransform, new Vector2(0, 1), new Vector2(20, -10), new Vector2(200, 26));
            creditsText = Label("Value", creditsCard.transform, "0.00", 44, textMain, TextAnchor.LowerRight, true);
            Place(creditsText.rectTransform, new Vector2(1, 0), new Vector2(-20, 8), new Vector2(300, 56), new Vector2(1, 0));

            // ── history pills (under credits) ──
            var hr = new GameObject("History", typeof(RectTransform)).GetComponent<RectTransform>();
            hr.SetParent(root, false);
            Place(hr, new Vector2(1, 1), new Vector2(-40, -126), new Vector2(1140, 40), new Vector2(1, 1));
            var hl = hr.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleRight;
            hl.spacing = 8;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            historyRow = hr;

            // ── centre: multiplier, status, warm-up bar ──
            multText = Label("Multiplier", root, "1.00x", 150, textMain, TextAnchor.MiddleCenter, true);
            multRT = multText.rectTransform;
            Place(multRT, new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(1000, 180), new Vector2(0.5f, 1));
            AddShadow(multText.gameObject, 6);
            multBase = multRT.anchoredPosition;

            statusText = Label("Status", root, "", 32, textMain, TextAnchor.MiddleCenter, false);
            Place(statusText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -350), new Vector2(1300, 46), new Vector2(0.5f, 1));
            AddShadow(statusText.gameObject, 3);

            var wb = Panel("WarmupBar", root, new Color(0, 0, 0, 0.45f), pillSprite);
            Place(wb.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -404), new Vector2(460, 16), new Vector2(0.5f, 1));
            warmFill = Panel("Fill", wb.transform, accent, pillSprite);
            warmFill.rectTransform.anchorMin = Vector2.zero;
            warmFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            warmFill.rectTransform.offsetMin = warmFill.rectTransform.offsetMax = Vector2.zero;
            warmBar = wb.gameObject;

            // ── toast ──
            toastText = Label("Toast", root, "", 96, win, TextAnchor.MiddleCenter, true);
            toastRT = toastText.rectTransform;
            Place(toastRT, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(900, 130), new Vector2(0.5f, 0.5f));
            AddShadow(toastText.gameObject, 6);
            toastCG = toastText.gameObject.AddComponent<CanvasGroup>();
            toastCG.alpha = 0f;
            toastCG.blocksRaycasts = false;
            toastBase = toastRT.anchoredPosition;

            BuildBetCard(root);
            BuildGraphCard(root);
        }

        void BuildBetCard(Transform root)
        {
            var card = Panel("BetCard", root, panel, rounded);
            Place(card.rectTransform, new Vector2(0, 0), new Vector2(40, 40), new Vector2(470, 372), new Vector2(0, 0));
            var t = card.transform;

            var title = Label("Title", t, "YOUR BET", 22, textMuted, TextAnchor.UpperLeft, false);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(24, -16), new Vector2(300, 28));

            betInput = MakeInput("Bet", t, startingBet, InputField.ContentType.DecimalNumber, 38);
            Place((RectTransform)betInput.transform, new Vector2(0, 1), new Vector2(24, -50), new Vector2(422, 66));
            var unit = Label("Unit", betInput.transform, "credits", 22, textMuted, TextAnchor.MiddleRight, false);
            Place(unit.rectTransform, new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(120, 40), new Vector2(1, 0.5f));

            string[] chips = { "1/2", "x2", "+10", "MAX" };
            string[] ops = { "half", "double", "plus10", "max" };
            var chipButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                string op = ops[i];
                chipButtons[i] = ButtonWithLabel("Chip_" + chips[i], t, chips[i], 22, out var bg, out var lbl, () => Chip(op));
                bg.color = panelLight;
                lbl.color = textMain;
                Place((RectTransform)chipButtons[i].transform, new Vector2(0, 1), new Vector2(24 + i * 108, -128), new Vector2(98, 48));
            }

            autoToggle = MakeToggle("Auto", t, out var autoLabel);
            Place((RectTransform)autoToggle.transform, new Vector2(0, 1), new Vector2(24, -194), new Vector2(36, 36));
            autoLabel.text = "";
            var al = Label("AutoLabel", t, "Auto cash-out at", 24, textMain, TextAnchor.MiddleLeft, false);
            Place(al.rectTransform, new Vector2(0, 1), new Vector2(70, -192), new Vector2(230, 40));
            autoInput = MakeInput("AutoValue", t, startingAuto, InputField.ContentType.DecimalNumber, 28);
            Place((RectTransform)autoInput.transform, new Vector2(0, 1), new Vector2(306, -188), new Vector2(110, 48));
            var ax = Label("x", t, "x", 26, textMuted, TextAnchor.MiddleLeft, false);
            Place(ax.rectTransform, new Vector2(0, 1), new Vector2(422, -192), new Vector2(30, 40));

            errorText = Label("Error", t, "", 20, lose, TextAnchor.MiddleLeft, false);
            Place(errorText.rectTransform, new Vector2(0, 1), new Vector2(24, -244), new Vector2(422, 28));

            actionBtn = ButtonWithLabel("Action", t, "BET", 36, out actionBg, out actionText, OnAction);
            Place((RectTransform)actionBtn.transform, new Vector2(0, 0), new Vector2(24, 22), new Vector2(422, 84), new Vector2(0, 0));
            actionBtn.transition = Selectable.Transition.None;
            var hint = Label("Hint", actionBtn.transform, "SPACE", 16, new Color(0, 0, 0, 0.35f), TextAnchor.LowerRight, false);
            Place(hint.rectTransform, new Vector2(1, 0), new Vector2(-12, 6), new Vector2(100, 22), new Vector2(1, 0));

            betControls = new Selectable[] { betInput, autoInput, autoToggle, chipButtons[0], chipButtons[1], chipButtons[2], chipButtons[3] };
        }

        void BuildGraphCard(Transform root)
        {
            var card = Panel("GraphCard", root, panel, rounded);
            Place(card.rectTransform, new Vector2(1, 0), new Vector2(-40, 40), new Vector2(580, 320), new Vector2(1, 0));
            var title = Label("Title", card.transform, "LIVE CURVE", 22, textMuted, TextAnchor.UpperLeft, false);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(24, -16), new Vector2(300, 28));

            var g = new GameObject("Graph", typeof(RectTransform));
            g.transform.SetParent(card.transform, false);
            var grt = (RectTransform)g.transform;
            grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2(84, 26);
            grt.offsetMax = new Vector2(-24, -58);
            graph = g.AddComponent<CrashGraph>();
            graph.raycastTarget = false;

            for (int i = 0; i < 4; i++)
            {
                var l = Label("Y" + i, card.transform, "", 18, textMuted, TextAnchor.MiddleRight, false);
                var rt = l.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(1, 0.5f);
                float h = 320 - 26 - 58;
                rt.anchoredPosition = new Vector2(76, 26 + h * i / 3f);
                rt.sizeDelta = new Vector2(70, 24);
                graphLabels[i] = l;
            }
        }

        // ── widget helpers ────────────────────────

        static Color SeasonColor(Season s)
        {
            switch (s)
            {
                case Season.Spring: return new Color(0.92f, 0.45f, 0.65f);
                case Season.Summer: return new Color(0.25f, 0.65f, 0.35f);
                case Season.Autumn: return new Color(0.88f, 0.45f, 0.12f);
                default: return new Color(0.35f, 0.55f, 0.85f);
            }
        }

        Color MultColor(float m)
        {
            if (m < 2f) return textMain;
            if (m < 5f) return new Color(1f, 0.85f, 0.3f);
            if (m < 10f) return accent;
            return new Color(1f, 0.45f, 0.75f);
        }

        Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor anchor, bool bold)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = bold ? boldFont : font;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        Image Panel(string name, Transform parent, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Button ButtonWithLabel(string name, Transform parent, string text, int size, out Image bg, out Text label, UnityEngine.Events.UnityAction onClick)
        {
            bg = Panel(name, parent, panelLight, rounded);
            bg.raycastTarget = true;
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            var cb = b.colors;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.6f);
            b.colors = cb;
            b.onClick.AddListener(onClick);
            label = Label("Label", bg.transform, text, size, textMain, TextAnchor.MiddleCenter, true);
            Stretch(label.rectTransform);
            return b;
        }

        InputField MakeInput(string name, Transform parent, string value, InputField.ContentType type, int size)
        {
            var bg = Panel(name, parent, panelLight, rounded);
            bg.raycastTarget = true;
            var txt = Label("Text", bg.transform, "", size, textMain, TextAnchor.MiddleLeft, true);
            txt.supportRichText = false;
            Stretch(txt.rectTransform, 18, 4);
            var field = bg.gameObject.AddComponent<InputField>();
            field.textComponent = txt;
            field.targetGraphic = bg;
            field.contentType = type;
            field.caretColor = accent;
            field.selectionColor = new Color(accent.r, accent.g, accent.b, 0.35f);
            field.text = value;
            return field;
        }

        Toggle MakeToggle(string name, Transform parent, out Text label)
        {
            var bg = Panel(name, parent, panelLight, rounded);
            bg.raycastTarget = true;
            var check = Panel("Check", bg.transform, accent, rounded);
            Stretch(check.rectTransform, 7, 7);
            var tg = bg.gameObject.AddComponent<Toggle>();
            tg.targetGraphic = bg;
            tg.graphic = check;
            tg.isOn = false;
            label = Label("Label", bg.transform, "", 1, Color.clear, TextAnchor.MiddleLeft, false);
            return tg;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt, float padX = 0, float padY = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        static void AddShadow(GameObject go, float d)
        {
            var s = go.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.55f);
            s.effectDistance = new Vector2(d, -d);
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }

        static void EnsureEventSystem()
        {
#if UNITY_2023_1_OR_NEWER
            if (FindAnyObjectByType<EventSystem>() != null) return;
#else
            if (FindObjectOfType<EventSystem>() != null) return;
#endif
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
