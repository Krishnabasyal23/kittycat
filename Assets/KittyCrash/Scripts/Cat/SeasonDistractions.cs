using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// Each season has its own distractions (what you SEE when kitty stops — the money side is unchanged):
    ///   Spring: butterflies, birds, yarn          Summer: fish (mostly), laser dots
    ///   Autumn: falling leaves, butterflies, and sometimes kitty just climbs a tree (it's a cat)
    ///   Winter: sleepy snow naps, snowflakes, yarn
    /// The server's pick ("fish" / "sleep" / "yarn") is used as the seed, so every screen showing the same round
    /// shows the same thing.
    /// </summary>
    public static class SeasonDistractions
    {
        static readonly DistractionType[] Spring = { DistractionType.Butterfly, DistractionType.Bird, DistractionType.Yarn, DistractionType.Butterfly };
        static readonly DistractionType[] Summer = { DistractionType.Fish, DistractionType.Fish, DistractionType.Laser, DistractionType.Fish };
        static readonly DistractionType[] Autumn = { DistractionType.Leaf, DistractionType.Butterfly, DistractionType.TreeClimb, DistractionType.Leaf };
        static readonly DistractionType[] Winter = { DistractionType.Sleep, DistractionType.Snowflake, DistractionType.Sleep, DistractionType.Yarn };

        /// <summary>Things that can appear as fake-out temptations on the path in this season.</summary>
        public static DistractionType[] TemptationsFor(Season s)
        {
            switch (s)
            {
                case Season.Spring: return new[] { DistractionType.Butterfly, DistractionType.Bird, DistractionType.Yarn };
                case Season.Summer: return new[] { DistractionType.Fish, DistractionType.Laser, DistractionType.CardboardBox };
                case Season.Autumn: return new[] { DistractionType.Leaf, DistractionType.Butterfly, DistractionType.CardboardBox };
                default: return new[] { DistractionType.Snowflake, DistractionType.Yarn, DistractionType.CardboardBox };
            }
        }

        public static DistractionType[] PoolFor(Season s)
        {
            switch (s)
            {
                case Season.Spring: return Spring;
                case Season.Summer: return Summer;
                case Season.Autumn: return Autumn;
                default: return Winter;
            }
        }

        public static bool Enabled = true;

        /// <summary>Server distraction id → what we show this season (deterministic for a given round).</summary>
        public static DistractionType Resolve(string serverId)
        {
            var ss = SeasonSystem.Instance;
            if (!Enabled || ss == null) return Distractions.FromServer(serverId);
            var rm = RoundManager.Instance;
            string seed = (serverId ?? "") + "|" + (rm != null ? (rm.RoundId ?? ("spectator" + rm.History.Count)) : "");
            var pool = PoolFor(ss.DominantSeason);
            return pool[(int)(Hash(seed) % (uint)pool.Length)];
        }

        static uint Hash(string s)
        {
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return h;
        }
    }
}
