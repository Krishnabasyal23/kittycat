using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KittyCrash
{
    /// <summary>
    /// All the game's sound in one place. Drop audio files into Resources with these names and it just works
    /// (any format Unity imports: .ogg .mp3 .wav; missing files are simply skipped):
    ///
    ///   Assets/KittyCrash/Resources/KittyAudio/Music/   spring  summer  fall  winter   (+ optional: lobby)
    ///   Assets/KittyCrash/Resources/KittyAudio/Sfx/     reel_tick reel_land bet go cashout bonus crash join
    ///                                                   butterfly leafstorm cocoa zoomies
    ///                                                   d_fish d_sleep d_yarn d_butterfly d_laser d_box
    ///                                                   d_leaf d_snowflake d_bird d_treeclimb
    ///   Assets/KittyCrash/Resources/KittyAudio/Cat/     meow  meow2               (optional real cat voice)
    ///
    /// Music: one track per season, crossfading with the season the world shows. While the season reel spins
    /// the music dips and every season it passes goes "tick"; it lands with a chime.
    /// M = mute/unmute music.  Put it on the Managers object.
    /// </summary>
    public class KittyAudio : MonoBehaviour
    {
        [Range(0f, 1f)] public float master = 0.9f;
        [Range(0f, 1f)] public float music = 0.45f;
        [Range(0f, 1f)] public float sfx = 0.8f;
        [Tooltip("Seconds for a full crossfade between season tracks.")]
        public float crossfadeSeconds = 1.6f;

        static readonly string[] SeasonNames = { "spring", "summer", "fall", "winter" };   // order = Season enum

        readonly AudioSource[] seasonTracks = new AudioSource[4];
        AudioSource lobby, oneShots;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        RoundManager rm;
        bool musicMuted, wasWarming, wasSpinning;
        Season lastSeen;
        float duck = 1f, duckTarget = 1f, duckHold, pitch = 1f;

        void Awake()
        {
            for (int i = 0; i < 4; i++) seasonTracks[i] = Track("Music_" + SeasonNames[i], Load("Music/" + SeasonNames[i]));
            lobby = Track("Music_lobby", Load("Music/lobby"));
            oneShots = gameObject.AddComponent<AudioSource>();
            oneShots.playOnAwake = false;
            oneShots.spatialBlend = 0f;
        }

        void Start()
        {
            rm = RoundManager.Instance;
            if (rm != null)
            {
                rm.OnPhaseChanged += Phase;
                rm.OnCashOut += CashOut;
                rm.OnCrash += Crash;
            }
            SeasonRules.OnEvent += SeasonEvent;
            CrowdMode.OnPlayersJoined += Joined;
            if (SeasonSystem.Instance != null) lastSeen = SeasonSystem.Instance.DominantSeason;
        }

        void OnDestroy()
        {
            if (rm != null)
            {
                rm.OnPhaseChanged -= Phase;
                rm.OnCashOut -= CashOut;
                rm.OnCrash -= Crash;
            }
            SeasonRules.OnEvent -= SeasonEvent;
            CrowdMode.OnPlayersJoined -= Joined;
        }

        // ── loading ────────────────────────────────

        AudioClip Load(string path)
        {
            if (clips.TryGetValue(path, out var c)) return c;
            c = Resources.Load<AudioClip>("KittyAudio/" + path);
            clips[path] = c;
            return c;
        }

        AudioSource Track(string n, AudioClip clip)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            if (clip != null) s.Play();
            return s;
        }

        public void Sfx(string name, float volume = 1f, float pitchJitter = 0f)
        {
            var c = Load("Sfx/" + name);
            if (c == null) return;
            oneShots.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            oneShots.PlayOneShot(c, volume * sfx * master);
        }

        void Cat(float volume = 1f)
        {
            var c = Load(Random.value < 0.5f ? "Cat/meow" : "Cat/meow2") ?? Load("Cat/meow");
            if (c != null) oneShots.PlayOneShot(c, volume * sfx * master);
        }

        void Duck(float to, float holdSeconds) { duckTarget = to; duckHold = holdSeconds; }

        // ── game events ────────────────────────────

        void Phase(RoundPhase p)
        {
            if (p == RoundPhase.Starting) Sfx("bet", 0.9f, 0.05f);
            if (p == RoundPhase.Idle) pitch = 1f;
        }

        void CashOut(float multiplier, decimal payout) => Sfx("cashout", 1f);

        void Crash(float multiplier, string distraction)
        {
            Sfx("crash", 0.75f);
            var type = SeasonDistractions.Resolve(distraction);   // the same pick the cat acts out
            Sfx(DistractionSfx(type), DistractionVolume(type));
            Cat(0.8f);
            Duck(0.35f, 2.5f);
            pitch = 1f;
        }

        void SeasonEvent(string e)
        {
            switch (e)
            {
                case "butterfly": Sfx("butterfly", 0.9f); break;
                case "leafstorm": Sfx("leafstorm", 0.8f); break;
                case "cocoa": Sfx("cocoa", 0.9f); break;
                case "zoomies": Sfx("zoomies", 0.9f); pitch = 1.07f; break;   // the music speeds up a little too
                case "bonus": Sfx("bonus", 1f); break;
            }
        }

        void Joined(int count) => Sfx("join", 0.8f, 0.1f);

        static string DistractionSfx(DistractionType t)
        {
            switch (t)
            {
                case DistractionType.Fish: return "d_fish";
                case DistractionType.Sleep: return "d_sleep";
                case DistractionType.Yarn: return "d_yarn";
                case DistractionType.Butterfly: return "d_butterfly";
                case DistractionType.Laser: return "d_laser";
                case DistractionType.Leaf: return "d_leaf";
                case DistractionType.Snowflake: return "d_snowflake";
                case DistractionType.Bird: return "d_bird";
                case DistractionType.TreeClimb: return "d_treeclimb";
                default: return "d_box";
            }
        }

        static float DistractionVolume(DistractionType t) => t == DistractionType.Sleep ? 0.6f : t == DistractionType.Leaf ? 1f : 0.85f;

        // ── every frame ────────────────────────────

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (!InputReader.Typing && Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) musicMuted = !musicMuted;
#else
            if (!InputReader.Typing && Input.GetKeyDown(KeyCode.M)) musicMuted = !musicMuted;
#endif
            var ss = SeasonSystem.Instance;

            // season reel: tick for every season it passes, chime when it lands
            bool spinning = ss != null && ss.IsSpinning;
            if (ss != null && ss.DominantSeason != lastSeen)
            {
                lastSeen = ss.DominantSeason;
                if (spinning) Sfx("reel_tick", 0.7f, 0.04f);
            }
            if (wasSpinning && !spinning) Sfx("reel_land", 0.9f);
            wasSpinning = spinning;

            // "go!" when the warm-up ends and kitty starts walking for real
            bool warming = rm != null && rm.IsWarmingUp;
            if (wasWarming && !warming && rm.Phase == RoundPhase.Running) Sfx("go", 0.7f);
            wasWarming = warming;

            // ducking (crash / reel spin)
            if (duckHold > 0f) duckHold -= Time.deltaTime;
            else duckTarget = 1f;
            float want = spinning ? Mathf.Min(duckTarget, 0.4f) : duckTarget;
            duck = Mathf.MoveTowards(duck, want, Time.deltaTime * 1.5f);

            // season crossfade (follows what the world shows, so it matches the server's season)
            float fade = Time.deltaTime / Mathf.Max(0.1f, crossfadeSeconds);
            bool anySeason = false;
            for (int i = 0; i < 4; i++)
            {
                var src = seasonTracks[i];
                if (src.clip == null) continue;
                anySeason = true;
                float w = ss != null ? ss.Weight((Season)i) : (i == 0 ? 1f : 0f);
                float target = musicMuted ? 0f : w * music * master * duck;
                src.volume = Mathf.MoveTowards(src.volume, target, fade);
                src.pitch = Mathf.MoveTowards(src.pitch, pitch, Time.deltaTime * 0.5f);
            }
            if (lobby.clip != null)
            {
                bool useLobby = !anySeason || rm == null || rm.Phase == RoundPhase.Connecting || rm.Phase == RoundPhase.Offline;
                lobby.volume = Mathf.MoveTowards(lobby.volume, !musicMuted && useLobby ? music * master * duck : 0f, fade);
            }
        }
    }
}
