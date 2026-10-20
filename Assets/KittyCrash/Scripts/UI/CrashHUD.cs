using System.Globalization;
using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// Functional betting UI (OnGUI = zero setup). Replace with a Canvas UI during the polish pass —
    /// it only reads RoundManager properties and calls PlaceBet() / CashOut().
    /// Keys: Space/Enter = bet or cash out, N = skip to next season change.
    /// </summary>
    public class CrashHUD : MonoBehaviour
    {
        public RoundManager round;
        public CatWalker cat;

        string betText = "10";
        string autoText = "2.00";
        bool autoOn;

        GUIStyle huge, big, label, small, button;
        const float RefHeight = 540f;
        static readonly Color Ink = new Color(0.08f, 0.08f, 0.12f);
        static readonly Color Win = new Color(0.1f, 0.55f, 0.2f);
        static readonly Color Lose = new Color(0.8f, 0.12f, 0.12f);
        static readonly Color Muted = new Color(0.35f, 0.35f, 0.4f);

        void Start()
        {
            if (round == null) round = RoundManager.Instance;
#if UNITY_2023_1_OR_NEWER
            if (cat == null) cat = FindAnyObjectByType<CatWalker>();
#else
            if (cat == null) cat = FindObjectOfType<CatWalker>();
#endif
        }

        void Update()
        {
            if (round == null || !InputReader.ActionPressed()) return;
            if (round.Phase == RoundPhase.Idle) TryPlaceBet();
            else if (round.Phase == RoundPhase.Running) round.CashOut();
        }

        void TryPlaceBet()
        {
            if (!decimal.TryParse(betText, NumberStyles.Number, CultureInfo.InvariantCulture, out var wager))
            {
                round.LastError = "Enter a valid bet";
                return;
            }
            float auto = 0f;
            if (autoOn) float.TryParse(autoText, NumberStyles.Float, CultureInfo.InvariantCulture, out auto);
            round.PlaceBet(wager, auto);
        }

        void Styles()
        {
            if (huge != null) return;
            huge = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            big = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            label = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            button = new GUIStyle(GUI.skin.button) { fontSize = 17, fontStyle = FontStyle.Bold };
            big.normal.textColor = label.normal.textColor = small.normal.textColor = Ink;
        }

        void OnGUI()
        {
            if (round == null) return;
            Styles();
            float s = Screen.height / RefHeight;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            float W = Screen.width / s;

            TopLeft();
            History(W);
            Center(W);
            BetPanel(W);
        }

        void TopLeft()
        {
            GUILayout.BeginArea(new Rect(12, 10, 340, 160));
            GUILayout.Label($"Points  {round.Balance:0.##}", label);

            var ss = SeasonSystem.Instance;
            if (ss != null)
            {
                string txt = ss.InTransition
                    ? $"{ss.CurrentSeason} → {ss.NextSeason}  {ss.Blend * 100f:0}%"
                    : ss.CurrentSeason.ToString();
                GUILayout.Label(txt, label);
                string fest = ss.Get(ss.DominantSeason).festiveName;
                if (!string.IsNullOrEmpty(fest)) GUILayout.Label(fest, small);
            }

            GUILayout.Label(round.BackendName, small);
            bool idleish = round.Phase == RoundPhase.Idle || round.Phase == RoundPhase.Offline || round.Phase == RoundPhase.Ended;
            GUI.enabled = idleish;
            GUILayout.BeginHorizontal();
            bool api = GUILayout.Toggle(round.useApi, " Use API");
            if (api != round.useApi) round.SetUseApi(api);
            if (!round.useApi && GUILayout.Button("Reset points", GUILayout.Width(100))) round.ResetOfflineBalance();
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        void History(float W)
        {
            GUILayout.BeginArea(new Rect(W - 340, 10, 330, 30));
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            for (int i = 0; i < round.History.Count && i < 8; i++)
            {
                float h = round.History[i];
                var st = new GUIStyle(small) { fontStyle = FontStyle.Bold };
                st.normal.textColor = h >= 2f ? Win : Lose;
                GUILayout.Label($"{h:0.00}x", st);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void Center(float W)
        {
            string bigText;
            string sub;
            Color c = Ink;

            switch (round.Phase)
            {
                case RoundPhase.Connecting:
                    bigText = "…"; sub = "Connecting…"; c = Muted; break;
                case RoundPhase.Offline:
                    bigText = "offline"; sub = $"Retrying in {Mathf.CeilToInt(round.PhaseTimeLeft)}s"; c = Lose; break;
                case RoundPhase.Idle:
                    bigText = "1.00x"; sub = "Place a bet to send kitty walking"; c = Muted; break;
                case RoundPhase.Starting:
                    bigText = "1.00x"; sub = "Kitty is stretching… (rolling the quantum dice)"; c = Muted; break;
                case RoundPhase.Running:
                    bigText = $"{CrashMath.Floor2(round.Multiplier):0.00}x";
                    if (round.CashedOut) c = Win;
                    sub = cat != null ? $"Kitty walked {cat.DistanceThisRound:0.0} m" : "Kitty is walking…";
                    if (round.IsWarmingUp) { sub = $"Kitty is warming up… multiplier starts in {round.WarmupLeft:0.0}s"; c = Muted; }
                    if (cat != null && cat.IsFidgeting) sub += "   …sniff sniff";
                    break;
                default:
                    bigText = $"{round.Multiplier:0.00}x";
                    c = Lose;
                    sub = cat != null ? $"Distracted by {Distractions.Pretty(cat.LastDistraction)}!" : "Distracted!";
                    break;
            }

            huge.normal.textColor = c;
            GUI.Label(new Rect(0, 40, W, 80), bigText, huge);
            GUI.Label(new Rect(0, 115, W, 30), sub, big);
        }

        void BetPanel(float W)
        {
            // Slim one-row bar hugging the bottom edge, so the cat's path stays visible.
            float barH = 50f;
            var area = new Rect(8, RefHeight - barH - 6, W - 16, barH);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 8, area.y + 6, area.width - 16, barH - 12));
            GUILayout.BeginHorizontal();

            bool editable = round.Phase == RoundPhase.Idle;
            GUI.enabled = editable;
            GUILayout.Label("Bet", label, GUILayout.Width(32), GUILayout.Height(38));
            betText = GUILayout.TextField(betText, GUILayout.Width(64), GUILayout.Height(30));
            if (GUILayout.Button("½", GUILayout.Width(30), GUILayout.Height(30))) betText = Clamp(Parse(betText) / 2m).ToString("0.##", CultureInfo.InvariantCulture);
            if (GUILayout.Button("2×", GUILayout.Width(30), GUILayout.Height(30))) betText = Clamp(Parse(betText) * 2m).ToString("0.##", CultureInfo.InvariantCulture);
            if (GUILayout.Button("Max", GUILayout.Width(40), GUILayout.Height(30))) betText = Clamp(round.Balance).ToString("0.##", CultureInfo.InvariantCulture);
            GUILayout.Space(10);
            autoOn = GUILayout.Toggle(autoOn, " Auto @", GUILayout.Height(30));
            autoText = GUILayout.TextField(autoText, GUILayout.Width(48), GUILayout.Height(30));
            GUI.enabled = true;
            GUILayout.FlexibleSpace();

            string text;
            System.Action action = null;
            switch (round.Phase)
            {
                case RoundPhase.Idle:
                    text = "PLACE BET  (Space)"; action = TryPlaceBet; break;
                case RoundPhase.Starting:
                    text = $"Bet {round.Wager:0.00} — starting…"; break;
                case RoundPhase.Running:
                    if (round.CashedOut) text = $"Cashed out @ {round.CashOutMultiplier:0.00}x  +{round.Payout:0.00}";
                    else if (round.CashOutPending) text = "Cashing out…";
                    else if (round.IsWarmingUp) text = $"Warming up… {round.WarmupLeft:0.0}s";
                    else { text = $"CASH OUT  {round.PotentialPayout:0.00}  (Space)"; action = round.CashOut; }
                    break;
                case RoundPhase.Ended:
                    text = round.CashedOut
                        ? $"You won {round.Payout:0.00}  ({round.CashOutMultiplier:0.00}x)"
                        : $"Lost {round.Wager:0.00}";
                    break;
                default:
                    text = "Not connected"; break;
            }

            GUI.enabled = action != null;
            if (GUILayout.Button(text, button, GUILayout.Width(Mathf.Min(320f, area.width * 0.42f)), GUILayout.Height(38)) && action != null) action();
            GUI.enabled = true;

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Errors float just above the bar instead of taking space inside it.
            if (!string.IsNullOrEmpty(round.LastError))
            {
                var err = new GUIStyle(small) { alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold };
                err.normal.textColor = Lose;
                GUI.Label(new Rect(area.x, area.y - 24, area.width, 22), round.LastError, err);
            }
        }

        decimal Clamp(decimal v)
        {
            if (v < round.MinBet) v = round.MinBet;
            if (v > round.MaxBet) v = round.MaxBet;
            return System.Math.Floor(v * 100m) / 100m;
        }

        static decimal Parse(string s)
            => decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }
}
