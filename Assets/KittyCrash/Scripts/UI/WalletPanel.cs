using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KittyCrash
{
    /// <summary>
    /// Wallet and play-safe tools for server mode (Phase 3). Mirrors the phone page:
    ///  • 19+ gate before play, wallet (daily free points, demo packs, mock lottery tickets),
    ///  • demo checkout with generic payment choices and a required "simulated" checkbox, receipts, history,
    ///  • daily spend limit, reality check, cool-off, self-exclusion, out-of-points page.
    /// Every rule is enforced by the server; this only shows it. Installs itself at runtime in any scene
    /// with a RoundManager (no scene or prefab edit). Key W or the WALLET button opens it.
    /// </summary>
    public class WalletPanel : MonoBehaviour
    {
        public static WalletPanel Instance { get; private set; }

        // ── server JSON (field names match KittyAPI) ──
        [Serializable] class DailyDto { public int points; public bool available; }
        [Serializable] class ProductDto { public string product_id, label, price, currency; public int points; public bool once_per_day, available; }
        [Serializable] class LotteryDto { public int points, redeemed_today, daily_max; public bool available; }
        [Serializable] class PendingDto { public int daily_spend_limit_points; public string effective_at; }
        [Serializable] class OptionsDto { public int[] cool_off_hours, self_exclusion_days, reality_check_minutes; public int limit_increase_delay_hours; }
        [Serializable] class RgDto { public bool age_confirmed, can_play; public int min_age, daily_spend_limit_points, spent_today, reality_check_minutes; public PendingDto pending_limit; public string cooling_off_until, self_excluded_until; public OptionsDto options; }
        [Serializable] class WalletDto { public string server_time; public int balance; public DailyDto daily_free; public ProductDto[] products; public LotteryDto lottery; public bool demo_mode; public RgDto responsible; }
        [Serializable] class ReceiptDto { public string payment_id, provider, label, price, currency, status, created_at, notice; public int points; }
        [Serializable] class ReceiptList { public ReceiptDto[] items; }
        [Serializable] class EntryDto { public string kind, created_at; public int amount_points, balance_after; }
        [Serializable] class EntryList { public EntryDto[] items; }
        [Serializable] class ActivityDto { public int minutes, bets, wagered, won, net; }
        [Serializable] class ErrorDto { public string detail; }
        [Serializable] class PurchaseBody { public string product; public bool confirmed; }
        [Serializable] class TicketBody { public string ticket_id, numbers; }

        static readonly Color Dim = new Color(0f, 0f, 0f, 0.62f);
        static readonly Color PanelBg = new Color(0.12f, 0.11f, 0.19f, 0.98f);
        static readonly Color Card = new Color(0.17f, 0.15f, 0.26f, 1f);
        static readonly Color Orange = new Color(1f, 0.65f, 0.18f, 1f);
        static readonly Color TextMain = new Color(0.96f, 0.95f, 1f, 1f);
        static readonly Color TextDim = new Color(0.64f, 0.61f, 0.77f, 1f);
        static readonly Color Red = new Color(1f, 0.35f, 0.42f, 1f);
        static readonly Color Green = new Color(0.31f, 0.88f, 0.54f, 1f);
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        RoundManager rm;
        Font font, bold;
        RectTransform buttons, modal, content;
        Text titleText, walletLabel, statusText;
        Button closeButton;
        WalletDto wallet;
        bool busy, gateShown, oopShown, closable = true;
        float nextRefresh, nextReality;
        string sessionStart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded += (scene, mode) => TryAdd();
            TryAdd();
        }

        static void TryAdd()
        {
            if (RoundManager.Instance == null || FindAnyObjectByType<WalletPanel>() != null) return;
            new GameObject("WalletPanel").AddComponent<WalletPanel>();
        }

        ApiBackend Api => rm != null && rm.useApi ? rm.Backend as ApiBackend : null;
        bool Connected => Api != null && !string.IsNullOrEmpty(Api.SessionId)
                          && rm.Phase != RoundPhase.Connecting && rm.Phase != RoundPhase.Offline;
        public bool IsOpen => modal != null && modal.gameObject.activeSelf;

        void Awake()
        {
            Instance = this;
            rm = RoundManager.Instance;
            font = Resources.Load<Font>("KittyCrashFonts/Fredoka-Medium");
            bold = Resources.Load<Font>("KittyCrashFonts/Fredoka-Bold");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (bold == null) bold = font;
            Build();
        }

        void Update()
        {
            bool online = Connected;
            buttons.gameObject.SetActive(online);
            if (!online) { if (IsOpen && closable) Close(); gateShown = false; return; }

            if (!InputReader.Typing && WPressed()) { if (IsOpen) { if (closable) Close(); } else ShowHome(); }

            if (Time.unscaledTime >= nextRefresh && !busy)
            {
                nextRefresh = Time.unscaledTime + 5f;
                StartCoroutine(Refresh());
            }
            if (wallet == null) return;

            // 19+ gate: the server refuses bets and purchases until the player confirms
            if (!wallet.responsible.age_confirmed && !gateShown) { gateShown = true; ShowAgeGate(); return; }

            // out of points while betting is open: show the ways back (and a break)
            bool broke = rm.Phase == RoundPhase.Idle && rm.Balance < rm.MinBet;
            if (broke && !oopShown && !IsOpen && wallet.responsible.age_confirmed) { oopShown = true; ShowOutOfPoints(); }
            if (!broke) oopShown = false;

            // reality check every N minutes (the player's choice, default 15)
            if (sessionStart != null && Time.unscaledTime >= nextReality && !IsOpen)
            {
                nextReality = Time.unscaledTime + Mathf.Max(1, wallet.responsible.reality_check_minutes) * 60f;
                StartCoroutine(RealityCheck());
            }
        }

        static bool WPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.wKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.W);
