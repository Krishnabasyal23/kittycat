using System;

namespace KittyCrash
{
    public enum RoundStatus { Running, Crashed, CashedOut }

    public class BackendResult
    {
        public bool ok;
        public string error;
        /// <summary>True when the server says the session no longer exists (e.g. server restarted).</summary>
        public bool sessionLost;
    }

    public class BalanceResult : BackendResult { public decimal balance; }

    public class StartRoundResult : BackendResult
    {
        public string roundId;
        public RoundStatus status;
        public decimal balance;
        public string distraction;   // only set if the round crashed instantly (1.00x)
    }

    public class RoundStateResult : BackendResult
    {
        public RoundStatus status;
        public float multiplier;
        public string distraction;
        public decimal payout;
    }

    public class CashOutResult : BackendResult
    {
        public RoundStatus status;   // CashedOut = success, Crashed = too late
        public float multiplier;
        public decimal payout;
        public decimal balance;
    }

    public class RoundOutcome : BackendResult
    {
        public RoundStatus status;
        public float crashPoint;
        public string distraction;
        public decimal payout;
    }

    /// <summary>
    /// Everything the game needs from a backend. Matches the Quantum Cat Crash FastAPI:
    ///   POST /api/sessions                 → Connect
    ///   POST /api/rounds {wager}           → StartRound   (placing the bet starts the walk)
    ///   GET  /api/rounds/{id}/state        → GetState     (polled while walking)
    ///   POST /api/rounds/{id}/cashout      → CashOut
    ///   GET  /api/rounds/{id}/result       → GetResult    (crash point + distraction once over)
    ///   GET  /api/player/balance           → GetBalance
    /// </summary>
    public interface ICrashBackend
    {
        string Name { get; }
        float GrowthRate { get; }
        /// <summary>Seconds the cat walks at 1.00x before the multiplier starts (no crash, no cash-out).</summary>
        float WarmupSeconds { get; }
        decimal MinBet { get; }
        decimal MaxBet { get; }

        void Connect(Action<BalanceResult> done);
        void StartRound(decimal wager, Action<StartRoundResult> done);
        void GetState(string roundId, Action<RoundStateResult> done);
        void CashOut(string roundId, Action<CashOutResult> done);
        void GetResult(string roundId, Action<RoundOutcome> done);
        void GetBalance(Action<BalanceResult> done);
    }

    public static class BackendUtil
    {
        public static RoundStatus ParseStatus(string s)
        {
            switch (s)
            {
                case "crashed": return RoundStatus.Crashed;
                case "cashed_out": return RoundStatus.CashedOut;
                default: return RoundStatus.Running;
            }
        }

        public static decimal ParseMoney(string s)
        {
            return decimal.TryParse(s, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;
        }

        public static string FormatMoney(decimal d)
            => d.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    }
}
