using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace KittyCrash
{
    public class CrashUI : MonoBehaviour
    {
        [Header("Fonts (Pixelify Sans is in Art/Fonts)")]
        public Font font;
        public Font boldFont;
        [Tooltip("Bottom betting panel uses a clean, sharp font (Fredoka) so numbers are easy to read, even if the rest is pixel-style.")]
        public bool cleanDockFont = true;
        Font dockFont, dockBoldFont;

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

        [Header("Split screen")]
        [Tooltip("Game on top, betting dock along the bottom. The camera only draws above the dock, so nothing covers the cat.")]
        public bool docked = true;
        public float dockHeight = 300f;
        [Tooltip("Camera zoom for the game area (smaller = closer).")]
        public float gameViewSize = 4f;
        [Tooltip("Camera height for the game area (keeps the ground near the bottom of the game view).")]
        public float gameViewCenterY = -1f;

        RectTransform dockRT;
        Camera gameCam;
        Canvas canvasRef;

        void Awake()
        {
            if (!docked) return;
            gameCam = Camera.main;
            if (gameCam == null) return;
            float frac = dockHeight / 1080f;
            gameCam.rect = new Rect(0f, frac, 1f, 1f - frac);
            gameCam.orthographicSize = gameViewSize;
            var p = gameCam.transform.position;
            gameCam.transform.position = new Vector3(p.x, gameViewCenterY, p.z);
            var juice = gameCam.GetComponent<CameraJuice>();
            if (juice != null) juice.minSize = gameViewSize * 0.88f;
        }

        void LateUpdate()
        {
            if (!docked || gameCam == null || dockRT == null || canvasRef == null) return;
            float frac = Mathf.Clamp(dockRT.rect.height * canvasRef.scaleFactor / Mathf.Max(1, Screen.height), 0f, 0.8f);
            var r = new Rect(0f, frac, 1f, 1f - frac);
            if (gameCam.rect != r) gameCam.rect = r;
            FitDock();
        }

        RectTransform creditsRT, warmRT, graphRT;

        /// <summary>
        /// Narrow screens (e.g. a 4:3 Game view): shrink the graph card and fit the middle column
        /// (credits, history, status, countdown bar) into the space between the two cards so nothing overlaps.
        /// </summary>
        void FitDock()
        {
            if (creditsRT == null || graphRT == null || statusText == null) return;
            float W = ((RectTransform)canvasRef.transform).rect.width;
            float gw = Mathf.Clamp(W - 30f - 470f - 16f - 380f - 16f - 30f, 320f, 580f);
            graphRT.sizeDelta = new Vector2(gw, graphRT.sizeDelta.y);
            float left = 30f + 470f + 16f, right = W - 30f - gw - 16f;
            float cw = Mathf.Max(200f, right - left);
            float cx = (left + right) * 0.5f - W * 0.5f;

            creditsRT.anchoredPosition = new Vector2(cx, creditsRT.anchoredPosition.y);
            creditsRT.sizeDelta = new Vector2(Mathf.Min(340f, cw), creditsRT.sizeDelta.y);
            historyRow.anchoredPosition = new Vector2(cx, historyRow.anchoredPosition.y);
            historyRow.sizeDelta = new Vector2(Mathf.Min(800f, cw), historyRow.sizeDelta.y);
            var st = statusText.rectTransform;
            st.anchoredPosition = new Vector2(cx, st.anchoredPosition.y);
            st.sizeDelta = new Vector2(Mathf.Min(820f, cw), 56f);
            if (warmRT != null)
            {
                warmRT.anchoredPosition = new Vector2(cx, warmRT.anchoredPosition.y);
                warmRT.sizeDelta = new Vector2(Mathf.Min(560f, cw - 20f), warmRT.sizeDelta.y);
            }
        }

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
        Text graphValue, graphCaption, graphX0, graphX1, graphTarget, graphCash;
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
            if (cleanDockFont)
            {
                dockFont = Resources.Load<Font>("KittyCrashFonts/Fredoka-Medium");
                dockBoldFont = Resources.Load<Font>("KittyCrashFonts/Fredoka-Bold");
            }
            if (dockFont == null) dockFont = font;
            if (dockBoldFont == null) dockBoldFont = dockFont != font ? dockFont : boldFont;

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
            ShowBanner(crash, distraction);
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
            UpdateBanner();
        }

        // ── big "DISTRACTED BY ..." banner ─────────

        RectTransform bannerRT, bannerIconRT;
        Image bannerIcon, bannerBg;
        Text bannerTitle, bannerReason, bannerResult;
        float bannerT = 99f;

                void BuildBanner(Transform root)
        {
            bannerBg = Panel("DistractionBanner", root, new Color(0.08f, 0.06f, 0.12f, 0.86f), rounded);
            bannerRT = bannerBg.rectTransform;
            Place(bannerRT, new Vector2(0.5f, 1), new Vector2(0, docked ? -118 : -150), new Vector2(600, 112), new Vector2(0.5f, 1));
            var stripe = Panel("Stripe", bannerRT, lose, rounded);
            stripe.rectTransform.anchorMin = new Vector2(0f, 0f);
            stripe.rectTransform.anchorMax = new Vector2(0f, 1f);
            stripe.rectTransform.pivot = new Vector2(0f, 0.5f);
            stripe.rectTransform.anchoredPosition = Vector2.zero;
            stripe.rectTransform.sizeDelta = new Vector2(10f, 0f);

            var iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(bannerRT, false);
            bannerIcon = iconGO.AddComponent<Image>();
            bannerIcon.preserveAspect = true;
            bannerIcon.raycastTarget = false;
            bannerIconRT = (RectTransform)iconGO.transform;
            Place(bannerIconRT, new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(80, 80), new Vector2(0, 0.5f));

            bannerTitle = Label("Title", bannerRT, "DISTRACTED!", 40, lose, TextAnchor.UpperLeft, true);
            Place(bannerTitle.rectTransform, new Vector2(0, 1), new Vector2(118, -8), new Vector2(460, 46));
            AddShadow(bannerTitle.gameObject, 2);
            bannerReason = Label("Reason", bannerRT, "", 24, textMain, TextAnchor.UpperLeft, true);
            Place(bannerReason.rectTransform, new Vector2(0, 1), new Vector2(120, -54), new Vector2(460, 30));
            bannerResult = Label("Result", bannerRT, "", 18, textMuted, TextAnchor.UpperLeft, false);
            Place(bannerResult.rectTransform, new Vector2(0, 1), new Vector2(121, -84), new Vector2(460, 24));
            bannerBg.gameObject.SetActive(false);
        }

        void ShowBanner(float crash, string distraction)
        {
            if (bannerRT == null) return;
            var type = SeasonDistractions.Resolve(distraction); // same seasonal pick as the cat shows
            Sprite icon = null;
            if (cat != null && cat.distractionSprites != null && (int)type < cat.distractionSprites.Length) icon = cat.distractionSprites[(int)type];
            bannerIcon.sprite = icon;
            bannerIcon.enabled = icon != null;
            bannerTitle.text = "DISTRACTED!";
            bannerReason.text = "Kitty spotted " + Distractions.Pretty(type) + " at " + crash.ToString("0.00", Inv) + "x";
            if (rm.HasBet && !rm.CashedOut) { bannerResult.text = "You lost " + rm.Wager.ToString("N2", Inv) + " - cash out sooner next time!"; bannerResult.color = lose; }
            else if (rm.HasBet && rm.CashedOut) { bannerResult.text = "You got out at " + rm.CashOutMultiplier.ToString("0.00", Inv) + "x and won " + rm.Payout.ToString("N2", Inv) + "!"; bannerResult.color = win; }
            else { bannerResult.text = "No bet this round - place one for the next walk"; bannerResult.color = textMuted; }
            bannerT = 0f;
            bannerBg.gameObject.SetActive(true);
        }

                void UpdateBanner()
        {
            if (bannerRT == null) return;
            bool show = rm.Phase == RoundPhase.Ended && bannerT < 30f;
            multText.enabled = !show; // the banner takes the multiplier's place (it shows the crash value itself)
            if (!show)
            {
                if (bannerBg.gameObject.activeSelf) bannerBg.gameObject.SetActive(false);
                bannerT = 99f;
                bannerRT.anchoredPosition = new Vector2(0f, bannerRT.anchoredPosition.y);
                return;
            }
            bannerT += Time.deltaTime;
            // elastic pop-in, then a gentle breathe
            float k = Mathf.Clamp01(bannerT / 0.45f);
            float pop = k < 1f ? 1f + Mathf.Sin(k * Mathf.PI * 2.5f) * (1f - k) * 0.25f : 1f + Mathf.Sin(bannerT * 3f) * 0.012f;
            if (bannerT < 0.08f) pop = Mathf.Lerp(0.3f, 1f, bannerT / 0.08f);
            bannerRT.localScale = new Vector3(pop, pop, 1f);
            bannerIconRT.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(bannerT * 6f) * 10f);
            bannerIconRT.anchoredPosition = new Vector2(24f, Mathf.Sin(bannerT * 4f) * 5f);

            // Keep kitty visible: if she is behind the banner (up a tree, mid-pounce...), slide the banner to the other side.
            float targetX = 0f;
            var view = Camera.main;
            var parent = bannerRT.parent as RectTransform;
            if (cat != null && view != null && parent != null)
            {
                var sr = cat.visual != null ? cat.visual.GetComponent<SpriteRenderer>() : null;
                Vector3 head = sr != null ? new Vector3(sr.bounds.center.x, sr.bounds.max.y, 0f) : cat.transform.position + Vector3.up * 1.5f;
                Vector3 s = PixelGrid.WorldToScreen(view, head);
                float scale = bannerRT.lossyScale.x;
                float half = bannerRT.rect.width * scale * 0.5f;
                float bottom = bannerRT.position.y - bannerRT.rect.height * scale;
                float centre = parent.position.x;
                if (s.y > bottom - 40f * scale && Mathf.Abs(s.x - centre) < half + 100f * scale)
                {
                    float room = parent.rect.width * 0.5f - bannerRT.rect.width * 0.5f - 24f;
                    targetX = s.x < centre ? room : -room;
                }
            }
            var p = bannerRT.anchoredPosition;
            p.x = Mathf.Lerp(p.x, targetX, Time.deltaTime * 7f);
            bannerRT.anchoredPosition = p;
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
                case RoundPhase.Idle:
                    if (rm.autoRounds)
                    {
                        big = Mathf.CeilToInt(Mathf.Max(0f, rm.PhaseTimeLeft)).ToString();
                        c = accent;
                        pulse = 0.6f;
                        sub = rm.BetQueued
                            ? $"Bet locked in: {rm.QueuedWager.ToString("N2", Inv)}  -  kitty starts in {rm.PhaseTimeLeft:0.0}s"
                            : $"Place your bets!  Next walk in {rm.PhaseTimeLeft:0.0}s";
                    }
                    else { big = "1.00x"; sub = "Place a bet to send kitty walking"; c = textMuted; }
                    break;
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
                        if (!rm.HasBet) sub = "Just watching this round - bets open when kitty gets distracted";
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

            bool counting = rm.Phase == RoundPhase.Idle && rm.autoRounds && rm.bettingCountdown > 0f;
            bool warming = rm.IsWarmingUp && rm.WarmupSeconds > 0f;
            warmBar.SetActive(counting || warming);
            var fill = warmFill.rectTransform;
            if (counting) fill.anchorMax = new Vector2(1f - Mathf.Clamp01(rm.PhaseTimeLeft / rm.bettingCountdown), 1f);
            else if (warming) fill.anchorMax = new Vector2(1f - rm.WarmupLeft / rm.WarmupSeconds, 1f);
        }

        void UpdateBetCard()
        {
            bool editable = rm.Phase == RoundPhase.Idle && !rm.BetQueued;
            foreach (var sel in betControls) sel.interactable = editable;

            string label;
            Color bg;
            bool clickable = false;
            bool darkText = true;
            switch (rm.Phase)
            {
                case RoundPhase.Idle:
                    if (rm.BetQueued) { label = "CANCEL BET"; bg = panelLight; clickable = true; darkText = false; }
                    else { label = rm.autoRounds ? $"BET   ({Mathf.CeilToInt(Mathf.Max(0f, rm.PhaseTimeLeft))}s)" : "BET"; bg = win; clickable = true; }
                    break;
                case RoundPhase.Starting: label = "STARTING..."; bg = panelLight; break;
                case RoundPhase.Running:
                    if (!rm.HasBet) { label = "WATCHING..."; bg = panelLight; }
                    else if (rm.CashedOut) { label = "WON  +" + rm.Payout.ToString("N2", Inv); bg = panelLight; }
                    else if (rm.CashOutPending) { label = "CASHING OUT..."; bg = accent * 0.8f; }
                    else if (rm.IsWarmingUp) { label = $"WARMING UP  {rm.WarmupLeft:0.0}s"; bg = panelLight; }
                    else { label = "CASH OUT  " + rm.PotentialPayout.ToString("N2", Inv); bg = accent; clickable = true; }
                    break;
                case RoundPhase.Ended:
                    label = !rm.HasBet ? "BETS OPEN SOON" : rm.CashedOut ? "WON  +" + rm.Payout.ToString("N2", Inv) : "LOST  -" + rm.Wager.ToString("N2", Inv);
                    bg = panelLight; break;
                default: label = "OFFLINE"; bg = panelLight; break;
            }
            bg.a = 1f;
            actionText.text = label;
            actionText.color = clickable ? (darkText ? new Color(0.06f, 0.07f, 0.1f) : textMain) : textMuted;
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

            float target = TargetMultiplier();
            bool ended = rm.Phase == RoundPhase.Ended;
            graph.SetState(rm.GrowthRate, elapsed, rm.CashedOut ? rm.CashOutMultiplier : -1f, line, rm.CashedOut ? -1f : target, ended);
            for (int i = 0; i < 4; i++)
                graphLabels[i].text = (1f + (graph.MaxMult - 1f) * i / 3f).ToString("0.0", Inv) + "x";

            // what the chart means, in words
            float m = rm.Multiplier;
            switch (rm.Phase)
            {
                case RoundPhase.Running:
                    graphValue.text = rm.IsWarmingUp ? "" : m.ToString("0.00", Inv) + "x  " + elapsed.ToString("0.0", Inv) + "s";
                    graphValue.color = line;
                    graphCaption.text = rm.CashedOut ? "Banked! The line keeps going until kitty gets distracted."
                                      : rm.HasBet ? "Payout grows every second - cash out before the line stops."
                                      : "Watching: the line stops where kitty gets distracted.";
                    break;
                case RoundPhase.Ended:
                    graphValue.text = "STOP " + m.ToString("0.00", Inv) + "x";
                    graphValue.color = lose;
                    graphCaption.text = "Kitty got distracted after " + elapsed.ToString("0.0", Inv) + "s - that's where this round crashed.";
                    break;
                default:
                    graphValue.text = "";
                    graphCaption.text = "Same curve every round. Only where it stops is random.";
                    break;
            }
            graphX1.text = graph.MaxTime.ToString("0", Inv) + "s";

            // labels for the dashed target line and the cash-out dot
            bool showTarget = target > 1f && !rm.CashedOut && target <= graph.MaxMult;
            graphTarget.enabled = showTarget;
            if (showTarget)
            {
                graphTarget.text = "AUTO CASH-OUT " + target.ToString("0.00", Inv) + "x";
                graphTarget.rectTransform.anchoredPosition = new Vector2(graph.rectTransform.rect.xMax, graph.LocalY(target) + 3f);
            }
            bool showCash = rm.CashedOut && rm.CashOutMultiplier > 1f;
            graphCash.enabled = showCash;
            if (showCash)
            {
                graphCash.text = "YOU " + rm.CashOutMultiplier.ToString("0.00", Inv) + "x";
                graphCash.rectTransform.anchoredPosition = new Vector2(graph.LocalX(graph.TimeFor(rm.CashOutMultiplier)), graph.LocalY(rm.CashOutMultiplier) + 12f);
            }
        }

        /// <summary>The auto cash-out the player is aiming for (active bet, queued bet, or what's typed in the card).</summary>
        float TargetMultiplier()
        {
            if (rm.HasBet && rm.AutoCashOut > 1f) return rm.AutoCashOut;
            if (rm.BetQueued && rm.QueuedAutoCashOut > 1f) return rm.QueuedAutoCashOut;
            if (!rm.HasBet && autoToggle != null && autoToggle.isOn && autoInput != null
                && float.TryParse(autoInput.text, NumberStyles.Float, Inv, out float a) && a > 1f) return a;
            return -1f;
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
            if (rm.Phase == RoundPhase.Idle && rm.BetQueued) { rm.CancelBet(); return; }
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
            // 2x-resolution corner textures (drawn at half size) = smooth, sharp rounded cards on big screens
            rounded = MakeRounded(128, 32, 200f);
            pillSprite = MakeRounded(128, 60, 200f);

            var canvasGO = new GameObject("CrashUI Canvas", typeof(RectTransform));
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            canvas.pixelPerfect = true;   // text snaps to whole screen pixels = sharper
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = docked ? 1f : 0.5f; // match height → the dock is always the same share of the screen
            canvasGO.AddComponent<GraphicRaycaster>();
            canvasRef = canvas;
            var root = canvasGO.transform;

            // ── bottom dock (split screen) ──
            Transform dock = root;
            if (docked)
            {
                var d = Panel("Dock", root, new Color(0.05f, 0.06f, 0.09f, 1f), null);
                d.raycastTarget = true;
                dockRT = d.rectTransform;
                dockRT.anchorMin = new Vector2(0f, 0f);
                dockRT.anchorMax = new Vector2(1f, 0f);
                dockRT.pivot = new Vector2(0.5f, 0f);
                dockRT.anchoredPosition = Vector2.zero;
                dockRT.sizeDelta = new Vector2(0f, dockHeight);
                var edge = Panel("Edge", dockRT, new Color(accent.r, accent.g, accent.b, 0.55f), null);
                edge.rectTransform.anchorMin = new Vector2(0f, 1f);
                edge.rectTransform.anchorMax = new Vector2(1f, 1f);
                edge.rectTransform.pivot = new Vector2(0.5f, 1f);
                edge.rectTransform.anchoredPosition = Vector2.zero;
                edge.rectTransform.sizeDelta = new Vector2(0f, 4f);
                dock = dockRT;
            }

            // ── top-left: logo + mode badge + switch ──
            var logo = Label("Logo", root, "CATINO", 58, accent, TextAnchor.UpperLeft, true);
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
            var creditsCard = Panel("Credits", dock, docked ? new Color(0.11f, 0.12f, 0.18f, 1f) : panel, rounded);
            if (docked) { Place(creditsCard.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 182), new Vector2(340, 88), new Vector2(0.5f, 0)); creditsRT = creditsCard.rectTransform; }
            else Place(creditsCard.rectTransform, new Vector2(1, 1), new Vector2(-40, -24), new Vector2(340, 88), new Vector2(1, 1));
            var cl = Label("Label", creditsCard.transform, "CREDITS", 20, textMuted, TextAnchor.UpperLeft, false);
            Place(cl.rectTransform, new Vector2(0, 1), new Vector2(20, -10), new Vector2(200, 26));
            creditsText = Label("Value", creditsCard.transform, "0.00", 44, textMain, TextAnchor.LowerRight, true);
            Place(creditsText.rectTransform, new Vector2(1, 0), new Vector2(-20, 8), new Vector2(300, 56), new Vector2(1, 0));

            // ── history pills (under credits) ──
            var hr = new GameObject("History", typeof(RectTransform)).GetComponent<RectTransform>();
            hr.SetParent(dock, false);
            if (docked) Place(hr, new Vector2(0.5f, 0), new Vector2(0, 128), new Vector2(800, 40), new Vector2(0.5f, 0));
            else Place(hr, new Vector2(1, 1), new Vector2(-40, -126), new Vector2(1140, 40), new Vector2(1, 1));
            var hl = hr.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = docked ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight;
            hl.spacing = 8;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            historyRow = hr;
            if (docked) hr.gameObject.AddComponent<RectMask2D>();

            // ── centre: multiplier, status, warm-up bar ──
            multText = Label("Multiplier", root, "1.00x", 150, textMain, TextAnchor.MiddleCenter, true);
            multRT = multText.rectTransform;
            Place(multRT, new Vector2(0.5f, 1), new Vector2(0, docked ? -128 : -170), new Vector2(1000, 180), new Vector2(0.5f, 1));
            AddShadow(multText.gameObject, 3);
            multBase = multRT.anchoredPosition;

            statusText = Label("Status", dock, "", docked ? 28 : 32, textMain, TextAnchor.MiddleCenter, false);
            if (docked) Place(statusText.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 66), new Vector2(820, 44), new Vector2(0.5f, 0));
            else Place(statusText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -350), new Vector2(1300, 46), new Vector2(0.5f, 1));
            AddShadow(statusText.gameObject, 3);
            if (docked)
            {
                statusText.resizeTextForBestFit = true;   // long messages shrink instead of running under the cards
                statusText.resizeTextMinSize = 14;
                statusText.resizeTextMaxSize = 28;
                statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
                statusText.verticalOverflow = VerticalWrapMode.Truncate;
            }

            var wb = Panel("WarmupBar", dock, new Color(1f, 1f, 1f, 0.12f), pillSprite);
            if (docked) Place(wb.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(560, 16), new Vector2(0.5f, 0));
            else Place(wb.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -404), new Vector2(460, 16), new Vector2(0.5f, 1));
            warmFill = Panel("Fill", wb.transform, accent, pillSprite);
            warmFill.rectTransform.anchorMin = Vector2.zero;
            warmFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            warmFill.rectTransform.offsetMin = warmFill.rectTransform.offsetMax = Vector2.zero;
            warmBar = wb.gameObject;
            warmRT = wb.rectTransform;

            // ── toast ──
            toastText = Label("Toast", root, "", 96, win, TextAnchor.MiddleCenter, true);
            toastRT = toastText.rectTransform;
            Place(toastRT, new Vector2(0.5f, 0.5f), new Vector2(0, docked ? dockHeight * 0.5f + 20f : 20f), new Vector2(900, 130), new Vector2(0.5f, 0.5f));
            AddShadow(toastText.gameObject, 6);
            toastCG = toastText.gameObject.AddComponent<CanvasGroup>();
            toastCG.alpha = 0f;
            toastCG.blocksRaycasts = false;
            toastBase = toastRT.anchoredPosition;

            BuildBanner(root);
            BuildBetCard(dock);
            BuildGraphCard(dock);
        }

        void BuildBetCard(Transform root)
        {
            bool d = docked;
            var card = Panel("BetCard", root, d ? new Color(0.11f, 0.12f, 0.18f, 1f) : panel, rounded);
            Place(card.rectTransform, new Vector2(0, 0), d ? new Vector2(30, 16) : new Vector2(40, 40), new Vector2(470, d ? 268 : 372), new Vector2(0, 0));
            var t = card.transform;
            float yInput = d ? -40 : -50, hInput = d ? 56 : 66, yChips = d ? -104 : -128, hChips = d ? 40 : 48;
            float yAuto = d ? -152 : -194, btnH = d ? 66 : 84, btnY = d ? 12 : 22;

            var title = Label("Title", t, "YOUR BET", 22, textMuted, TextAnchor.UpperLeft, false);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(24, -16), new Vector2(300, 28));

            betInput = MakeInput("Bet", t, startingBet, InputField.ContentType.DecimalNumber, 38);
            Place((RectTransform)betInput.transform, new Vector2(0, 1), new Vector2(24, yInput), new Vector2(422, hInput));
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
                Place((RectTransform)chipButtons[i].transform, new Vector2(0, 1), new Vector2(24 + i * 108, yChips), new Vector2(98, hChips));
            }

            autoToggle = MakeToggle("Auto", t, out var autoLabel);
            Place((RectTransform)autoToggle.transform, new Vector2(0, 1), new Vector2(24, yAuto), new Vector2(36, 36));
            autoLabel.text = "";
            var al = Label("AutoLabel", t, "Auto cash-out at", 24, textMain, TextAnchor.MiddleLeft, false);
            Place(al.rectTransform, new Vector2(0, 1), new Vector2(70, yAuto + 2), new Vector2(230, 40));
            autoInput = MakeInput("AutoValue", t, startingAuto, InputField.ContentType.DecimalNumber, 28);
            Place((RectTransform)autoInput.transform, new Vector2(0, 1), new Vector2(306, yAuto + 4), new Vector2(110, d ? 42 : 48));
            var ax = Label("x", t, "x", 26, textMuted, TextAnchor.MiddleLeft, false);
            Place(ax.rectTransform, new Vector2(0, 1), new Vector2(422, yAuto + 2), new Vector2(30, 40));

            errorText = Label("Error", t, "", 20, lose, d ? TextAnchor.UpperRight : TextAnchor.MiddleLeft, false);
            if (d) Place(errorText.rectTransform, new Vector2(1, 1), new Vector2(-24, -14), new Vector2(300, 26), new Vector2(1, 1));
            else Place(errorText.rectTransform, new Vector2(0, 1), new Vector2(24, -244), new Vector2(422, 28));

            actionBtn = ButtonWithLabel("Action", t, "BET", 36, out actionBg, out actionText, OnAction);
            Place((RectTransform)actionBtn.transform, new Vector2(0, 0), new Vector2(24, btnY), new Vector2(422, btnH), new Vector2(0, 0));
            actionBtn.transition = Selectable.Transition.None;
            var hint = Label("Hint", actionBtn.transform, "SPACE", 16, new Color(0, 0, 0, 0.35f), TextAnchor.LowerRight, false);
            Place(hint.rectTransform, new Vector2(1, 0), new Vector2(-12, 6), new Vector2(100, 22), new Vector2(1, 0));

            betControls = new Selectable[] { betInput, autoInput, autoToggle, chipButtons[0], chipButtons[1], chipButtons[2], chipButtons[3] };
        }

        void BuildGraphCard(Transform root)
        {
            float cardH = docked ? 268f : 320f;
            var card = Panel("GraphCard", root, docked ? new Color(0.11f, 0.12f, 0.18f, 1f) : panel, rounded);
            Place(card.rectTransform, new Vector2(1, 0), docked ? new Vector2(-30, 16) : new Vector2(-40, 40), new Vector2(580, cardH), new Vector2(1, 0));
            if (docked) graphRT = card.rectTransform;
            var title = Label("Title", card.transform, "MULTIPLIER OVER TIME", 22, textMuted, TextAnchor.UpperLeft, true);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(24, -14), new Vector2(330, 28));
            graphValue = Label("Value", card.transform, "", 24, textMain, TextAnchor.UpperRight, true);
            Place(graphValue.rectTransform, new Vector2(1, 1), new Vector2(-22, -12), new Vector2(220, 30), new Vector2(1, 1));
            graphCaption = Label("Caption", card.transform, "", 17, textMuted, TextAnchor.UpperLeft, false);
            Place(graphCaption.rectTransform, new Vector2(0, 1), new Vector2(24, -42), new Vector2(530, 24));
            var crt = graphCaption.rectTransform;
            crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(0, 1);
            crt.anchoredPosition = new Vector2(24, -42); crt.sizeDelta = new Vector2(-46, 24);
            graphCaption.horizontalOverflow = HorizontalWrapMode.Wrap;
            graphCaption.verticalOverflow = VerticalWrapMode.Truncate;
            graphCaption.resizeTextForBestFit = true;
            graphCaption.resizeTextMinSize = 11;
            graphCaption.resizeTextMaxSize = 17;

            var g = new GameObject("Graph", typeof(RectTransform));
            g.transform.SetParent(card.transform, false);
            var grt = (RectTransform)g.transform;
            grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2(84, 40);
            grt.offsetMax = new Vector2(-24, -74);
            graph = g.AddComponent<CrashGraph>();
            graph.raycastTarget = false;

            for (int i = 0; i < 4; i++)
            {
                var l = Label("Y" + i, card.transform, "", 18, textMuted, TextAnchor.MiddleRight, false);
                var rt = l.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(1, 0.5f);
                float h = cardH - 40 - 74;
                rt.anchoredPosition = new Vector2(76, 40 + h * i / 3f);
                rt.sizeDelta = new Vector2(70, 24);
                graphLabels[i] = l;
            }

            // x axis (seconds) + markers labels
            graphX0 = Label("X0", card.transform, "0s", 16, textMuted, TextAnchor.UpperLeft, false);
            Place(graphX0.rectTransform, new Vector2(0, 0), new Vector2(84, 34), new Vector2(80, 20), new Vector2(0, 1));
            graphX1 = Label("X1", card.transform, "", 16, textMuted, TextAnchor.UpperRight, false);
            Place(graphX1.rectTransform, new Vector2(1, 0), new Vector2(-24, 34), new Vector2(120, 20), new Vector2(1, 1));
            graphTarget = Label("Target", g.transform, "", 16, graph.targetColor, TextAnchor.LowerRight, true);
            graphTarget.rectTransform.sizeDelta = new Vector2(200, 22);
            graphTarget.rectTransform.pivot = new Vector2(1, 0);
            graphCash = Label("CashMark", g.transform, "", 16, win, TextAnchor.LowerCenter, true);
            graphCash.rectTransform.sizeDelta = new Vector2(160, 22);
            graphCash.rectTransform.pivot = new Vector2(0.5f, 0);
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
            bool inDock = docked && dockRT != null && parent != null && parent.IsChildOf(dockRT);
            t.font = inDock ? (bold ? dockBoldFont : dockFont) : (bold ? boldFont : font);
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

        static Sprite MakeRounded(int size, int radius, float ppu = 100f)
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
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect,
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
