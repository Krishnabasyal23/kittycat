using System;
using System.Collections.Generic;
using UnityEngine;

namespace KittyCrash
{
    public enum RoundPhase { Connecting, Offline, Idle, Starting, Running, Ended }

    /// <summary>
    /// Client-side round flow, built around the API:
    ///   Idle      — set your bet (+ optional auto cash-out)
    ///   Starting  — POST /api/rounds (server rolls the quantum crash point)
    ///   Running   — cat walks; multiplier animated locally with the server's curve and
    ///               synced by polling /state; Cash Out → POST /cashout
    ///               (after cashing out the cat keeps walking until the real crash point, then gets distracted)
    ///   Ended     — show the distraction + result, then back to Idle
    /// The server is the authority for every number that involves credits.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class RoundManager : MonoBehaviour
    {
        public static RoundManager Instance { get; private set; }

        [Header("Backend")]
        [Tooltip("Tick to use your FastAPI server; untick to play offline with LocalBackend.")]
        public bool useApi = false;
        public ApiBackend apiBackend;
        public LocalBackend localBackend;

        [Header("Timing")]
        public float pollInterval = 0.15f;
        public float resultDuration = 3.5f;
        public float reconnectDelay = 3f;
        public int historySize = 12;

        // ── state ──
        public RoundPhase Phase { get; private set; } = RoundPhase.Connecting;
        public decimal Balance { get; private set; }
        public float Multiplier { get; private set; } = 1f;
        public string RoundId { get; private set; }
        public decimal Wager { get; private set; }
        public float AutoCashOut { get; private set; }
        public bool CashedOut { get; private set; }
        public bool CashOutPending { get; private set; }
        public float CashOutMultiplier { get; private set; }
        public decimal Payout { get; private set; }
        /// <summary>Known once the round is over for us (crashed, or fetched after cash-out). -1 = unknown.</summary>
        public float CrashPoint { get; private set; } = -1f;
        public string Distraction { get; private set; }
        public string LastError { get; set; }
        public float PhaseTimeLeft { get; private set; }
        public readonly List<float> History = new List<float>();

        public ICrashBackend Backend { get; private set; }
        public string BackendName => Backend != null ? Backend.Name : "-";
        public float GrowthRate => Backend != null ? Backend.GrowthRate : CrashMath.DefaultGrowthRate;
        public decimal MinBet => Backend != null ? Backend.MinBet : 1m;
        public decimal MaxBet => Backend != null ? Backend.MaxBet : 500m;
        public decimal PotentialPayout => CrashMath.Payout(Wager, CrashMath.Floor2(Multiplier));
        public float RunElapsed => Time.time - runStartTime;

        public event Action<RoundPhase> OnPhaseChanged;
        public event Action OnRoundStarted;
        public event Action<float, string> OnCrash;          // crash multiplier, distraction id
        public event Action<float, decimal> OnCashOut;       // multiplier, payout

        float runStartTime, nextPollTime;
        bool pollInFlight, outcomeRequested;
        int connectAttempt;

        void Awake()
        {
            Instance = this;
            SelectBackend();
        }

        void Start() => Connect();

        void SelectBackend()
        {
            if (apiBackend == null) apiBackend = GetComponent<ApiBackend>();
            if (localBackend == null) localBackend = GetComponent<LocalBackend>();
            if (useApi && apiBackend == null) apiBackend = gameObject.AddComponent<ApiBackend>();
            if (!useApi && localBackend == null) localBackend = gameObject.AddComponent<LocalBackend>();
            Backend = useApi ? (ICrashBackend)apiBackend : localBackend;
        }

        /// <summary>Switch between API and offline (only between rounds).</summary>
        public void SetUseApi(bool api)
        {
            if (Phase == RoundPhase.Starting || Phase == RoundPhase.Running) return;
            useApi = api;
            SelectBackend();
            Connect();
        }

        public void Connect()
        {
            int attempt = ++connectAttempt;
            SetPhase(RoundPhase.Connecting);
            Backend.Connect(r =>
            {
                if (attempt != connectAttempt) return;
                if (r.ok)
                {
                    Balance = r.balance;
                    LastError = null;
                    SetPhase(RoundPhase.Idle);
                }
                else
                {
                    LastError = r.error;
                    PhaseTimeLeft = reconnectDelay;
                    SetPhase(RoundPhase.Offline);
                }
            });
        }

        void Update()
        {
            switch (Phase)
            {
                case RoundPhase.Offline:
                    PhaseTimeLeft -= Time.deltaTime;
                    if (PhaseTimeLeft <= 0f) Connect();
                    break;
                case RoundPhase.Running:
                    UpdateRunning();
                    break;
                case RoundPhase.Ended:
                    PhaseTimeLeft -= Time.deltaTime;
                    if (PhaseTimeLeft <= 0f)
                    {
                        SetPhase(RoundPhase.Idle);
                        RefreshBalance();
                    }
                    break;
            }
        }

        // ── player actions ─────────────────────────

        public bool PlaceBet(decimal wager, float autoCashOut)
        {
            LastError = null;
            wager = Math.Round(wager, 2, MidpointRounding.AwayFromZero);
            if (Phase != RoundPhase.Idle) { LastError = "Wait for the current walk to finish"; return false; }
            if (wager < MinBet || wager > MaxBet) { LastError = $"Bet must be {MinBet:0}–{MaxBet:0}"; return false; }
            if (wager > Balance) { LastError = "Not enough credits"; return false; }

            Wager = wager;
            AutoCashOut = autoCashOut >= 1.01f ? CrashMath.Floor2(autoCashOut) : 0f;
            RoundId = null;
            Multiplier = 1f;
            CashedOut = CashOutPending = pollInFlight = outcomeRequested = false;
            CashOutMultiplier = 0f;
            Payout = 0m;
            CrashPoint = -1f;
            Distraction = null;
            SetPhase(RoundPhase.Starting);

            Backend.StartRound(wager, r =>
            {
                if (Phase != RoundPhase.Starting) return;
                if (!r.ok)
                {
                    LastError = r.error;
                    SetPhase(RoundPhase.Idle);
                    if (r.sessionLost) Connect();
                    return;
                }

                RoundId = r.roundId;
                Balance = r.balance;
                runStartTime = Time.time;
                nextPollTime = Time.time + pollInterval;
                SetPhase(RoundPhase.Running);
                OnRoundStarted?.Invoke();

                if (r.status == RoundStatus.Crashed) EndRound(1f, r.distraction);   // instant 1.00x
            });
            return true;
        }

        public void CashOut()
        {
            if (Phase != RoundPhase.Running || CashedOut || CashOutPending || RoundId == null) return;
            CashOutPending = true;
            string id = RoundId;

            Backend.CashOut(id, r =>
            {
                CashOutPending = false;
                if (id != RoundId) return;
                if (!r.ok)
                {
                    LastError = r.error;
                    if (r.sessionLost) { EndRound(Multiplier, null); }
                    return;
                }

                Balance = r.balance;
                if (r.status == RoundStatus.CashedOut)
                {
                    CashedOut = true;
                    CashOutMultiplier = r.multiplier;
                    Payout = r.payout;
                    OnCashOut?.Invoke(r.multiplier, r.payout);
                }
                else
                {
                    LastError = "Too late — kitty was already distracted!";
                }
                FetchOutcome(id); // learn the real crash point (+ distraction)
            });
        }

        // ── running ────────────────────────────────

        void UpdateRunning()
        {
            float local = CrashMath.MultiplierAt(RunElapsed, GrowthRate);

            if (CrashPoint > 0f && local >= CrashPoint)
            {
                EndRound(CrashPoint, Distraction);
                return;
            }
            Multiplier = CrashPoint > 0f ? Mathf.Min(local, CrashPoint) : local;

            if (!CashedOut && !CashOutPending && AutoCashOut > 0f && Multiplier >= AutoCashOut)
                CashOut();

            if (!CashedOut && !CashOutPending && !outcomeRequested && !pollInFlight && Time.time >= nextPollTime)
                Poll();

            // Cashed out but still don't know where kitty stops → (re)ask for the result.
            if (CashedOut && CrashPoint < 0f && !outcomeRequested && Time.time >= nextPollTime)
            {
                nextPollTime = Time.time + pollInterval * 3f;
                FetchOutcome(RoundId);
            }

            // Safety net: never walk forever if a request got lost.
            if (RunElapsed > CrashMath.SecondsFor(CrashMath.DefaultMaxMultiplier, GrowthRate) + 5f)
                EndRound(Multiplier, Distraction);
        }

        void Poll()
        {
            pollInFlight = true;
            string id = RoundId;
            Backend.GetState(id, r =>
            {
                pollInFlight = false;
                nextPollTime = Time.time + pollInterval;
                if (id != RoundId || Phase != RoundPhase.Running) return;
                if (!r.ok)
                {
                    LastError = r.error;
                    if (r.sessionLost) EndRound(Multiplier, null);
                    return;
                }

                switch (r.status)
                {
                    case RoundStatus.Running:
                        // Keep the local animation in step with the server clock (only ever catch up, never go back).
                        if (r.multiplier > Multiplier + 0.02f)
                            runStartTime = Time.time - CrashMath.SecondsFor(r.multiplier, GrowthRate);
                        break;
                    case RoundStatus.Crashed:
                        if (!CashOutPending) EndRound(r.multiplier, r.distraction);
                        break;
                    case RoundStatus.CashedOut:
                        if (!CashedOut) { CashedOut = true; CashOutMultiplier = r.multiplier; Payout = r.payout; FetchOutcome(id); }
                        break;
                }
            });
        }

        void FetchOutcome(string id)
        {
            outcomeRequested = true;
            Backend.GetResult(id, r =>
            {
                if (id != RoundId || Phase != RoundPhase.Running) return;
                if (!r.ok || r.status == RoundStatus.Running)
                {
                    if (!CashedOut) EndRound(Multiplier, null);
                    else outcomeRequested = false; // try again via polling path
                    return;
                }
                CrashPoint = Mathf.Max(1f, Mathf.Round(r.crashPoint * 100f) / 100f);
                Distraction = r.distraction;
                if (!CashedOut || Multiplier >= CrashPoint) EndRound(CrashPoint, Distraction);
                // else: keep walking (we cashed out) until the multiplier reaches the crash point.
            });
        }

        void EndRound(float crash, string distraction)
        {
            if (Phase != RoundPhase.Running) return;
            Multiplier = crash;
            CrashPoint = crash;
            Distraction = distraction;
            PhaseTimeLeft = resultDuration;

            History.Insert(0, crash);
            if (History.Count > historySize) History.RemoveAt(History.Count - 1);

            OnCrash?.Invoke(crash, distraction);
            SetPhase(RoundPhase.Ended);
        }

        void RefreshBalance()
        {
            Backend.GetBalance(r =>
            {
                if (r.ok) Balance = r.balance;
                else if (r.sessionLost) Connect();
            });
        }

        public void ResetOfflineBalance()
        {
            if (useApi || localBackend == null || Phase == RoundPhase.Running || Phase == RoundPhase.Starting) return;
            localBackend.ResetBalance();
            Connect();
        }

        void SetPhase(RoundPhase p)
        {
            Phase = p;
            OnPhaseChanged?.Invoke(p);
        }
    }
}