#endif
        }

        // ── server calls ──────────────────────────

        IEnumerator Call(string method, string path, string json, string idemKey, Action<long, string> done)
        {
            var api = Api;
            if (api == null || string.IsNullOrEmpty(api.SessionId)) { done(0, "{\"detail\":\"Not connected to the server\"}"); yield break; }
            using (var req = new UnityWebRequest(api.baseUrl.TrimEnd('/') + path, method))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                if (json != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                req.SetRequestHeader("X-Session-ID", api.SessionId);
                if (!string.IsNullOrEmpty(idemKey)) req.SetRequestHeader("Idempotency-Key", idemKey);
                req.timeout = 8;
                yield return req.SendWebRequest();
                done(req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : "");
            }
        }

        static bool Ok(long code) => code >= 200 && code < 300;

        static string Detail(string text)
        {
            try { var e = JsonUtility.FromJson<ErrorDto>(text); if (e != null && !string.IsNullOrEmpty(e.detail)) return e.detail; }
            catch (ArgumentException) { }
            return "Can't reach the server";
        }

        static T[] List<T, TWrap>(string json, Func<TWrap, T[]> items)
        {
            var w = JsonUtility.FromJson<TWrap>("{\"items\":" + (string.IsNullOrEmpty(json) ? "[]" : json) + "}");
            return w != null ? items(w) ?? new T[0] : new T[0];
        }

        IEnumerator Refresh()
        {
            busy = true;
            yield return Call("GET", "/api/wallet", null, null, (code, text) =>
            {
                if (!Ok(code)) return;
                wallet = JsonUtility.FromJson<WalletDto>(text);
                if (sessionStart == null && wallet != null)
                {
                    sessionStart = wallet.server_time;
                    nextReality = Time.unscaledTime + Mathf.Max(1, wallet.responsible.reality_check_minutes) * 60f;
                }
            });
            busy = false;
            if (wallet != null) walletLabel.text = $"WALLET  ·  {wallet.balance} pts";
        }

        void AfterMoneyChange()
        {
            rm.RefreshBalance();
            nextRefresh = 0f;
        }

        static string NewKey() => "pc-" + Guid.NewGuid().ToString("N");

        static string When(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "";
            return DateTime.TryParse(iso, Inv, DateTimeStyles.RoundtripKind, out var t) ? t.ToLocalTime().ToString("g", Inv) : iso;
        }

        // ── pages ─────────────────────────────────

        void ShowHome()
        {
            StartCoroutine(Call("GET", "/api/wallet", null, null, (code, text) =>
            {
                if (!Ok(code)) { Page("Wallet"); Line(Detail(text), Red); return; }
                wallet = JsonUtility.FromJson<WalletDto>(text);
                Page("Wallet");
                Line($"Balance: {wallet.balance} points", TextMain, 34, true);
                var daily = wallet.daily_free;
                Btn(daily.available ? $"CLAIM {daily.points} FREE POINTS" : "FREE POINTS CLAIMED TODAY", () => StartCoroutine(ClaimDaily()), Orange, daily.available);
                if (wallet.demo_mode)
                {
                    Banner("DEMO MODE: payments are simulated. No real money is ever charged.");
                    foreach (var p in wallet.products)
                    {
                        var product = p;
                        string tail = p.once_per_day ? "  ·  once a day" : "";
                        Btn($"{p.label}  ·  ${p.price} → {p.points} points{tail}" + (p.available ? "" : "  (bought today)"),
                            () => ShowCheckout(product), Card, p.available);
                    }
                    Line($"Mock lottery ticket: {wallet.lottery.points} points, {wallet.lottery.redeemed_today}/{wallet.lottery.daily_max} today. Mock tickets only, not real lottery tickets.", TextDim, 22);
                    var id = Input("Ticket id (MOCK-XXXX-XXXX-XXXX)", InputField.ContentType.Standard);
                    var nums = Input("Numbers, e.g. 01 02 04 08 16", InputField.ContentType.Standard);
                    var msg = Line("", TextDim, 22);
                    Btn("REDEEM TICKET", () => StartCoroutine(Redeem(id.text, nums.text, msg)), Orange, wallet.lottery.available);
                }
                else Line("Point packs are off on this server.", TextDim, 22);
                Row(("RECEIPTS & HISTORY", ShowHistory), ("PLAY SAFE", ShowSafe));
            }));
        }

        IEnumerator ClaimDaily()
        {
            yield return Call("POST", "/api/wallet/daily-free", "{}", null, (code, text) =>
            {
                if (Ok(code)) { AfterMoneyChange(); ShowHome(); } else Toast(Detail(text));
            });
        }

        IEnumerator Redeem(string ticketId, string numbers, Text msg)
        {
            var body = JsonUtility.ToJson(new TicketBody { ticket_id = (ticketId ?? "").Trim().ToUpperInvariant(), numbers = (numbers ?? "").Trim() });
            yield return Call("POST", "/api/wallet/lottery/redeem", body, NewKey(), (code, text) =>
            {
                if (Ok(code)) { AfterMoneyChange(); ShowHome(); }
                else { msg.text = Detail(text); msg.color = Red; }
            });
        }

        void ShowCheckout(ProductDto p)
        {
            string key = NewKey();    // one key per checkout: retries can never charge twice
            string method = "";
            Page("Demo checkout");
            Banner("SIMULATED PAYMENT · DEMO ONLY\nNo card is read and no real money is charged.");
            Line($"{p.label}   ${p.price} {p.currency}", TextMain, 28, true);
            Line($"You get {p.points} points", TextMain, 26);
            Line("Pay with (demo)", TextDim, 22);
            Button pay = null;
            Toggle agree = null;
            Button wallet_ = null, card = null;
            void Refresh_() { pay.interactable = agree.isOn && method != ""; Highlight(wallet_, method == "wallet"); Highlight(card, method == "card"); }
            wallet_ = Btn("Phone wallet (demo)", () => { method = "wallet"; Refresh_(); }, Card);
            card = Btn("Card (demo)", () => { method = "card"; Refresh_(); }, Card);
            agree = Check("I understand this is a simulated demo payment and no real money will be charged.");
            agree.onValueChanged.AddListener(_ => Refresh_());
            var msg = Line("", TextDim, 22);
            pay = Btn($"PAY ${p.price} (DEMO)", () =>
            {
                pay.interactable = false;
                var body = JsonUtility.ToJson(new PurchaseBody { product = p.product_id, confirmed = true });
                StartCoroutine(Call("POST", "/api/wallet/purchase", body, key, (code, text) =>
                {
                    if (code == 402) { msg.text = "Demo payment declined. No points added."; msg.color = Red; Refresh_(); return; }
                    if (!Ok(code)) { msg.text = Detail(text); msg.color = Red; Refresh_(); return; }
                    AfterMoneyChange();
                    ShowReceipt(JsonUtility.FromJson<ReceiptDto>(text));
                }));
            }, Orange, false);
            Btn("Cancel", ShowHome, Card);
        }

        void ShowReceipt(ReceiptDto r)
        {
            Page("Receipt (simulated)");
            Banner(r.notice);
            Line($"Item: {r.label}", TextMain, 26);
            Line($"Price: ${r.price} {r.currency}", TextMain, 26);
            Line($"Points: +{r.points}", TextMain, 26);
            Line($"Status: {r.status}   ·   {When(r.created_at)}", TextMain, 24);
            Line($"Payment id {r.payment_id} (provider {r.provider})", TextDim, 18);
            Btn("BACK TO WALLET", ShowHome, Orange);
        }

        void ShowHistory()
        {
            StartCoroutine(HistoryRoutine());
        }

        IEnumerator HistoryRoutine()
        {
            ReceiptDto[] receipts = new ReceiptDto[0];
            EntryDto[] entries = new EntryDto[0];
            string error = null;
            yield return Call("GET", "/api/wallet/receipts?limit=20", null, null, (code, text) =>
            {
                if (Ok(code)) receipts = List<ReceiptDto, ReceiptList>(text, w => w.items); else error = Detail(text);
            });
            yield return Call("GET", "/api/wallet/ledger?limit=30", null, null, (code, text) =>
            {
                if (Ok(code)) entries = List<EntryDto, EntryList>(text, w => w.items); else error = Detail(text);
            });
            Page("History");
            if (error != null) Line(error, Red);
            Line("Simulated receipts", TextMain, 26, true);
            if (receipts.Length == 0) Line("No purchases.", TextDim, 22);
            foreach (var r in receipts) Line($"{r.label}  ·  {r.status}  ·  +{r.points}   {When(r.created_at)}", TextMain, 22);
            Line("Points ledger", TextMain, 26, true);
            foreach (var e in entries)
                Line($"{Kind(e.kind)}   {(e.amount_points > 0 ? "+" : "")}{e.amount_points} → {e.balance_after}   {When(e.created_at)}", TextMain, 22);
            Btn("BACK", ShowHome, Card);
        }

        static string Kind(string k)
        {
            switch (k)
            {
                case "starting_grant": return "Welcome points";
                case "daily_free": return "Daily free";
                case "topup": return "Demo top-up";
                case "parking_bundle": return "Demo parking bundle";
                case "lottery_ticket": return "Mock lottery ticket";
                case "wager": return "Bet";
                case "payout": return "Cash-out";
                case "refund": return "Refund";
                default: return k;
            }
        }

        void ShowSafe()
        {
            StartCoroutine(Call("GET", "/api/wallet/limits", null, null, (code, text) =>
            {
                if (!Ok(code)) { Page("Play safe"); Line(Detail(text), Red); return; }
                var v = JsonUtility.FromJson<RgDto>(text);
                Page("Play safe");
                if (!string.IsNullOrEmpty(v.self_excluded_until)) Warn($"Self-excluded until {When(v.self_excluded_until)}.");
                else if (!string.IsNullOrEmpty(v.cooling_off_until)) Warn($"Taking a break until {When(v.cooling_off_until)}.");

                Line("Daily spend limit", TextMain, 26, true);
                string now = v.daily_spend_limit_points > 0 ? $"{v.daily_spend_limit_points} points ({Mathf.Max(0, v.daily_spend_limit_points - v.spent_today)} left today)" : "none";
                Line($"Now: {now}   ·   spent today {v.spent_today}", TextDim, 22);
                if (v.pending_limit != null && !string.IsNullOrEmpty(v.pending_limit.effective_at))
                    Line($"Change waiting: {(v.pending_limit.daily_spend_limit_points > 0 ? v.pending_limit.daily_spend_limit_points.ToString() : "no limit")} from {When(v.pending_limit.effective_at)}", TextDim, 22);
                var limit = Input("Points per day", InputField.ContentType.IntegerNumber);
                var msg = Line($"Lowering applies now. Raising or removing waits {v.options.limit_increase_delay_hours} hours.", TextDim, 20);
                Row(("SET LIMIT", () =>
                {
                    if (!int.TryParse(limit.text, NumberStyles.Integer, Inv, out int n) || n <= 0) { msg.text = "Enter a number of points."; msg.color = Red; return; }
                    StartCoroutine(Send("PUT", "/api/wallet/limits", $"{{\"daily_spend_limit_points\":{n}}}", msg));
                }), ("REMOVE LIMIT", () => StartCoroutine(Send("PUT", "/api/wallet/limits", "{\"daily_spend_limit_points\":null}", msg))));

                Line("Reality check", TextMain, 26, true);
                var row = RowRect();
                foreach (int m in v.options.reality_check_minutes)
                {
                    int minutes = m;
                    var b = Btn($"every {m} min", () =>
                    {
                        nextReality = Time.unscaledTime + minutes * 60f;
                        StartCoroutine(Send("PUT", "/api/wallet/limits", $"{{\"reality_check_minutes\":{minutes}}}", msg));
                    }, Card, true, row);
                    Highlight(b, m == v.reality_check_minutes);
                }

                Line("Take a break", TextMain, 26, true);
                var coolOk = Check("Pause betting and purchases. It can't be shortened.");
                row = RowRect();
                foreach (int h in v.options.cool_off_hours)
                {
                    int hours = h;
                    string label = h < 24 ? $"{h} hour" : h == 24 ? "1 day" : $"{h / 24} days";
                    Btn(label, () =>
                    {
                        if (!coolOk.isOn) { msg.text = "Tick the box to start a break."; msg.color = Red; return; }
                        StartCoroutine(Send("POST", "/api/wallet/cool-off", $"{{\"hours\":{hours},\"confirm\":true}}", msg));
                    }, Card, true, row);
                }

                Line("Self-exclusion", TextMain, 26, true);
                Warn("Blocks all betting and purchases on this wallet for the whole period. It cannot be undone early.");
                var exclOk = Check("I understand and want to self-exclude.");
                row = RowRect();
                foreach (int d in v.options.self_exclusion_days)
                {
                    int days = d;
                    Btn($"{d} days", () =>
                    {
                        if (!exclOk.isOn) { msg.text = "Tick the box to self-exclude."; msg.color = Red; return; }
                        StartCoroutine(Send("POST", "/api/wallet/self-exclude", $"{{\"days\":{days},\"confirm\":true}}", msg));
                    }, Card, true, row);
                }
                Line("Need to talk to someone? ConnexOntario: 1-866-531-2600 (free, 24/7).", TextDim, 20);
                Btn("BACK TO WALLET", ShowHome, Card);
            }));
        }

        IEnumerator Send(string method, string path, string json, Text msg)
        {
            yield return Call(method, path, json, null, (code, text) =>
            {
                if (Ok(code)) { nextRefresh = 0f; ShowSafe(); }
                else { msg.text = Detail(text); msg.color = Red; }
            });
        }

        void ShowAgeGate()
        {
            Page("Are you 19 or older?", false);
            Line("Server play uses points in a wallet. You must be 19 or older to bet or buy demo packs.", TextMain, 26);
            Line("Points only, no real money. Play for fun and take breaks.", TextDim, 22);
            var ok = Check("I confirm I am 19 or older.");
            var msg = Line("", TextDim, 22);
            Button go = null;
            go = Btn("CONFIRM", () =>
            {
                go.interactable = false;
                StartCoroutine(Call("POST", "/api/wallet/age-confirm", "{\"confirm\":true}", null, (code, text) =>
                {
                    if (Ok(code)) { nextRefresh = 0f; closable = true; Close(); }
                    else { msg.text = Detail(text); msg.color = Red; go.interactable = true; }
                }));
            }, Orange, false);
            ok.onValueChanged.AddListener(v => go.interactable = v);
            Btn("I'm under 19: practise offline instead", () => { closable = true; Close(); rm.SetUseApi(false); }, Card);
        }

        void ShowOutOfPoints()
        {
            Page("Out of points");
            Line("You can keep watching. To play again:", TextDim, 24);
            if (wallet.daily_free.available) Btn($"CLAIM {wallet.daily_free.points} FREE POINTS", () => StartCoroutine(ClaimDaily()), Orange);
            else Line("Free points are back tomorrow.", TextDim, 22);
            if (wallet.demo_mode) Btn("Demo point packs or a mock ticket", ShowHome, Card);
            Btn("Take a break", ShowSafe, Card);
            Btn("Just watch", Close, Card);
        }

        IEnumerator RealityCheck()
        {
            ActivityDto act = null;
            yield return Call("GET", "/api/wallet/activity?since=" + UnityWebRequest.EscapeURL(sessionStart), null, null, (code, text) =>
            {
                if (Ok(code)) act = JsonUtility.FromJson<ActivityDto>(text);
            });
            if (act == null || IsOpen) yield break;
            Page("Reality check");
            Line($"Playing for {act.minutes} minutes", TextMain, 28, true);
            Line($"Bets: {act.bets}", TextMain, 26);
            Line($"Points bet: {act.wagered}", TextMain, 26);
            Line($"Points won: {act.won}", TextMain, 26);
            Line($"Net: {(act.net > 0 ? "+" : "")}{act.net}", TextMain, 26, true);
            Btn("KEEP PLAYING", Close, Orange);
            Btn("Take a break", ShowSafe, Card);
            Btn("See my history", ShowHistory, Card);
        }

        // ── UI build ──────────────────────────────

        void Build()
        {
            var cgo = new GameObject("WalletCanvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 85;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            cgo.AddComponent<GraphicRaycaster>();

            // small buttons under the quantum seal badge
            buttons = Rect("WalletButtons", cgo.transform);
            buttons.anchorMin = buttons.anchorMax = buttons.pivot = new Vector2(0f, 1f);
            buttons.anchoredPosition = new Vector2(28f, -244f);
            buttons.sizeDelta = new Vector2(300f, 116f);
            var wb = MakeButton(buttons, "WALLET", ShowHome, Orange, out walletLabel);
            Place(wb.transform, 0f, 52f);
            var sb = MakeButton(buttons, "PLAY SAFE", ShowSafe, Card, out _);
            Place(sb.transform, -60f, 52f);

            // modal: dim background + scrolling panel
            modal = Rect("WalletModal", cgo.transform);
            modal.anchorMin = Vector2.zero; modal.anchorMax = Vector2.one; modal.offsetMin = modal.offsetMax = Vector2.zero;
            modal.gameObject.AddComponent<Image>().color = Dim;

            var panel = Rect("Panel", modal);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(820f, 940f);
            panel.gameObject.AddComponent<Image>().color = PanelBg;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.65f, 0.18f, 0.5f);
            outline.effectDistance = new Vector2(2f, -2f);

            titleText = MakeText(panel, "", bold, 36, TextMain, TextAnchor.MiddleLeft);
            var tr = titleText.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
            tr.offsetMin = new Vector2(28f, -76f); tr.offsetMax = new Vector2(-100f, -16f);

            closeButton = MakeButton(panel, "X", Close, Card, out _);
            var cr = (RectTransform)closeButton.transform;
            cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
            cr.anchoredPosition = new Vector2(-20f, -18f);
            cr.sizeDelta = new Vector2(60f, 56f);

            var scroll = Rect("Scroll", panel);
            scroll.anchorMin = Vector2.zero; scroll.anchorMax = Vector2.one;
            scroll.offsetMin = new Vector2(24f, 24f); scroll.offsetMax = new Vector2(-24f, -90f);
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

            statusText = MakeText(cgo.transform, "", font, 22, TextMain, TextAnchor.MiddleCenter);
            var st = statusText.rectTransform;
            st.anchorMin = st.anchorMax = st.pivot = new Vector2(0.5f, 0f);
            st.anchoredPosition = new Vector2(0f, 60f);
            st.sizeDelta = new Vector2(900f, 40f);

            modal.gameObject.SetActive(false);
        }

        void Page(string title, bool canClose = true)
        {
            closable = canClose;
            closeButton.gameObject.SetActive(canClose);
            titleText.text = title;
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            content.anchoredPosition = Vector2.zero;
            modal.gameObject.SetActive(true);
        }

        public void Close()
        {
            if (!closable) return;
            modal.gameObject.SetActive(false);
        }

        void Toast(string text)
        {
            statusText.text = text;
            CancelInvoke(nameof(ClearToast));
            Invoke(nameof(ClearToast), 4f);
        }

        void ClearToast() => statusText.text = "";

        // content helpers (stacked top to bottom inside the scrolling panel)

        Text Line(string text, Color color, int size = 26, bool strong = false)
        {
            var t = MakeText(content, text, strong ? bold : font, size, color, TextAnchor.UpperLeft);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        void Banner(string text) => Boxed(text, Orange);
        void Warn(string text) => Boxed(text, Red);

        void Boxed(string text, Color color)
        {
            var box = Rect("Box", content);
            box.gameObject.AddComponent<Image>().color = new Color(color.r, color.g, color.b, 0.14f);
            var pad = box.gameObject.AddComponent<VerticalLayoutGroup>();
            pad.padding = new RectOffset(16, 16, 12, 12);
            pad.childControlWidth = true; pad.childControlHeight = true; pad.childForceExpandHeight = false;
            var t = MakeText(box, text, bold, 22, color, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
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

        InputField Input(string placeholder, InputField.ContentType type)
        {
            var go = Rect("Input", content);
            go.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.06f, 0.11f, 1f);
            go.gameObject.AddComponent<LayoutElement>().minHeight = 64f;
            var field = go.gameObject.AddComponent<InputField>();
            var text = MakeText(go, "", font, 26, TextMain, TextAnchor.MiddleLeft);
            Inset(text.rectTransform);
            text.supportRichText = false;
            var ph = MakeText(go, placeholder, font, 24, TextDim, TextAnchor.MiddleLeft);
            Inset(ph.rectTransform);
            ph.fontStyle = FontStyle.Italic;
            field.textComponent = text;
            field.placeholder = ph;
            field.contentType = type;
            field.characterLimit = 40;
            return field;
        }

        Toggle Check(string label)
        {
            var go = Rect("Check", content);
            go.gameObject.AddComponent<LayoutElement>().minHeight = 56f;
            var toggle = go.gameObject.AddComponent<Toggle>();
            var box = Rect("Box", go);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0f, 0.5f);
            box.sizeDelta = new Vector2(40f, 40f);
            var boxImg = box.gameObject.AddComponent<Image>();
            boxImg.color = TextMain;
            var mark = Rect("Mark", box);
            mark.anchorMin = Vector2.zero; mark.anchorMax = Vector2.one; mark.offsetMin = new Vector2(7f, 7f); mark.offsetMax = new Vector2(-7f, -7f);
            var markImg = mark.gameObject.AddComponent<Image>();
            markImg.color = Orange;
            var t = MakeText(go, label, font, 24, TextMain, TextAnchor.MiddleLeft);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = new Vector2(56f, 0f); t.rectTransform.offsetMax = Vector2.zero;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            toggle.targetGraphic = boxImg;
            toggle.graphic = markImg;
            toggle.isOn = false;
            return toggle;
        }

        static void Highlight(Button b, bool on)
        {
            if (b == null) return;
            var o = b.GetComponent<Outline>();
            if (o == null) o = b.gameObject.AddComponent<Outline>();
            o.effectColor = Orange;
            o.effectDistance = new Vector2(3f, -3f);
            o.enabled = on;
        }

        // primitives

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
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
            Inset(text.rectTransform);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return b;
        }

        static void Inset(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(16f, 4f); r.offsetMax = new Vector2(-16f, -4f);
        }

        static void Place(Transform t, float y, float height)
        {
            var r = (RectTransform)t;
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, y);
            r.sizeDelta = new Vector2(0f, height);
        }
    }
}
