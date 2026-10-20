using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// Offline copy of the Python server's rules (round_manager.py / crash_engine.py / economy.py),
    /// so you can build and test without the API running. Same growth rate, house edge, limits,
    /// one-round-per-session rule and response shapes. Uses normal RNG instead of the quantum one.
    /// </summary>
    public class LocalBackend : MonoBehaviour, ICrashBackend
    {
        [Header("Server constants (keep in sync with crash_engine.py / economy.py)")]
        public float growthRate = CrashMath.DefaultGrowthRate;
        [Tooltip("Seconds of walking at 1.00x before the multiplier starts.")]
        public float warmupSeconds = 6f;
        [Range(0f, 0.2f)] public float houseEdge = CrashMath.DefaultHouseEdge;
        public float maxMultiplier = CrashMath.DefaultMaxMultiplier;
        public float initialBalance = 1000f;
        public float minBet = 1f;
        public float maxBet = 500f;
        public string[] distractions = { "fish", "sleep", "yarn" };

        [Header("Testing")]
        [Tooltip("Fake network delay per request (seconds).")]
        public float simulatedLatency = 0.05f;
        [Tooltip("Extra delay when starting a round, like the quantum RNG on the server.")]
        public float simulatedStartDelay = 0.6f;
        public bool logCrashPoints;

        class Round
        {
            public string id;
            public decimal wager;
            public float crashPoint, crashTime, startTime;
            public string distraction;
            public RoundStatus status;
            public QuantumProof proof;
            public float cashOutMultiplier;
            public decimal payout;
        }

        const string BalanceKey = "KittyCrash_LocalBalance";
        readonly Dictionary<string, Round> rounds = new Dictionary<string, Round>();
        readonly System.Random rng = new System.Random();
        Round active;
        decimal balance;

        public string Name => "Offline (local)";
        public float GrowthRate => growthRate;
        public float WarmupSeconds => warmupSeconds;
        public decimal MinBet => (decimal)minBet;
        public decimal MaxBet => (decimal)maxBet;

        static float Now => Time.realtimeSinceStartup;

        public void Connect(Action<BalanceResult> done)
        {
            string saved = PlayerPrefs.GetString(BalanceKey, "");
            balance = string.IsNullOrEmpty(saved) ? Round2((decimal)initialBalance) : BackendUtil.ParseMoney(saved);
            Respond(done, new BalanceResult { ok = true, balance = balance }, simulatedLatency);
        }

        public void ResetBalance()
        {
            balance = Round2((decimal)initialBalance);
            Save();
        }

        public void StartRound(decimal wager, Action<StartRoundResult> done)
        {
            string err = Validate(wager);
            if (err != null) { Respond(done, new StartRoundResult { ok = false, error = err }, simulatedLatency); return; }

            StartCoroutine(After(simulatedStartDelay + simulatedLatency, () =>
            {
                string err2 = Validate(wager);
                if (err2 != null) { done(new StartRoundResult { ok = false, error = err2 }); return; }

                // provably fair, offline edition: seal the outcome first, reveal it after the round
                string id = Guid.NewGuid().ToString("N");
                var proof = Fairness.MakeOffline(id, rng, houseEdge, maxMultiplier, out double u);

                var r = new Round
                {
                    id = id,
                    proof = proof,
                    wager = wager,
                    crashPoint = CrashMath.CrashFromUniform(u, houseEdge, maxMultiplier),
                    distraction = distractions.Length > 0 ? distractions[rng.Next(distractions.Length)] : "fish",
                    startTime = Now + warmupSeconds,   // multiplier starts after the warm-up walk
                    status = RoundStatus.Running,
                };
                r.crashTime = Mathf.Log(r.crashPoint) / growthRate;
                if (logCrashPoints) Debug.Log($"[KittyCrash local] round {r.id.Substring(0, 6)} crash {r.crashPoint:0.00}x ({r.distraction})");

                rounds[r.id] = r;
                balance -= wager;
                Save();
                active = r;
                Refresh(r);

                done(new StartRoundResult
                {
                    ok = true,
                    roundId = r.id,
                    status = r.status,
                    balance = balance,
                    distraction = r.status == RoundStatus.Crashed ? r.distraction : null,
                    commitment = r.proof.commitment,
                });
            }));
        }

        public void GetState(string roundId, Action<RoundStateResult> done)
        {
            if (!rounds.TryGetValue(roundId ?? "", out var r)) { Respond(done, new RoundStateResult { ok = false, error = "Round not found for session" }, simulatedLatency); return; }
            Refresh(r);
            var res = new RoundStateResult { ok = true, status = r.status };
            switch (r.status)
            {
                case RoundStatus.Running:
                    res.multiplier = CrashMath.Floor2(CrashMath.MultiplierAt(Mathf.Max(0f, Now - r.startTime), growthRate));
                    break;
                case RoundStatus.Crashed:
                    res.multiplier = Mathf.Round(r.crashPoint * 100f) / 100f;
                    res.distraction = r.distraction;
                    break;
                default:
                    res.multiplier = r.cashOutMultiplier;
                    res.payout = r.payout;
                    break;
            }
            Respond(done, res, simulatedLatency);
        }

        public void CashOut(string roundId, Action<CashOutResult> done)
        {
            if (!rounds.TryGetValue(roundId ?? "", out var r)) { Respond(done, new CashOutResult { ok = false, error = "Round not found for session" }, simulatedLatency); return; }
            Refresh(r);
            if (r.status == RoundStatus.Running && Now < r.startTime)
            {
                Respond(done, new CashOutResult { ok = false, error = "Kitty is still warming up" }, simulatedLatency);
                return;
            }
            if (r.status == RoundStatus.Running)
            {
                float m = CrashMath.Floor2(CrashMath.MultiplierAt(Now - r.startTime, growthRate));
                r.payout = CrashMath.Payout(r.wager, m);
                r.cashOutMultiplier = m;
                r.status = RoundStatus.CashedOut;
                balance += r.payout;
                Save();
                if (active == r) active = null;
            }
            Respond(done, new CashOutResult
            {
                ok = true,
                status = r.status,
                multiplier = r.cashOutMultiplier,
                payout = r.status == RoundStatus.CashedOut ? r.payout : 0m,
                balance = balance,
            }, simulatedLatency);
        }

        public void GetResult(string roundId, Action<RoundOutcome> done)
        {
            if (!rounds.TryGetValue(roundId ?? "", out var r)) { Respond(done, new RoundOutcome { ok = false, error = "Round not found for session" }, simulatedLatency); return; }
            Refresh(r);
            Respond(done, new RoundOutcome
            {
                ok = true,
                status = r.status,
                crashPoint = r.status == RoundStatus.Running ? 0f : r.crashPoint,
                distraction = r.status == RoundStatus.Running ? null : r.distraction,
                payout = r.payout,
                proof = r.status == RoundStatus.Running ? null : r.proof,
            }, simulatedLatency);
        }

        public void GetBalance(Action<BalanceResult> done)
            => Respond(done, new BalanceResult { ok = true, balance = balance }, simulatedLatency);

        // ── helpers ─────────────────────────────

        string Validate(decimal wager)
        {
            if (wager != Round2(wager)) return "Amount must use at most 2 decimal places";
            if (active != null) Refresh(active);
            if (active != null) return "Session already has an active round";
            if (wager < MinBet || wager > MaxBet) return "Wager outside betting limits";
            if (wager > balance) return "Insufficient points";
            return null;
        }

        void Refresh(Round r)
        {
            if (r.status != RoundStatus.Running) return;
            if (Now - r.startTime >= r.crashTime)
            {
                r.status = RoundStatus.Crashed;
                if (active == r) active = null;
            }
        }

        void Save() => PlayerPrefs.SetString(BalanceKey, BackendUtil.FormatMoney(balance));

        static decimal Round2(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);

        void Respond<T>(Action<T> done, T result, float delay)
        {
            if (done == null) return;
            if (delay <= 0f || !isActiveAndEnabled) done(result);
            else StartCoroutine(After(delay, () => done(result)));
        }

        static IEnumerator After(float seconds, Action a)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            a();
        }
    }
}
