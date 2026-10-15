using System;
using UnityEngine;

namespace KittyCrash
{
    public enum Season { Spring = 0, Summer = 1, Autumn = 2, Winter = 3 }

    /// <summary>
    /// Season clock + smooth blending.
    /// • Every launch starts on a random season, and never the same one as the previous launch.
    /// • Each season lasts `secondsPerSeason`. During its last `transitionPortion`, everything —
    ///   sky, ground, leaves, plant sprites, brightness, weather, festive decor — eases into the next
    ///   season with a smootherstep curve (no visible "pop" at the start or the end).
    /// Other scripts read SeasonSystem.Instance (Current snapshot, Weight(season), Blend).
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class SeasonSystem : MonoBehaviour
    {
        [Serializable]
        public class SeasonTheme
        {
            public Season season;
            [Tooltip("Optional festival shown in the HUD while this season dominates (e.g. Holi, Teej, Tihar, Christmas).")]
            public string festiveName;

            public Color sky = Color.cyan;
            public Color ground = Color.green;
            public Color leaves = Color.green;
            public Color trunk = new Color(0.45f, 0.3f, 0.2f);
            [Range(0f, 1f)] public float brightness = 1f;

            [Header("Weather (petals / pollen / leaves / snow)")]
            public Color weatherColor = Color.white;
            [Range(0f, 1f)] public float weatherDensity = 0f;
            public float weatherFallSpeed = 1f;
            public float weatherSize = 0.12f;
            public float weatherSpin = 60f;
            public Sprite weatherSprite;
        }

        public struct Snapshot
        {
            public Color sky, ground, leaves, trunk;
            public float brightness;
        }

        public static SeasonSystem Instance { get; private set; }

        [Header("Clock")]
        public float secondsPerSeason = 90f;
        [Range(0.1f, 0.9f)]
        [Tooltip("Fraction at the END of each season spent blending into the next one.")]
        public float transitionPortion = 0.4f;
        [Tooltip("Random start season, never repeating the previous launch's.")]
        public bool randomStartSeason = true;
        public Season startSeason = Season.Spring;
        [Tooltip("Also start at a random point inside that season (before its transition).")]
        public bool randomStartOffset = true;

        [Header("Targets")]
        [Tooltip("Black sprite in front of the scene; alpha = 1 - brightness.")]
        public SpriteRenderer brightnessOverlay;

        public SeasonTheme[] themes =
        {
            new SeasonTheme { season = Season.Spring,
                sky = new Color(0.68f, 0.87f, 0.97f), ground = new Color(0.52f, 0.78f, 0.40f),
                leaves = new Color(0.98f, 0.74f, 0.84f), trunk = new Color(0.46f, 0.33f, 0.23f), brightness = 1f,
                weatherColor = new Color(1f, 0.78f, 0.86f), weatherDensity = 0.3f, weatherFallSpeed = 0.55f, weatherSize = 0.1f, weatherSpin = 90f },
            new SeasonTheme { season = Season.Summer,
                sky = new Color(0.47f, 0.77f, 1f), ground = new Color(0.40f, 0.72f, 0.26f),
                leaves = new Color(0.24f, 0.62f, 0.24f), trunk = new Color(0.42f, 0.30f, 0.20f), brightness = 1f,
                weatherColor = new Color(1f, 0.96f, 0.62f), weatherDensity = 0.08f, weatherFallSpeed = 0.2f, weatherSize = 0.06f, weatherSpin = 0f },
            new SeasonTheme { season = Season.Autumn,
                sky = new Color(0.93f, 0.79f, 0.60f), ground = new Color(0.66f, 0.56f, 0.30f),
                leaves = new Color(0.90f, 0.46f, 0.14f), trunk = new Color(0.38f, 0.26f, 0.18f), brightness = 0.86f,
                weatherColor = new Color(0.92f, 0.48f, 0.15f), weatherDensity = 0.45f, weatherFallSpeed = 0.85f, weatherSize = 0.14f, weatherSpin = 140f },
            new SeasonTheme { season = Season.Winter,
                sky = new Color(0.79f, 0.85f, 0.93f), ground = new Color(0.93f, 0.95f, 0.98f),
                leaves = new Color(0.89f, 0.93f, 0.97f), trunk = new Color(0.33f, 0.27f, 0.25f), brightness = 0.74f,
                weatherColor = Color.white, weatherDensity = 0.9f, weatherFallSpeed = 1.1f, weatherSize = 0.1f, weatherSpin = 20f },
        };

        public Season CurrentSeason { get; private set; }
        public Season NextSeason => (Season)(((int)CurrentSeason + 1) % 4);
        /// <summary>0 = fully current season, 1 = fully next season.</summary>
        public float Blend { get; private set; }
        public bool InTransition => Blend > 0.001f;
        public Season DominantSeason => Blend >= 0.5f ? NextSeason : CurrentSeason;
        /// <summary>0..1 progress through the current season.</summary>
        public float SeasonProgress { get; private set; }
        public Snapshot Current { get; private set; }

        public event Action<Season> OnSeasonChanged;

        const string LastStartKey = "KittyCrash_LastStartSeason";
        float progress; // in seasons, 0..4
        Camera cam;
        Season lastDominant;

        void Awake()
        {
            Instance = this;
            cam = Camera.main;

            int start = (int)startSeason;
            if (randomStartSeason)
            {
                int last = PlayerPrefs.GetInt(LastStartKey, -1);
                do { start = UnityEngine.Random.Range(0, 4); } while (start == last);
                PlayerPrefs.SetInt(LastStartKey, start);
                PlayerPrefs.Save();
            }

            progress = start + (randomStartOffset ? UnityEngine.Random.Range(0f, (1f - transitionPortion) * 0.7f) : 0f);
            Sample();
            lastDominant = DominantSeason;
            Apply();
        }

        void Update()
        {
            progress += Time.deltaTime / Mathf.Max(1f, secondsPerSeason);
            if (progress >= 4f) progress -= 4f;

            if (InputReader.DebugNextSeason()) SkipToNextTransition();

            Sample();
            Apply();

            if (DominantSeason != lastDominant)
            {
                lastDominant = DominantSeason;
                OnSeasonChanged?.Invoke(lastDominant);
            }
        }

        /// <summary>How visible a season is right now (0..1). Current = 1 - Blend, Next = Blend, others 0.</summary>
        public float Weight(Season s)
        {
            if (s == CurrentSeason) return 1f - Blend;
            if (s == NextSeason) return Blend;
            return 0f;
        }

        /// <summary>Jump to the start of the next transition (dev / demo).</summary>
        public void SkipToNextTransition()
        {
            float whole = Mathf.Floor(progress);
            float transitionStart = whole + 1f - transitionPortion;
            progress = progress < transitionStart - 0.001f ? transitionStart : transitionStart + 1f;
            if (progress >= 4f) progress -= 4f;
        }

        public SeasonTheme Get(Season s)
        {
            foreach (var t in themes) if (t != null && t.season == s) return t;
            return themes[0];
        }

        static float Smootherstep(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * x * (x * (x * 6f - 15f) + 10f);
        }

        void Sample()
        {
            int i = Mathf.FloorToInt(progress) % 4;
            float t = progress - Mathf.Floor(progress);
            float start = 1f - transitionPortion;
            SeasonProgress = t;
            Blend = t <= start ? 0f : Smootherstep((t - start) / transitionPortion);
            CurrentSeason = (Season)i;

            var a = Get(CurrentSeason);
            var b = Get(NextSeason);
            float k = Blend;
            Current = new Snapshot
            {
                sky = Color.Lerp(a.sky, b.sky, k),
                ground = Color.Lerp(a.ground, b.ground, k),
                leaves = Color.Lerp(a.leaves, b.leaves, k),
                trunk = Color.Lerp(a.trunk, b.trunk, k),
                brightness = Mathf.Lerp(a.brightness, b.brightness, k),
            };
        }

        void Apply()
        {
            if (cam != null) cam.backgroundColor = Current.sky;
            if (brightnessOverlay != null) brightnessOverlay.color = new Color(0f, 0f, 0f, 1f - Current.brightness);
        }
    }
}
