using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KittyCrash
{
    /// <summary>
    /// Title screen, menu, how to play, settings and the demo reset. Installs itself at runtime in any scene
    /// with a RoundManager (no scene or prefab edit).
    ///  • Title screen at start (can be switched off in Settings).
    ///  • MENU button (top-left) or Esc: wallet, safe play, panels to show, how to play, settings, demo reset.
    ///  • F1 = how to play, F5 = demo reset (asks first). The reset starts a fresh player: a new wallet with
    ///    the starting points and no breaks or limits. On the server it is only offered in DEMO_MODE;
    ///    the old wallet and its history stay on the server untouched.
    /// </summary>
    public class GameMenu : MonoBehaviour
    {
        public static GameMenu Instance { get; private set; }

        static readonly Color Dim = new Color(0f, 0f, 0f, 0.7f);
        static readonly Color PanelBg = new Color(0.12f, 0.11f, 0.19f, 0.98f);
        static readonly Color TitleBg = new Color(0.07f, 0.06f, 0.12f, 0.97f);
        static readonly Color Card = new Color(0.17f, 0.15f, 0.26f, 1f);
        static readonly Color Orange = new Color(1f, 0.65f, 0.18f, 1f);
        static readonly Color TextMain = new Color(0.96f, 0.95f, 1f, 1f);
        static readonly Color TextDim = new Color(0.64f, 0.61f, 0.77f, 1f);
        static readonly Color Red = new Color(1f, 0.35f, 0.42f, 1f);
        static readonly Color Green = new Color(0.31f, 0.88f, 0.54f, 1f);

        static bool titleShownThisRun;

        RoundManager rm;
        Font font, bold;
        RectTransform title, modal, content, menuButton;
        Text pageTitle;

        public bool IsOpen => (modal != null && modal.gameObject.activeSelf) || (title != null && title.gameObject.activeSelf);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded += (scene, mode) => TryAdd();
            TryAdd();
        }

        static void TryAdd()
        {
            if (RoundManager.Instance == null || FindAnyObjectByType<GameMenu>() != null) return;
            new GameObject("GameMenu").AddComponent<GameMenu>();
        }

        void Awake()
        {
            Instance = this;
            rm = RoundManager.Instance;
            font = Resources.Load<Font>("KittyCrashFonts/Fredoka-Medium");
            bold = Resources.Load<Font>("KittyCrashFonts/Fredoka-Bold");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (bold == null) bold = font;
            HudLayout.ApplyAudio();
            Build();
            if (HudLayout.TitleAtStart && !titleShownThisRun) ShowTitle();
            titleShownThisRun = true;
        }

        void OnDestroy()
        {
            if (Instance == this) { Instance = null; InputReader.MenuOpen = false; }
        }

        void Update()
        {
            InputReader.MenuOpen = IsOpen;
            PlaceMenuButton();

            if (InputReader.FieldFocused) return;
            var wallet = WalletPanel.Instance;
            if (Pressed(Hotkey.Escape) && wallet != null && wallet.IsOpen && !IsOpen) { wallet.Close(); return; }
            if (Pressed(Hotkey.Escape))
            {
                if (title.gameObject.activeSelf) return;
                if (modal.gameObject.activeSelf) Close(); else ShowMenu();
            }
            else if (Pressed(Hotkey.F1)) ShowHowTo();
            else if (Pressed(Hotkey.F5)) ShowReset();
        }

        void PlaceMenuButton()
        {
            // clean layout: under the logo; classic: next to the old server switch
            menuButton.anchoredPosition = HudLayout.Classic ? new Vector2(232f, -130f) : new Vector2(44f, -100f);
        }

        enum Hotkey { Escape, F1, F5 }

        static bool Pressed(Hotkey key)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return false;
            return key == Hotkey.Escape ? k.escapeKey.wasPressedThisFrame : key == Hotkey.F1 ? k.f1Key.wasPressedThisFrame : k.f5Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(key == Hotkey.Escape ? KeyCode.Escape : key == Hotkey.F1 ? KeyCode.F1 : KeyCode.F5);
#endif
        }

        // ── pages ─────────────────────────────────

        public void ShowTitle()
        {
            modal.gameObject.SetActive(false);
            title.gameObject.SetActive(true);
        }

        void Play() => title.gameObject.SetActive(false);

        /// <summary>Close the title screen and any menu page (screenshots, tests).</summary>
        public void CloseAll()
        {
            title.gameObject.SetActive(false);
            modal.gameObject.SetActive(false);
        }

        public void ShowMenu()
        {
            Page("Menu");
            Row(("WALLET", () => OpenWallet(w => w.ShowHome())), ("SAFE PLAY", ShowSafePlay));
            if (!HudLayout.Classic)
            {
                Line("Show on screen (this session)", TextDim, 22);
                Toggle("Multiplier graph", HudLayout.ShowGraph, v => HudLayout.ShowGraph = v);
                Toggle("Round history", HudLayout.ShowHistory, v => HudLayout.ShowHistory = v);
                Toggle("Crowd leaderboard", HudLayout.ShowLeaderboard, v => HudLayout.ShowLeaderboard = v);
                Toggle("Mood face", HudLayout.ShowMood, v => HudLayout.ShowMood = v);
            }
            var crowd = FindAnyObjectByType<CrowdMode>();
            if (crowd != null) Btn("CROWD MODE (PHONES JOIN WITH A QR CODE)", () => { Close(); crowd.Toggle(); }, Card);
            Row(("HOW TO PLAY", ShowHowTo), ("SETTINGS", ShowSettings));
            Row(("DEMO RESET", ShowReset), ("TITLE SCREEN", ShowTitle));
            Btn("BACK TO THE GAME", Close, Orange);
        }

        void OpenWallet(Action<WalletPanel> open)
        {
            var w = WalletPanel.Instance;
            if (w == null || !w.Online)
            {
                Page("Wallet");
                Line("The wallet needs the game server. Switch to online play in Settings.", TextMain, 24);
                Btn("Back", ShowMenu, Card);
                return;
            }
            Close();
            open(w);
        }

        public void ShowSafePlay()
        {
            Page("Safe play");
            Line("Catino is a game. Points have no cash value and can't be cashed out.", TextMain, 24, true);
            Line("-  Decide how long you'll play before you start, and stop when the time is up.", TextMain, 24);
            Line("-  Each walk is random. The next walk doesn't know about the last one, so chasing losses never helps.", TextMain, 24);
            Line("-  On average the game pays back less than you bet. Playing longer doesn't make a win more likely.", TextMain, 24);
            Line("-  Take breaks. If playing stops being fun, or you feel you have to play, stop.", TextMain, 24);
            Line("Your tools (server mode): a daily spend limit, a reality check reminder, a cool-off break and self-exclusion. Breaks and self-exclusion can't be shortened.", TextDim, 22);
            Btn("LIMITS, REALITY CHECK AND BREAKS", () => OpenWallet(w => w.ShowSafe()), Orange);
            Line("Need to talk to someone? ConnexOntario: call 1-866-531-2600 or text CONNEX to 247247 (free, 24/7, includes gambling support).", TextMain, 22);
            Line("In crisis: call or text 9-8-8 (Canada, 24/7). In danger: call 9-1-1.", TextMain, 22);
            Btn("Back", () => { if (title.gameObject.activeSelf) Close(); else ShowMenu(); }, Card);
        }

        public void ShowHowTo()
        {
            Page("How to play");
            Line("1.  While betting is open, choose how many points to bet and press BET (or Space).", TextMain, 24);
            Line("2.  The cat starts walking and the multiplier climbs.", TextMain, 24);
            Line("3.  Press CASH OUT (or Space) before the cat gets distracted. You win bet x multiplier.", TextMain, 24);
            Line("4.  If the cat gets distracted first, the bet is lost. Auto cash-out can cash out for you.", TextMain, 24);
            Line("5.  Each season changes one rule. The forecast while betting shows what's coming.", TextMain, 24);
            Line("Points are not money and can't be cashed out. Take breaks: Safe play is in the menu.", TextDim, 22);
            Line("Keys:  Space bet / cash out  ·  Esc menu  ·  W wallet  ·  C crowd mode  ·  M music  ·  F1 help  ·  F5 demo reset", TextDim, 20);
            Btn("BACK", () => { if (title.gameObject.activeSelf) Close(); else ShowMenu(); }, Orange);
        }

        public void ShowSettings()
        {
            Page("Settings");
            Toggle("Mute all sound", HudLayout.Muted, v => HudLayout.Muted = v);
            Text vol = null;
            void ShowVolume() => vol.text = $"Volume  {Mathf.RoundToInt(HudLayout.Volume * 100f)}%";
            var row = RowRect();
            Btn("-", () => { HudLayout.Volume = Mathf.Round(HudLayout.Volume * 10f - 1f) / 10f; ShowVolume(); }, Card, true, row);
            vol = MakeText(row, "", bold, 26, TextMain, TextAnchor.MiddleCenter);
            Btn("+", () => { HudLayout.Volume = Mathf.Round(HudLayout.Volume * 10f + 1f) / 10f; ShowVolume(); }, Card, true, row);
            ShowVolume();
            Toggle("Classic layout (everything on screen)", HudLayout.Classic, v => HudLayout.Classic = v);
            Toggle("Show the title screen at start", HudLayout.TitleAtStart, v => HudLayout.TitleAtStart = v);
            if (rm != null)
                Toggle("Play online (game server)", rm.useApi, v => { if (rm.useApi != v) rm.SetUseApi(v); });
            Btn("BACK", () => { if (title.gameObject.activeSelf) Close(); else ShowMenu(); }, Orange);
        }

        public void ShowReset()
        {
            Page("Demo reset");
            string blocked = ResetBlockedReason();
            if (blocked != null)
            {
                Line(blocked, TextMain, 24);
                Btn("Back", ShowMenu, Card);
                return;
            }
            Line("Start over as a new demo player? You get a fresh wallet with the starting points, and breaks and limits on this computer are cleared.", TextMain, 24);
            Line(rm.useApi ? "The old wallet and its history stay on the server." : "This resets the offline practice balance.", TextDim, 22);
            Btn("YES, START OVER", DemoReset, Orange);
            Btn("Cancel", ShowMenu, Card);
        }

        string ResetBlockedReason()
        {
            if (rm == null) return "The game isn't ready yet.";
            if (rm.HasBet || rm.BetQueued) return "Finish or cancel your bet first.";
            if (rm.Phase == RoundPhase.Starting || rm.Phase == RoundPhase.Running) return "Wait for this walk to end, then try again.";
            if (rm.useApi && (WalletPanel.Instance == null || !WalletPanel.Instance.DemoMode))
                return "Demo reset is only available when the server runs in demo mode.";
            return null;
        }

        void DemoReset()
        {
            if (ResetBlockedReason() != null) { ShowReset(); return; }
            if (rm.useApi)
            {
                if (rm.Backend is ApiBackend api) api.ForgetSession();
            }
            else if (rm.localBackend != null) rm.localBackend.ResetBalance();
            if (WalletPanel.Instance != null) WalletPanel.Instance.ResetForNewPlayer();
            rm.Connect();
            Page("Demo reset");
            Line("Done. You're a new player.", Green, 28, true);
            Btn("PLAY", Close, Orange);
        }

        public void Close() => modal.gameObject.SetActive(false);

        // ── building ──────────────────────────────

        void Build()
        {
            var cgo = new GameObject("GameMenuCanvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            cgo.AddComponent<GraphicRaycaster>();

            var mb = MakeButton(cgo.transform, "MENU", ShowMenu, new Color(0.11f, 0.12f, 0.18f, 0.92f), out var mbText);
            mbText.fontSize = 20;
            menuButton = (RectTransform)mb.transform;
            menuButton.anchorMin = menuButton.anchorMax = menuButton.pivot = new Vector2(0f, 1f);
            menuButton.sizeDelta = new Vector2(150f, 40f);
            PlaceMenuButton();

            // modal page: dim background + scrolling panel
            modal = Rect("MenuModal", cgo.transform);
            modal.anchorMin = Vector2.zero; modal.anchorMax = Vector2.one; modal.offsetMin = modal.offsetMax = Vector2.zero;
            modal.gameObject.AddComponent<Image>().color = Dim;
            var panel = Rect("Panel", modal);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(860f, 900f);
            panel.gameObject.AddComponent<Image>().color = PanelBg;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.65f, 0.18f, 0.5f);
            outline.effectDistance = new Vector2(2f, -2f);
            pageTitle = MakeText(panel, "", bold, 38, TextMain, TextAnchor.MiddleLeft);
            var tr = pageTitle.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
            tr.offsetMin = new Vector2(30f, -80f); tr.offsetMax = new Vector2(-110f, -16f);
            var close = MakeButton(panel, "X", Close, Card, out _);
            var cr = (RectTransform)close.transform;
            cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
            cr.anchoredPosition = new Vector2(-20f, -18f);
            cr.sizeDelta = new Vector2(60f, 56f);

            var scroll = Rect("Scroll", panel);
            scroll.anchorMin = Vector2.zero; scroll.anchorMax = Vector2.one;
            scroll.offsetMin = new Vector2(28f, 24f); scroll.offsetMax = new Vector2(-28f, -94f);
            scroll.gameObject.AddComponent<RectMask2D>();
            var sr = scroll.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.scrollSensitivity = 30f;
            sr.movementType = ScrollRect.MovementType.Clamped;
            content = Rect("Content", scroll);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content;
            sr.viewport = scroll;
            modal.gameObject.SetActive(false);

            BuildTitle(cgo.transform);
        }

        void BuildTitle(Transform root)
        {
            title = Rect("TitleScreen", root);
            title.anchorMin = Vector2.zero; title.anchorMax = Vector2.one; title.offsetMin = title.offsetMax = Vector2.zero;
            title.gameObject.AddComponent<Image>().color = TitleBg;

            var logo = MakeText(title, "CATINO", bold, 150, Orange, TextAnchor.MiddleCenter);
            Center(logo.rectTransform, 300f, new Vector2(1200f, 190f));
            logo.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(6f, -6f);
            var tag = MakeText(title, "Bet points, watch the cat walk, cash out before it gets distracted.", font, 30, TextDim, TextAnchor.MiddleCenter);
            Center(tag.rectTransform, 170f, new Vector2(1400f, 50f));

            var col = Rect("Buttons", title);
            Center(col, -110f, new Vector2(520f, 440f));
            var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 14f;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandHeight = false;
            Btn("PLAY", Play, Orange, true, col).GetComponent<LayoutElement>().minHeight = 84f;
            Btn("HOW TO PLAY", ShowHowTo, Card, true, col);
            Btn("SETTINGS", ShowSettings, Card, true, col);
            Btn("SAFE PLAY", ShowSafePlay, Card, true, col);
#if !UNITY_WEBGL
            Btn("QUIT", Application.Quit, Card, true, col);
#endif
            var foot = MakeText(title, "Points only, never money.  Players must be 19+.  v" + VersionFooter.ClientVersion, font, 20, TextDim, TextAnchor.MiddleCenter);
            Center(foot.rectTransform, -470f, new Vector2(1400f, 30f));
            title.gameObject.SetActive(false);
        }

        // ── content helpers (stacked inside the scrolling panel) ──

        void Page(string text)
        {
            pageTitle.text = text;
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            content.anchoredPosition = Vector2.zero;
            modal.gameObject.SetActive(true);
            modal.SetAsLastSibling();      // above the title screen when opened from it
        }

        Text Line(string text, Color color, int size = 26, bool strong = false)
        {
            var t = MakeText(content, text, strong ? bold : font, size, color, TextAnchor.UpperLeft);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        Button Btn(string label, Action onClick, Color bg, bool interactable = true, Transform parent = null)
        {
            var b = MakeButton(parent != null ? parent : content, label, onClick, bg, out _);
            b.gameObject.AddComponent<LayoutElement>().minHeight = 64f;
            b.interactable = interactable;
            return b;
        }

        RectTransform RowRect()
        {
            var row = Rect("Row", content);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10f;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true;
            row.gameObject.AddComponent<LayoutElement>().minHeight = 64f;
            return row;
        }

        void Row(params (string label, Action onClick)[] items)
        {
            var row = RowRect();
            foreach (var (label, onClick) in items) Btn(label, onClick, Card, true, row);
        }

        /// <summary>An on/off row: the button shows the state and flips it.</summary>
        void Toggle(string label, bool on, Action<bool> set)
        {
            Text text = null;
            Button b = null;
            void Show() { text.text = label + (on ? "   ON" : "   OFF"); b.targetGraphic.color = on ? new Color(0.24f, 0.21f, 0.36f, 1f) : Card; }
            b = MakeButton(content, "", () => { on = !on; set(on); Show(); }, Card, out text);
            b.gameObject.AddComponent<LayoutElement>().minHeight = 60f;
            text.alignment = TextAnchor.MiddleLeft;
            Show();
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Center(RectTransform r, float y, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(0f, y);
            r.sizeDelta = size;
        }

        Text MakeText(Transform parent, string text, Font f, int size, Color color, TextAnchor anchor)
        {
            var t = Rect("Text", parent).gameObject.AddComponent<Text>();
            t.font = f;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.text = text;
            t.raycastTarget = false;
            return t;
        }

        Button MakeButton(Transform parent, string label, Action onClick, Color bg, out Text text)
        {
            var go = Rect("Button", parent);
            var img = go.gameObject.AddComponent<Image>();
            img.color = bg;
            var b = go.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick());
            text = MakeText(go, label, bold, 24, bg == Orange ? new Color(0.09f, 0.07f, 0.12f, 1f) : TextMain, TextAnchor.MiddleCenter);
            var r = text.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(16f, 4f); r.offsetMax = new Vector2(-16f, -4f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return b;
        }
    }
}
