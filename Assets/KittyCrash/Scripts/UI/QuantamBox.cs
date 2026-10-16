using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KittyCrash
{
    public class QuantumBox : MonoBehaviour
    {
        public Vector2 position = new Vector2(28f, -178f);
        [Tooltip("Show the full proof panel at start (otherwise only the small badge; click it to open).")]
        public bool showProofAtStart = false;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly Color Purple = new Color(0.55f, 0.35f, 1f), Cyan = new Color(0.35f, 0.95f, 1f), Zero = new Color(0.16f, 0.13f, 0.26f);
        static readonly Color Good = new Color(0.4f, 0.95f, 0.55f), Bad = new Color(1f, 0.38f, 0.38f), Muted = new Color(0.65f, 0.67f, 0.8f);

        RoundManager rm;
        Camera cam;
        RectTransform area, panel, pill;
        Text pillText;
        Image pillBg;
        readonly Image[] pillDots = new Image[6];
        Image[,] cells = new Image[6, 6];          // [register, bit]  bit 0 = top (most significant)
        Text[] regLabels = new Text[6];
        Text title, sub, info, verdict;
        int[,] bits = new int[6, 6];
        float collapseT = -1f;
        bool revealed;
        float[] phase = new float[36];

        void Start()
        {
            rm = RoundManager.Instance;
            cam = Camera.main;
            Build();
            for (int i = 0; i < phase.Length; i++) phase[i] = Random.value * 10f;
            panel.gameObject.SetActive(showProofAtStart);
            if (rm != null)
            {
                rm.OnRoundStarted += () => { revealed = false; collapseT = -1f; Refresh(); };
                rm.OnCommitted += Refresh;
                rm.OnProofRevealed += Reveal;
            }
            Refresh();
        }

        // ── state → text ─────────────────────────

        void Refresh()
        {
            if (rm == null || info == null) return;
            if (revealed) return;
            string c = rm.Commitment;
            if (pillText != null)
            {
                pillText.text = string.IsNullOrEmpty(c) ? "QUANTUM SEAL" : "SEALED  " + c.Substring(0, Mathf.Min(6, c.Length));
                pillText.color = Color.white;
                pillBg.color = new Color(0.08f, 0.06f, 0.16f, 0.85f);
            }
            if (string.IsNullOrEmpty(c))
            {
                info.text = rm.Phase == RoundPhase.Running ? "Rolling the qubits..." : "Waiting for the next walk.\nEvery round is sealed\nbefore anyone can cash out.";
                verdict.text = "";
            }
            else
            {
                info.text = "<b>SEALED</b>  " + Short(c) + "\nThe crash point is already\nlocked inside the box.\nCash out = open the box early.";
                verdict.text = "outcome locked before betting";
                verdict.color = Muted;
            }
            sub.text = "quantum seal - provably fair";
        }
        void Reveal()
        {
            var p = rm.LastProof;
            var chk = rm.LastCheck;
            if (p == null || chk == null) return;

            // bits for the 6x6 grid: the measured qubit registers if we have them, otherwise the value's own bits
            long source = chk.value;
            if (p.HasQubits)
                for (int r = 0; r < 6; r++)
                {
                    int s = r < p.quantum_samples.Length ? p.quantum_samples[r] : 0;
                    for (int b = 0; b < 6; b++) bits[r, b] = (s >> (5 - b)) & 1;
                    regLabels[r].text = s.ToString(Inv);
                }
            else
                for (int r = 0; r < 6; r++)
                {
                    int s = (int)((source >> (6 * (5 - r))) & 63);
                    for (int b = 0; b < 6; b++) bits[r, b] = (s >> (5 - b)) & 1;
                    regLabels[r].text = s.ToString(Inv);
                }

            string src = p.source == "hybrid" ? "6 qubits XOR secure mask"
                       : p.source == "offline" ? "offline practice (classical)"
                       : p.source == "secure_fallback" ? "secure random (qubits unavailable)"
                       : p.source;
            var sb = new System.Text.StringBuilder();
            sb.Append("<b>OPENED</b>  ").Append(src).Append('\n');
            if (chk.mathOk.HasValue)
            {
                sb.Append("value ").Append(chk.value.ToString("N0", Inv)).Append('\n');
                sb.Append("u = ").Append(chk.u.ToString("0.0000", Inv)).Append("  ->  ")
                  .Append((1.0 - p.house_edge).ToString("0.00", Inv)).Append(" / u = ")
                  .Append(chk.recomputedCrash.ToString("0.00", Inv)).Append("x\n");
            }
            sb.Append(Check("seal matches", chk.hashOk)).Append('\n');
            if (chk.mathOk.HasValue) sb.Append(Check("crash math", chk.mathOk.Value)).Append('\n');
            if (chk.quantumOk.HasValue) sb.Append(Check("qubits used", chk.quantumOk.Value));
            info.text = sb.ToString();

            verdict.text = chk.AllOk ? "VERIFIED FAIR" : "MISMATCH - NOT FAIR";
            verdict.color = chk.AllOk ? Good : Bad;
            if (pillText != null)
            {
                pillText.text = chk.AllOk ? "FAIR - VERIFIED" : "NOT FAIR!";
                pillText.color = new Color(0.05f, 0.08f, 0.06f);
                pillBg.color = chk.AllOk ? new Color(0.4f, 0.95f, 0.55f, 0.95f) : new Color(1f, 0.38f, 0.38f, 0.95f);
            }
            revealed = true;
            collapseT = 0f;
        }

        void TogglePanel() => panel.gameObject.SetActive(!panel.gameObject.activeSelf);

        static string Check(string label, bool ok) =>
            label + "  <color=#" + (ok ? "66F28C" : "FF6161") + "><b>" + (ok ? "OK" : "FAIL") + "</b></color>";

        static string Short(string h) => h.Length > 14 ? h.Substring(0, 8) + "..." + h.Substring(h.Length - 6) : h;

        // ── animation ────────────────────────────

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) TogglePanel();
#else
            if (Input.GetKeyDown(KeyCode.Q)) TogglePanel();
#endif
            if (cam == null) cam = Camera.main;
            if (cam != null) { area.anchorMin = new Vector2(0f, cam.rect.y); area.anchorMax = new Vector2(1f, Mathf.Min(1f, cam.rect.y + cam.rect.height)); }
            bool live = rm != null && rm.Phase == RoundPhase.Running;
            // badge dots: shimmer while sealed, settle once opened
            for (int i = 0; i < 6; i++)
            {
                if (pillDots[i] == null) continue;
                if (revealed) pillDots[i].color = new Color(0.05f, 0.08f, 0.06f, 0.85f);
                else
                {
                    float k = 0.5f + 0.5f * Mathf.Sin(Time.time * (live ? 7f : 2f) + i * 1.3f);
                    pillDots[i].color = Color.Lerp(Purple, Cyan, k);
                }
            }
            if (!panel.gameObject.activeSelf) return;

            if (collapseT >= 0f) collapseT += Time.deltaTime;
            float t = Time.time;
            for (int r = 0; r < 6; r++)
                for (int b = 0; b < 6; b++)
                {
                    var img = cells[r, b];
                    int idx = r * 6 + b;
                    float when = idx * 0.03f;                       // collapse sweeps across the box
                    bool collapsed = revealed && collapseT >= when;
                    if (collapsed)
                    {
                        float since = collapseT - when;
                        Color target = bits[r, b] == 1 ? Cyan : Zero;
                        img.color = since < 0.12f ? Color.Lerp(Color.white, target, since / 0.12f) : target;
                        float pop = since < 0.12f ? 1.25f - since * 2f : 1f;
                        img.rectTransform.localScale = Vector3.one * pop;
                    }
                    else
                    {
                        // superposition: |0> and |1> at once — shimmering between the two
                        float speed = live ? 6f : 1.6f;
                        float k = 0.5f + 0.5f * Mathf.Sin(t * speed + phase[idx] * 3f) * Mathf.Cos(t * speed * 0.37f + phase[idx]);
                        var c = Color.Lerp(Purple, Cyan, k);
                        c.a = live ? 0.95f : 0.55f;
                        img.color = c;
                        img.rectTransform.localScale = Vector3.one * (live ? 0.85f + 0.15f * k : 0.9f);
                    }
                }
            if (!revealed) for (int r = 0; r < 6; r++) regLabels[r].text = live ? "?" : "";
        }

        // ── UI build ─────────────────────────────

        void Build()
        {
            var font = Resources.Load<Font>("KittyCrashFonts/Fredoka-Medium");
            var bold = Resources.Load<Font>("KittyCrashFonts/Fredoka-Bold");
            if (font == null)
            {
#if UNITY_2022_2_OR_NEWER
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            }
            if (bold == null) bold = font;

            var cgo = new GameObject("QuantumBoxCanvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 54;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            cgo.AddComponent<GraphicRaycaster>();

            area = Rect("GameArea", cgo.transform);
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = area.offsetMax = Vector2.zero;

            // small badge (always visible) — click it or press Q for the full proof
            pill = Rect("SealBadge", area);
            pill.anchorMin = pill.anchorMax = pill.pivot = new Vector2(0f, 1f);
            pill.anchoredPosition = position;
            pill.sizeDelta = new Vector2(300f, 52f);
            pillBg = pill.gameObject.AddComponent<Image>();
            pillBg.color = new Color(0.08f, 0.06f, 0.16f, 0.85f);
            var pb = pill.gameObject.AddComponent<Button>();
            pb.targetGraphic = pillBg;
            pb.onClick.AddListener(TogglePanel);
            for (int i = 0; i < 6; i++)
            {
                var d = Rect("dot" + i, pill);
                d.anchorMin = d.anchorMax = d.pivot = new Vector2(0f, 0.5f);
                d.anchoredPosition = new Vector2(12f + i * 14f, 0f);
                d.sizeDelta = new Vector2(10f, 22f);
                pillDots[i] = d.gameObject.AddComponent<Image>();
                pillDots[i].raycastTarget = false;
            }
            pillText = Label("BadgeText", pill, bold, 24, Color.white, TextAnchor.MiddleLeft, new Vector2(100f, 0f), new Vector2(200f, 52f));
            pillText.text = "QUANTUM SEAL";

            panel = Rect("SchrodingerBox", area);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = position + new Vector2(0f, -62f);
            panel.sizeDelta = new Vector2(520f, 300f);
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.05f, 0.12f, 0.84f);
            bg.raycastTarget = false;
            var ol = panel.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0.55f, 0.35f, 1f, 0.7f);
            ol.effectDistance = new Vector2(2f, -2f);

            title = Label("Title", panel, bold, 24, Cyan, TextAnchor.UpperLeft, new Vector2(14f, -10f), new Vector2(400f, 28f));
            title.text = "SCHRÖDINGER BOX";
            sub = Label("Sub", panel, font, 16, Muted, TextAnchor.UpperLeft, new Vector2(15f, -36f), new Vector2(400f, 20f));

            const float cell = 22f, gap = 4f;
            for (int r = 0; r < 6; r++)
            {
                for (int b = 0; b < 6; b++)
                {
                    var c = Rect($"q{r}_{b}", panel);
                    c.anchorMin = c.anchorMax = new Vector2(0f, 1f);
                    c.pivot = new Vector2(0.5f, 0.5f);
                    c.sizeDelta = new Vector2(cell, cell);
                    c.anchoredPosition = new Vector2(14f + cell / 2f + r * (cell + gap), -66f - cell / 2f - b * (cell + gap));
                    var img = c.gameObject.AddComponent<Image>();
                    img.raycastTarget = false;
                    cells[r, b] = img;
                }
                regLabels[r] = Label("reg" + r, panel, bold, 13, Muted, TextAnchor.UpperCenter,
                    new Vector2(14f + r * (cell + gap) - 6f, -66f - 6 * (cell + gap) - 2f), new Vector2(cell + 12f, 18f));
            }

            info = Label("Info", panel, font, 19, Color.white, TextAnchor.UpperLeft, new Vector2(178f, -64f), new Vector2(330f, 190f));
            info.supportRichText = true;
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            info.lineSpacing = 1.05f;

            verdict = Label("Verdict", panel, bold, 22, Good, TextAnchor.LowerLeft, new Vector2(178f, -268f), new Vector2(330f, 28f));
        }

        static RectTransform Rect(string n, Transform parent)
        {
            var go = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Text Label(string n, Transform parent, Font f, int size, Color c, TextAnchor a, Vector2 pos, Vector2 sizeDelta)
        {
            var r = Rect(n, parent);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = pos;
            r.sizeDelta = sizeDelta;
            var t = r.gameObject.AddComponent<Text>();
            t.font = f; t.fontSize = size; t.color = c; t.alignment = a; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var o = r.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.6f);
            o.effectDistance = new Vector2(1f, -1f);
            return t;
        }
    }
}
