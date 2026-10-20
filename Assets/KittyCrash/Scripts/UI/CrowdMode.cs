using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KittyCrash
{
    /// <summary>
    /// CROWD MODE: everyone in the room scans the QR code and bets on the same cat from their phone.
    ///  • Click the CROWD MODE pill (top-right) or press C. Needs USE API (the server keeps everyone in sync).
    ///  • Phone players ride as name tags above the cat. Cash out = the tag flies off green with the multiplier.
    ///    Kitty gets distracted = the tags still on board burn and fall.
    ///  • Leaderboard (top-left, under the quantum seal) shows the room's top 5.
    ///  • When phones have bets, the laptop auto-bets the minimum so the round runs on the server.
    /// Put it on the Managers object (next to RoundManager).
    /// </summary>
    public class CrowdMode : MonoBehaviour
    {
        /// <summary>Fires when new phones join (argument = how many), e.g. for a "pop" sound.</summary>
        public static event Action<int> OnPlayersJoined;
        int lastCount = -1;

        public bool startOn = false;
        public float pollInterval = 0.3f;
        [Tooltip("Max name tags around the cat (the rest show as '+N').")]
        public int maxTags = 8;
        [Tooltip("Top-right corner of the crowd pill + QR panel.")]
        public Vector2 panelPosition = new Vector2(-28f, -330f);
        public float qrSize = 200f;
        [Tooltip("Top-left corner of the leaderboard.")]
        public Vector2 boardPosition = new Vector2(28f, -250f);
        [Tooltip("Laptop bets the minimum when phones have bets, so the server runs the round.")]
        public bool autoHostBet = true;
        [Tooltip("Your name on the crowd leaderboard (the player on this screen).")]
        public string hostName = "PC Player";

        // ── server data ──
        [Serializable] class HostDto { public string join_url; public string[] qr; }
        [Serializable] class PlayerDto { public string name; public float bet; public float cashed_at; public float payout; public string status; }
        [Serializable] class LeaderDto { public string name; public float balance; public float best; }
        [Serializable] class StateDto { public string phase; public int round_no; public float seconds_left; public float multiplier; public float crash_point; public string join_url; public PlayerDto[] players; public LeaderDto[] leaderboard; public int player_count; }
        [Serializable] class OpenDto { public float seconds; }
        [Serializable] class HostBody { public string name; }

        RoundManager rm;
        ApiBackend api;
        Camera cam;
        CatWalker cat;
        Font font;

        bool on, polling, hostBetTried, refilling;
        string hostedSession, joinUrl;
        float nextPoll;
        StateDto state;
        readonly Dictionary<string, string> lastStatus = new Dictionary<string, string>();

        // ── UI ──
        RectTransform root, area, panel, tagLayer, board;
        Image pillBg;
        Text pillText, joinText, countText, boardText, moreText;
        RawImage qrImage;
        Texture2D qrTex;

        class Tag { public RectTransform rt; public Text text; public Image bg; public string name; }
        readonly List<Tag> tags = new List<Tag>();

        class Flyer { public RectTransform rt; public Text text; public Image bg; public Vector2 pos, vel; public float t, life; public bool fall; }
        readonly List<Flyer> flyers = new List<Flyer>();

        static readonly Color Orange = new Color(1f, 0.65f, 0.18f);
        static readonly Color Green = new Color(0.31f, 0.88f, 0.54f);
        static readonly Color Red = new Color(1f, 0.35f, 0.4f);
        static readonly Color Dark = new Color(0.07f, 0.06f, 0.12f, 0.88f);

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
            Build();
            if (rm != null)
            {
                rm.OnPhaseChanged += PhaseChanged;
                rm.OnCrash += (m, d) => BurnAll();
            }
            if (startOn) Toggle();
            else RefreshPill();
        }

        void OnDestroy()
        {
            if (rm != null) rm.OnPhaseChanged -= PhaseChanged;
            if (qrTex != null) Destroy(qrTex);
        }

        // ── on / off ───────────────────────────────

        void Toggle()
        {
            if (on) { on = false; RefreshPill(); return; }
            api = rm != null ? (rm.apiBackend != null ? rm.apiBackend : rm.GetComponent<ApiBackend>()) : null;
            if (rm == null || !rm.useApi || api == null || string.IsNullOrEmpty(api.SessionId))
            {
                pillText.text = "CLICK USE API FIRST";
                return;
            }
            on = true;
            Host();
            RefreshPill();
        }

        void Host()
        {
            hostedSession = api.SessionId;
            StartCoroutine(Send("POST", "/api/crowd/host", JsonUtility.ToJson(new HostBody { name = hostName }), (code, text) =>
            {
                if (code != 200) { joinText.text = "Server has no crowd mode yet\n(update KittyAPI)"; return; }
                var h = JsonUtility.FromJson<HostDto>(text);
                joinUrl = h.join_url;
                joinText.text = joinUrl.Replace("http://", "").Replace("https://", "");
                DrawQr(h.qr);
                if (rm.Phase == RoundPhase.Idle) Open(rm.PhaseTimeLeft);
            }));
        }

        void RefreshPill()
        {
            pillText.text = on ? "CROWD MODE: ON  (C)" : "CROWD MODE: OFF  (C)";
            pillBg.color = on ? Orange : Dark;
            pillText.color = on ? new Color(0.08f, 0.06f, 0.1f) : Color.white;
            panel.gameObject.SetActive(on);
            board.gameObject.SetActive(false);
            if (!on) { ClearTags(); state = null; }
        }

        void PhaseChanged(RoundPhase p)
        {
            hostBetTried = false;
            if (on && p == RoundPhase.Idle) Open(rm.PhaseTimeLeft);
        }

        void Open(float seconds)
        {
            var body = JsonUtility.ToJson(new OpenDto { seconds = Mathf.Max(1f, seconds) });
            StartCoroutine(Send("POST", "/api/crowd/open", body, (code, text) =>
            {
                if (code == 403) Host();   // server restarted or new session: register again
            }));
        }


        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (!InputReader.Typing && Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) Toggle();
#else
            if (!InputReader.Typing && Input.GetKeyDown(KeyCode.C)) Toggle();
#endif
            if (cam == null) cam = Camera.main;
            if (cam != null) { area.anchorMin = new Vector2(0f, cam.rect.y); area.anchorMax = new Vector2(1f, Mathf.Min(1f, cam.rect.y + cam.rect.height)); }

            UpdateFlyers();
            if (!on) return;

            if (api != null && api.SessionId != hostedSession && !string.IsNullOrEmpty(api.SessionId)) Host();
            if (!rm.useApi) { on = false; RefreshPill(); return; }

            // Laptop ran out of credits → start a fresh server session (50-point starting grant) so the crowd round can still run.
            if (rm.Phase == RoundPhase.Idle && rm.Balance < rm.MinBet && !refilling)
            {
                refilling = true;
                api.ForgetSession();
                rm.Connect();
            }
            if (refilling && rm.Balance >= rm.MinBet) refilling = false;

            if (!polling && Time.time >= nextPoll) Poll();

            // Phones bet but the laptop didn't → place the minimum so the round runs on the server.
            if (autoHostBet && !hostBetTried && rm.Phase == RoundPhase.Idle && !rm.BetQueued && rm.PhaseTimeLeft < 1.5f
                && state != null && state.phase == "betting" && state.players != null && state.players.Length > 0)
            {
                hostBetTried = true;
                rm.PlaceBet(rm.MinBet, 0f);
            }

            UpdateTags();
        }

        void Poll()
        {
            polling = true;
            StartCoroutine(Send("GET", "/api/crowd/state", null, (code, text) =>
            {
                polling = false;
                nextPoll = Time.time + pollInterval;
                if (code != 200 || !on) return;
                var s = JsonUtility.FromJson<StateDto>(text);
                if (s == null) return;
                if (state != null && s.round_no != state.round_no) lastStatus.Clear();
                state = s;
                if (lastCount >= 0 && s.player_count > lastCount) OnPlayersJoined?.Invoke(s.player_count - lastCount);
                lastCount = s.player_count;
                countText.text = s.player_count == 0 ? "Scan to play!" : s.player_count + (s.player_count == 1 ? " player joined" : " players joined");
                Diff();
                DrawBoard();
            }));
        }

        /// <summary>Spot phones that just cashed out → their tag flies off.</summary>
        void Diff()
        {
            if (state.players == null) return;
            foreach (var p in state.players)
            {
                lastStatus.TryGetValue(p.name, out var before);
                if (p.status == "cashed" && before == "riding" && rm.Phase == RoundPhase.Running)
                {
                    var t = tags.Find(x => x.name == p.name && x.rt.gameObject.activeSelf);
                    Vector2 from = t != null ? t.rt.anchoredPosition : CatHead() + new Vector2(0f, 160f);
                    Fly($"{p.name}  {p.cashed_at:0.00}x  +{p.payout:0}", from, new Vector2(UnityEngine.Random.Range(-60f, 60f), 190f), Green, 1.8f);
                }
                lastStatus[p.name] = p.status;
            }
        }

        // ── name tags on the cat ───────────────────

        readonly List<PlayerDto> riders = new List<PlayerDto>();

        void UpdateTags()
        {
            riders.Clear();
            if (state != null && state.players != null && rm.Phase == RoundPhase.Running && state.phase == "running")
                foreach (var p in state.players) if (p.status == "riding") riders.Add(p);

            int n = Mathf.Min(riders.Count, maxTags);
            while (tags.Count < n) tags.Add(MakeTag());
            Vector2 head = CatHead();
            for (int i = 0; i < tags.Count; i++)
            {
                var t = tags[i];
                bool show = i < n;
                t.rt.gameObject.SetActive(show);
                if (!show) continue;
                var p = riders[i];
                t.name = p.name;
                t.text.text = $"{p.name}  {p.bet:0}";
                // fan of tags over kitty's head, two rings so they don't overlap
                float spread = n <= 1 ? 0f : Mathf.Lerp(-62f, 62f, i / (float)(n - 1));
                float radius = (150f + (i % 2) * 58f) * CatSize.Ui;
                float a = (90f - spread) * Mathf.Deg2Rad;
                float bob = Mathf.Sin(Time.time * 3f + i) * 5f;
                t.rt.anchoredPosition = head + new Vector2(Mathf.Cos(a) * radius * 1.5f, Mathf.Sin(a) * radius + bob);
                t.rt.sizeDelta = new Vector2(t.text.preferredWidth + 26f, 40f);
            }
            moreText.gameObject.SetActive(riders.Count > n);
            if (riders.Count > n)
            {
                moreText.text = "+" + (riders.Count - n) + " more riding";
                moreText.rectTransform.anchoredPosition = head + new Vector2(0f, 290f * CatSize.Ui);
            }
        }

        void BurnAll()
        {
            foreach (var t in tags)
            {
                if (!t.rt.gameObject.activeSelf) continue;
                Fly(t.name + "  BURNED", t.rt.anchoredPosition, new Vector2(UnityEngine.Random.Range(-90f, 90f), -60f), Red, 1.4f, gravity: true);
                t.rt.gameObject.SetActive(false);
            }
            moreText.gameObject.SetActive(false);
        }

        void ClearTags()
        {
            foreach (var t in tags) t.rt.gameObject.SetActive(false);
            if (moreText != null) moreText.gameObject.SetActive(false);
        }

        Vector2 CatHead()
        {
            if (cat == null)
            {
#if UNITY_2023_1_OR_NEWER
                cat = FindAnyObjectByType<CatWalker>();
#else
                cat = FindObjectOfType<CatWalker>();
#endif
            }
            if (cat == null || cam == null) return Vector2.zero;
            var sr = cat.visual != null ? cat.visual.GetComponent<SpriteRenderer>() : null;
            Vector3 world = sr != null ? new Vector3(sr.bounds.center.x, sr.bounds.max.y, 0f) : cat.transform.position + Vector3.up * 1.5f;
            Vector3 screen = PixelGrid.WorldToScreen(cam, world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(tagLayer, screen, null, out var local);
            return local;
        }

        // ── flying text (cash-outs and burns) ──────

        void Fly(string text, Vector2 from, Vector2 vel, Color color, float life, bool gravity = false)
        {
            var go = R("Flyer", tagLayer);
            var bg = go.gameObject.AddComponent<Image>();
            bg.color = new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.9f);
            bg.raycastTarget = false;
            var t = T("Text", go, 24, color);
            Stretch(t.rectTransform);
            t.text = text;
            go.sizeDelta = new Vector2(t.preferredWidth + 30f, 44f);
            go.anchoredPosition = from;
            flyers.Add(new Flyer { rt = go, text = t, bg = bg, pos = from, vel = gravity ? vel + new Vector2(0f, 120f) : vel, life = life, fall = gravity });
        }

        void UpdateFlyers()
        {
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i];
                f.t += Time.deltaTime;
                if (f.fall) f.vel.y -= 900f * Time.deltaTime;   // burned tags fall
                else f.vel *= 1f - Time.deltaTime * 0.8f;
                f.pos += f.vel * Time.deltaTime;
                f.rt.anchoredPosition = f.pos;
                float pop = f.t < 0.15f ? Mathf.Lerp(0.6f, 1.15f, f.t / 0.15f) : Mathf.Lerp(1.15f, 1f, (f.t - 0.15f) * 4f);
                f.rt.localScale = Vector3.one * pop;
                float alpha = 1f - Mathf.Clamp01((f.t - f.life * 0.6f) / (f.life * 0.4f));
                var c = f.text.color; c.a = alpha; f.text.color = c;
                var b = f.bg.color; b.a = 0.9f * alpha; f.bg.color = b;
                if (f.t >= f.life) { Destroy(f.rt.gameObject); flyers.RemoveAt(i); }
            }
        }

        // ── leaderboard + QR ───────────────────────

        void DrawBoard()
        {
            bool show = state.leaderboard != null && state.leaderboard.Length > 0;
            board.gameObject.SetActive(show);
            if (!show) return;
            var sb = new StringBuilder("<color=#FFA62E>CROWD TOP 5</color>\n");
            string[] medal = { "1.", "2.", "3.", "4.", "5." };
            for (int i = 0; i < state.leaderboard.Length && i < 5; i++)
            {
                var p = state.leaderboard[i];
                sb.Append(medal[i]).Append(' ').Append(p.name).Append("  <color=#4FE08A>").Append(p.balance.ToString("0")).Append("</color>\n");
            }
            boardText.text = sb.ToString().TrimEnd('\n');
            board.sizeDelta = new Vector2(300f, 48f + 34f * Mathf.Min(5, state.leaderboard.Length));
        }

        void DrawQr(string[] rows)
        {
            if (rows == null || rows.Length == 0)
            {
                qrImage.gameObject.SetActive(false);
                joinText.text = "Open on your phone:\n" + joinText.text + "\n(pip install segno for a QR)";
                return;
            }
            qrImage.gameObject.SetActive(true);
            int n = rows.Length, border = 2, size = n + border * 2;
            if (qrTex != null) Destroy(qrTex);
            qrTex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n && x < rows[y].Length; x++)
                    if (rows[y][x] == '1') px[(size - 1 - (y + border)) * size + x + border] = new Color32(20, 16, 30, 255);
            qrTex.SetPixels32(px);
            qrTex.Apply();
            qrImage.texture = qrTex;
        }

        // ── building the UI ────────────────────────

        void Build()
        {
            var cgo = new GameObject("CrowdCanvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 57;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            cgo.AddComponent<GraphicRaycaster>();
            root = (RectTransform)cgo.transform;

            area = R("GameArea", root);
            Stretch(area);

            tagLayer = R("Tags", area);
            Stretch(tagLayer);

            moreText = T("More", tagLayer, 22, Color.white);
            moreText.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2f, -2f);
            moreText.gameObject.SetActive(false);

            // pill (toggle)
            var pill = R("CrowdPill", area);
            pill.anchorMin = pill.anchorMax = pill.pivot = new Vector2(1f, 1f);
            pill.anchoredPosition = panelPosition;
            pill.sizeDelta = new Vector2(qrSize + 40f, 46f);
            pillBg = pill.gameObject.AddComponent<Image>();
            var btn = pill.gameObject.AddComponent<Button>();
            btn.targetGraphic = pillBg;
            btn.onClick.AddListener(Toggle);
            pillText = T("Label", pill, 19, Color.white);
            Stretch(pillText.rectTransform);

            // QR panel
            panel = R("CrowdPanel", area);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = panelPosition + new Vector2(0f, -54f);
            panel.sizeDelta = new Vector2(qrSize + 40f, qrSize + 110f);
            var pbg = panel.gameObject.AddComponent<Image>(); pbg.color = Dark; pbg.raycastTarget = false;

            var title = T("Title", panel, 24, Orange);
            Place(title.rectTransform, -8f, 32f);
            title.text = "PLAY ON YOUR PHONE";

            var qr = R("QR", panel);
            qr.anchorMin = qr.anchorMax = qr.pivot = new Vector2(0.5f, 1f);
            qr.anchoredPosition = new Vector2(0f, -44f);
            qr.sizeDelta = new Vector2(qrSize, qrSize);
            qrImage = qr.gameObject.AddComponent<RawImage>();
            qrImage.raycastTarget = false;

            joinText = T("Url", panel, 17, new Color(0.8f, 0.78f, 0.9f));
            Place(joinText.rectTransform, -(qrSize + 50f), 26f);
            joinText.text = "connecting...";

            countText = T("Count", panel, 20, Green);
            Place(countText.rectTransform, -(qrSize + 76f), 28f);
            countText.text = "Scan to play!";

            // leaderboard
            board = R("CrowdBoard", area);
            board.anchorMin = board.anchorMax = board.pivot = new Vector2(0f, 1f);
            board.anchoredPosition = boardPosition;
            board.sizeDelta = new Vector2(300f, 100f);
            var bbg = board.gameObject.AddComponent<Image>(); bbg.color = Dark; bbg.raycastTarget = false;
            boardText = T("Text", board, 22, Color.white);
            boardText.alignment = TextAnchor.UpperLeft;
            boardText.supportRichText = true;
            boardText.lineSpacing = 1.1f;
            var br = boardText.rectTransform; br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = new Vector2(16f, 10f); br.offsetMax = new Vector2(-12f, -10f);
            board.gameObject.SetActive(false);

            panel.gameObject.SetActive(false);
        }

        Tag MakeTag()
        {
            var rt = R("Tag", tagLayer);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.08f, 0.16f, 0.88f);
            bg.raycastTarget = false;
            var t = T("Text", rt, 21, Color.white);
            Stretch(t.rectTransform);
            return new Tag { rt = rt, text = t, bg = bg };
        }

        // ── HTTP ───────────────────────────────────

        IEnumerator Send(string method, string path, string json, Action<long, string> done)
        {
            if (api == null) yield break;
            string url = api.baseUrl.TrimEnd('/') + path;
            using (var req = new UnityWebRequest(url, method))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                if (json != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                if (!string.IsNullOrEmpty(api.SessionId)) req.SetRequestHeader("X-Session-ID", api.SessionId);
                req.timeout = 8;
                yield return req.SendWebRequest();
                done?.Invoke(req.result == UnityWebRequest.Result.ConnectionError ? 0 : req.responseCode, req.downloadHandler.text);
            }
        }

        // ── helpers ────────────────────────────────

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
