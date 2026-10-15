using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// Mirrors crash_engine.py so the client animates exactly like the server counts.
    ///   multiplier_at(t)  = e^(GROWTH_RATE * t)        GROWTH_RATE = 0.18
    ///   crash_from_uniform = clamp((1 - HOUSE_EDGE) / u, 1, MAX)   HOUSE_EDGE = 0.04, MAX = 100
    /// The real crash point always comes from the server; these are for display + offline mode.
    /// </summary>
    public static class CrashMath
    {
        public const float DefaultGrowthRate = 0.18f;
        public const float DefaultHouseEdge = 0.04f;
        public const float DefaultMaxMultiplier = 100f;

        public static float MultiplierAt(float seconds, float growthRate)
            => Mathf.Exp(growthRate * Mathf.Max(0f, seconds));

        public static float SecondsFor(float multiplier, float growthRate)
            => Mathf.Log(Mathf.Max(1f, multiplier)) / growthRate;

        /// <summary>Floor to 2 decimals, like math.floor(m * 100) / 100 on the server.</summary>
        public static float Floor2(float v) => Mathf.Floor(v * 100f + 1e-4f) / 100f;

        public static float CrashFromUniform(double u, double houseEdge, double maxMultiplier)
        {
            double crash = (1.0 - houseEdge) / u;
            return (float)System.Math.Max(1.0, System.Math.Min(crash, maxMultiplier));
        }

        /// <summary>wager * multiplier, rounded half-up to cents (economy.calculate_cashout).</summary>
        public static decimal Payout(decimal wager, float multiplier)
        {
            decimal m = decimal.Parse(multiplier.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                                      System.Globalization.CultureInfo.InvariantCulture);
            return System.Math.Round(wager * m, 2, System.MidpointRounding.AwayFromZero);
        }
    }
}
