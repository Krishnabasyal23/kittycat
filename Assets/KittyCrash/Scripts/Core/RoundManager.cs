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

        [Header("Round cycle (crash-game style)")]
        [Tooltip("Rounds run on their own: betting countdown (cat keeps running) → walk → distracted → countdown…\n" +
                 "Bets placed during the countdown join the next walk. Untick for the old 'bet starts the round' flow.")]
        public bool autoRounds = true;
        public float bettingCountdown = 8f;
        [Tooltip("Distractions used for rounds nobody bet on (purely visual).")]
        public string[] spectatorDistractions = { "fish", "sleep", "yarn" };

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
        public float WarmupSeconds => Backend != null ? Backend.WarmupSeconds : 0f;
        /// <summary>True during the first seconds of a round: cat walks, multiplier stays 1.00x, no cash-out.</summary>
        public bool IsWarmingUp => Phase == RoundPhase.Running && RunElapsed < WarmupSeconds;
        public float WarmupLeft => Mathf.Max(0f, WarmupSeconds - RunElapsed);
        public decimal MinBet => Backend != null ? Backend.MinBet : 1m;
        public decimal MaxBet => Backend != null ? Backend.MaxBet : 500m;
        public decimal PotentialPayout => CrashMath.Payout(Wager, CrashMath.Floor2(Multiplier));
        /// <summary>The player has real money in the current/last round.</summary>
        public bool HasBet { get; private set; }
        /// <summary>A bet was placed during the countdown and will join the next walk.</summary>
        public bool BetQueued { get; private set; }
        public decimal QueuedWager { get; private set; }
        public float QueuedAutoCashOut { get; private set; }
        public bool IsSpectating => Phase == RoundPhase.Running && !HasBet;
        public float RunElapsed => Time.time - runStartTime;

        public event Action<RoundPhase> OnPhaseChanged;
        public event Action OnRoundStarted;
        public event Action<float, string> OnCrash;          // crash multiplier, distraction id
        public event Action<float, decimal> OnCashOut;       // multiplier, payout

        float runStartTime, nextPollTime;
        bool pollInFlight, outcomeRequested;
        float spectatorCrash;
        readonly System.Random spectatorRng = new System.Random();
        QuantumProof spectatorProof;

        // ── provably fair (Schrödinger box) ──
        /// <summary>SHA-256 seal of this round's outcome, published before anyone can cash out.</summary>
        public string Commitment { get; private set; }
        /// <summary>The id the seal was made for (the server round id, or a local id for watch-only rounds).</summary>
        public string ProofRoundId { get; private set; }
        public QuantumProof LastProof { get; private set; }
        public FairnessResult LastCheck { get; private set; }
        public event Action OnCommitted;
        public event Action OnProofRevealed;
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
                    BetQueued = false;
                    EnterIdle();
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
                case RoundPhase.Idle:
                    if (autoRounds)
                    {
                        PhaseTimeLeft -= Time.deltaTime;
                        if (PhaseTimeLeft <= 0f) BeginRound();
                    }
                    break;
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
                        EnterIdle();
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
            if (Phase != RoundPhase.Idle) { LastError = "Bets open again after this walk"; return false; }
            if (wager < MinBet || wager > MaxBet) { LastError = $"Bet must be {MinBet:0}–{MaxBet:0}"; return false; }
            if (wager > Balance) { LastError = "Not enough points"; return false; }

            if (autoRounds)
            {
                BetQueued = true;
                QueuedWager = wager;
                QueuedAutoCashOut = autoCashOut;
                return true;
            }
            StartBackendRound(wager, autoCashOut);
            return true;
        }

        /// <summary>Take back a bet placed during the countdown (before the walk starts).</summary>
        public void CancelBet()
        {
            if (Phase == RoundPhase.Idle) BetQueued = false;
        }

        void EnterIdle()
        {
            PhaseTimeLeft = autoRounds ? bettingCountdown : 0f;
            SetPhase(RoundPhase.Idle);
        }

        void ResetRoundState()
        {
            RoundId = null;
            Multiplier = 1f;
            CashedOut = CashOutPending = pollInFlight = outcomeRequested = false;
            CashOutMultiplier = 0f;
            Payout = 0m;
            CrashPoint = -1f;
            Distraction = null;
            Commitment = null;
            ProofRoundId = null;
            LastProof = null;
            LastCheck = null;
            spectatorProof = null;
        }

        void BeginRound()
        {
            if (BetQueued)
            {
                BetQueued = false;
                if (QueuedWager <= Balance) { StartBackendRound(QueuedWager, QueuedAutoCashOut); return; }
                LastError = "Not enough points - watching this round";
            }
            StartSpectatorRound();
        }

        /// <summary>A round nobody bet on: same curve, local random crash point, no money involved.</summary>
        void StartSpectatorRound()
        {
            ResetRoundState();
            HasBet = false;
            Wager = 0m;
            string id = "watch-" + Guid.NewGuid().ToString("N");
            spectatorProof = Fairness.MakeOffline(id, spectatorRng, CrashMath.DefaultHouseEdge, CrashMath.DefaultMaxMultiplier, out double u);
            spectatorCrash = CrashMath.CrashFromUniform(u, CrashMath.DefaultHouseEdge, CrashMath.DefaultMaxMultiplier);
            runStartTime = Time.time;
            SetPhase(RoundPhase.Running);
            OnRoundStarted?.Invoke();
            Commitment = spectatorProof.commitment;
            ProofRoundId = id;
            OnCommitted?.Invoke();
        }

        string RandomSpectatorDistraction()
            => spectatorDistractions != null && spectatorDistractions.Length > 0
                ? spectatorDistractions[spectatorRng.Next(spectatorDistractions.Length)] : "fish";

        void StartBackendRound(decimal wager, float autoCashOut)
        {
            Wager = wager;
            AutoCashOut = autoCashOut >= 1.01f ? CrashMath.Floor2(autoCashOut) : 0f;
            ResetRoundState();
            HasBet = true;
            SetPhase(RoundPhase.Starting);

            Backend.StartRound(wager, r =>
            {
                if (Phase != RoundPhase.Starting) return;
                if (!r.ok)
                {
                    LastError = r.error;
                    HasBet = false;
                    if (r.sessionLost) { Connect(); return; }
                    if (autoRounds) StartSpectatorRound(); else EnterIdle();
                    return;
                }

                RoundId = r.roundId;
                Balance = r.balance;
                runStartTime = Time.time;
                nextPollTime = Time.time + pollInterval;
                SetPhase(RoundPhase.Running);
                OnRoundStarted?.Invoke();
                Commitment = r.commitment;
                ProofRoundId = r.roundId;
                if (!string.IsNullOrEmpty(Commitment)) OnCommitted?.Invoke();

                if (r.status == RoundStatus.Crashed) EndRound(1f, r.distraction);   // instant 1.00x
            });
        }

        public void CashOut()
        {
            if (Phase != RoundPhase.Running || !HasBet || CashedOut || CashOutPending || RoundId == null || IsWarmingUp) return;
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
                else if (r.status == RoundStatus.Interrupted)
                {
                    LastError = "Server restarted - your wager was refunded";
                    EndRound(Multiplier, null);
                    RefreshBalance();
                    return;
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
            float local = CrashMath.MultiplierAt(Mathf.Max(0f, RunElapsed - WarmupSeconds), GrowthRate);

            if (!HasBet)
            {
                if (local >= spectatorCrash) EndRound(spectatorCrash, RandomSpectatorDistraction());
                else Multiplier = local;
                return;
            }

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
            if (RunElapsed > WarmupSeconds + CrashMath.SecondsFor(CrashMath.DefaultMaxMultiplier, GrowthRate) + 5f)
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
                            runStartTime = Time.time - WarmupSeconds - CrashMath.SecondsFor(r.multiplier, GrowthRate);
                        break;
                    case RoundStatus.Crashed:
                        if (!CashOutPending) EndRound(r.multiplier, r.distraction);
                        break;
                    case RoundStatus.CashedOut:
                        if (!CashedOut) { CashedOut = true; CashOutMultiplier = r.multiplier; Payout = r.payout; FetchOutcome(id); }
                        break;
                    case RoundStatus.Interrupted:
                        LastError = "Server restarted - your wager was refunded";
                        EndRound(Multiplier, null);
                        RefreshBalance();
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
            RevealProof();
        }

        /// <summary>Open the box: fetch the revealed seal ingredients and check them.</summary>
        void RevealProof()
        {
            if (spectatorProof != null) { SetProof(spectatorProof, spectatorProof.crash_point_exact); return; }
            if (string.IsNullOrEmpty(RoundId) || string.IsNullOrEmpty(Commitment)) return;
            string id = RoundId;
            Backend.GetResult(id, r =>
            {
                if (id != ProofRoundId || r == null || !r.ok || r.proof == null) return;
                SetProof(r.proof, r.proof.crash_point_exact > 0 ? r.proof.crash_point_exact : r.crashPoint);
            });
        }

        void SetProof(QuantumProof p, double crash)
        {
            if (LastProof != null) return;   // already opened (cash-out path fetches twice)
            LastProof = p;
            LastCheck = Fairness.Verify(ProofRoundId, p, crash);
            if (!string.Equals(p.commitment, Commitment, StringComparison.OrdinalIgnoreCase)) LastCheck.hashOk = false; // must match what was published
            OnProofRevealed?.Invoke();
        }

        /// <summary>Re-read the balance from the backend (e.g. after the wallet panel added points).</summary>
        public void RefreshBalance()
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
            BetQueued = false;
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
