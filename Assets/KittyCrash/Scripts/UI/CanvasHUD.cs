using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace KittyCrash
{
    /// <summary>
    /// Proper Canvas UI for the game (built by Kitty Crash › Add Polish Pack). Same data as the old
    /// OnGUI CrashHUD, plus juice: the multiplier swells as it climbs, flashes green on cash-out and
    /// shakes red on distraction. Restyle freely in the Inspector — the script only fills in text/state.
    /// </summary>
    public class CanvasHUD : MonoBehaviour
    {
        [Header("Top-left")]
        public Text creditsText;
        public Text seasonText;
        public Text backendText;
        public Toggle apiToggle;

        [Header("Centre")]
        public Text multiplierText;
        public Text statusText;

        [Header("History")]
        public RectTransform historyRoot;

        [Header("Bet bar")]
        public InputField betInput;
        public Button halfButton, doubleButton, maxButton;
        public Toggle autoToggle;
        public InputField autoInput;
        public Button actionButton;
        public Text actionText;
        public Text errorText;

        [Header("Style")]
        public Font font;
        public Color ink = new Color(0.1f, 0.1f, 0.14f);
        public Color win = new Color(0.12f, 0.62f, 0.25f);
        public Color lose = new Color(0.85f, 0.15f, 0.15f);
        public Color muted = new Color(0.35f, 0.35f, 0.42f);
        public Color betColor = new Color(0.2f, 0.65f, 0.3f);
        public Color cashColor = new Color(0.95f, 0.6f, 0.1f);
        public Color idleColor = new Color(0.5f, 0.5f, 0.55f);

        RoundManager round;
        CatWalker cat;
        RectTransform multRT;
        Vector2 multBasePos;
        float pulse, shake;
        int historySignature = -1;
        readonly List<GameObject> chips = new List<GameObject>();

        void Start()
        {
            round = RoundManager.Instance;
#if UNITY_2023_1_OR_NEWER
            cat = FindAnyObjectByType<CatWalker>();
#else
            cat = FindObjectOfType<CatWalker>();
#endif
            if (multiplierText != null) { multRT = multiplierText.rectTransform; multBasePos = multRT.anchoredPosition; }

            if (actionButton != null) actionButton.onClick.AddListener(OnAction);
            if (halfButton != null) halfButton.onClick.AddListener(() => SetBet(Parse(betInput.text) / 2m));
            if (doubleButton != null) doubleButton.onClick.AddListener(() => SetBet(Parse(betInput.text) * 2m));
            if (maxButton != null) maxButton.onClick.AddListener(() => SetBet(round != null ? round.Balance : 0m));
            if (apiToggle != null) apiToggle.onValueChanged.AddListener(v => { if (round != null && v != round.useApi) round.SetUseApi(v); });

            if (round != null)
            {
                round.OnCashOut += (m, p) => pulse = 1f;
                round.OnCrash += (m, d) => shake = 1f;
            }
        }

        void Update()
        {
            if (round == null) return;

            bool typing = (betInput != null && betInput.isFocused) || (autoInput != null && autoInput.isFocused);
            if (!typing && InputReader.ActionPressed()) OnAction();

            UpdateTopLeft();
            UpdateCentre();
            UpdateBar();
            UpdateHistory();
        }

        // ── actions ────────────────────────────

        void OnAction()
        {
            if (round.Phase == RoundPhase.Idle)
            {
                if (!decimal.TryParse(betInput.text, NumberStyles.Number, CultureInfo.InvariantCulture, out var wager))
                {
                    round.LastError = "Enter a valid bet";
                    return;
                }
                float auto = 0f;
                if (autoToggle != null && autoToggle.isOn)
                    float.TryParse(autoInput.text, NumberStyles.Float, CultureInfo.InvariantCulture, out auto);
                round.PlaceBet(wager, auto);
            }
            else if (round.Phase == RoundPhase.Running)
            {
                round.CashOut();
            }
        }

        void SetBet(decimal v)
        {
            if (v < round.MinBet) v = round.MinBet;
            if (v > round.MaxBet) v = round.MaxBet;
            betInput.text = (System.Math.Floor(v * 100m) / 100m).ToString("0.##", CultureInfo.InvariantCulture);
        }

        static decimal Parse(string s)
            => decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0m;

        // ── views ──────────────────────────────

        void UpdateTopLeft()
        {
            if (creditsText != null) creditsText.text = $"{round.Balance:0.##} points";

            var ss = SeasonSystem.Instance;
            if (seasonText != null && ss != null)
            {
                string fest = ss.Get(ss.DominantSeason).festiveName;
                seasonText.text = (ss.InTransition ? $"{ss.CurrentSeason} → {ss.NextSeason}" : ss.CurrentSeason.ToString())
                                  + (string.IsNullOrEmpty(fest) ? "" : $"  ·  {fest}");
            }
            if (backendText != null) backendText.text = round.BackendName;

            if (apiToggle != null)
            {
                apiToggle.SetIsOnWithoutNotify(round.useApi);
                apiToggle.interactable = round.Phase != RoundPhase.Running && round.Phase != RoundPhase.Starting;
            }
        }

        void UpdateCentre()
        {
            string big, sub;
            Color c = ink;
            switch (round.Phase)
            {
                case RoundPhase.Connecting: big = "…"; sub = "Connecting…"; c = muted; break;
                case RoundPhase.Offline: big = "offline"; sub = $"Retrying in {Mathf.CeilToInt(round.PhaseTimeLeft)}s"; c = lose; break;
                case RoundPhase.Idle: big = "1.00x"; sub = "Place a bet to send kitty walking"; c = muted; break;
                case RoundPhase.Starting: big = "1.00x"; sub = "Kitty is stretching…"; c = muted; break;
                case RoundPhase.Running:
                    big = $"{CrashMath.Floor2(round.Multiplier):0.00}x";
                    if (round.CashedOut) c = win;
                    sub = cat != null ? $"Kitty walked {cat.DistanceThisRound:0.0} m" + (cat.IsFidgeting ? "   …sniff sniff" : "") : "Kitty is walking…";
                    break;
                default:
                    big = $"{round.Multiplier:0.00}x";
                    c = lose;
                    sub = cat != null ? $"Distracted by {Distractions.Pretty(cat.LastDistraction)}!" : "Distracted!";
                    break;
            }

            if (pulse > 0f) c = Color.Lerp(c, win, pulse);
            pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 1.5f);
            shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 2.5f);

            if (multiplierText != null)
            {
                multiplierText.text = big;
                multiplierText.color = c;
            }
            if (statusText != null) statusText.text = sub;

            if (multRT != null)
            {
                float grow = round.Phase == RoundPhase.Running
                    ? Mathf.Clamp01(Mathf.Log(Mathf.Max(1f, round.Multiplier)) / Mathf.Log(10f)) * 0.25f
                    : 0f;
                float s = 1f + grow + pulse * 0.15f + Mathf.Sin(Time.time * 6f) * 0.01f * grow * 4f;
                multRT.localScale = new Vector3(s, s, 1f);
                multRT.anchoredPosition = multBasePos + new Vector2(Mathf.Sin(Time.time * 70f) * 14f * shake, 0f);
            }
        }

        void UpdateBar()
        {
            bool idle = round.Phase == RoundPhase.Idle;
            if (betInput != null) betInput.interactable = idle;
            if (halfButton != null) halfButton.interactable = idle;
            if (doubleButton != null) doubleButton.interactable = idle;
            if (maxButton != null) maxButton.interactable = idle;
            if (autoToggle != null) autoToggle.interactable = idle;
            if (autoInput != null) autoInput.interactable = idle;

            string label;
            bool clickable = false;
            Color col = idleColor;
            switch (round.Phase)
            {
                case RoundPhase.Idle: label = "PLACE BET"; clickable = true; col = betColor; break;
                case RoundPhase.Starting: label = $"Bet {round.Wager:0.00} — starting…"; break;
                case RoundPhase.Running:
                    if (round.CashedOut) { label = $"Cashed out @ {round.CashOutMultiplier:0.00}x  +{round.Payout:0.00}"; col = win; }
                    else if (round.CashOutPending) label = "Cashing out…";
                    else { label = $"CASH OUT  {round.PotentialPayout:0.00}"; clickable = true; col = cashColor; }
                    break;
                case RoundPhase.Ended:
                    if (round.CashedOut) { label = $"You won {round.Payout:0.00}"; col = win; }
                    else { label = $"Lost {round.Wager:0.00}"; col = lose; }
                    break;
                default: label = "Not connected"; break;
            }

            if (actionText != null) actionText.text = label;
            if (actionButton != null)
            {
                actionButton.interactable = clickable;
                var img = actionButton.targetGraphic as Image;
                if (img != null) img.color = col;
                var cb = actionButton.colors;
                cb.disabledColor = Color.white; // keep our colour when not clickable
                actionButton.colors = cb;
            }
            if (errorText != null) errorText.text = round.LastError ?? "";
        }

        void UpdateHistory()
        {
            if (historyRoot == null) return;
            int sig = round.History.Count * 100000 + (round.History.Count > 0 ? Mathf.RoundToInt(round.History[0] * 100f) : 0);
            if (sig == historySignature) return;
            historySignature = sig;

            foreach (var c in chips) if (c != null) Destroy(c);
            chips.Clear();

            for (int i = 0; i < round.History.Count && i < 8; i++)
            {
                float h = round.History[i];
                var chip = new GameObject("Chip", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                chip.transform.SetParent(historyRoot, false);
                chip.GetComponent<Image>().color = h >= 2f ? win : lose;
                var le = chip.GetComponent<LayoutElement>();
                le.preferredWidth = 100; le.preferredHeight = 44;

                var t = new GameObject("Text", typeof(RectTransform), typeof(Text));
                t.transform.SetParent(chip.transform, false);
                var rt = (RectTransform)t.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                var tx = t.GetComponent<Text>();
                tx.font = font;
                tx.fontSize = 24;
                tx.fontStyle = FontStyle.Bold;
                tx.alignment = TextAnchor.MiddleCenter;
                tx.color = Color.white;
                tx.text = $"{h:0.00}x";
                chips.Add(chip);
            }
        }
    }
}
