using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// All game audio, driven by RoundManager events and the season blend. Every slot is optional —
    /// drop clips in as you find them (freesound.org, kenney.nl, zapsplat…). Nothing breaks if empty.
    ///  • Footsteps loop: speeds up with the multiplier
    ///  • Tension loop: gets louder/higher as the multiplier climbs
    ///  • One-shots: bet placed, cash-out, crash sting, meow, one per distraction
    ///  • Ambience: one loop per season, crossfaded exactly with the season blend
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        [Header("Loops")]
        public AudioClip footstepsLoop;
        public AudioClip tensionLoop;

        [Header("One-shots")]
        public AudioClip betPlaced;
        public AudioClip cashOut;
        public AudioClip crashSting;
        public AudioClip meow;
        [Tooltip("Fish, Sleep, Yarn, Butterfly, Laser, CardboardBox — in that order.")]
        public AudioClip[] distractionClips = new AudioClip[6];

        [Header("Season ambience (Spring, Summer, Autumn, Winter)")]
        public AudioClip springAmbience;
        public AudioClip summerAmbience;
        public AudioClip autumnAmbience;
        public AudioClip winterAmbience;

        [Header("Mix")]
        [Range(0f, 1f)] public float masterVolume = 0.8f;
        [Range(0f, 1f)] public float sfxVolume = 1f;
        [Range(0f, 1f)] public float loopVolume = 0.6f;
        [Range(0f, 1f)] public float ambienceVolume = 0.4f;
        [Tooltip("Multiplier at which the tension loop is at full intensity.")]
        public float fullTensionAt = 10f;

        AudioSource steps, tension, sfx;
        readonly AudioSource[] ambience = new AudioSource[4];

        AudioSource MakeSource(string n, bool loop)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        void Awake()
        {
            steps = MakeSource("Footsteps", true);
            tension = MakeSource("Tension", true);
            sfx = MakeSource("SFX", false);
            sfx.volume = 1f;
            for (int i = 0; i < 4; i++) ambience[i] = MakeSource("Ambience_" + (Season)i, true);
        }

        void Start()
        {
            var rm = RoundManager.Instance;
            if (rm != null)
            {
                rm.OnPhaseChanged += HandlePhase;
                rm.OnCashOut += HandleCashOut;
                rm.OnCrash += HandleCrash;
            }
            Assign(steps, footstepsLoop);
            Assign(tension, tensionLoop);
            Assign(ambience[0], springAmbience);
            Assign(ambience[1], summerAmbience);
            Assign(ambience[2], autumnAmbience);
            Assign(ambience[3], winterAmbience);
        }

        void OnDestroy()
        {
            var rm = RoundManager.Instance;
            if (rm == null) return;
            rm.OnPhaseChanged -= HandlePhase;
            rm.OnCashOut -= HandleCashOut;
            rm.OnCrash -= HandleCrash;
        }

        static void Assign(AudioSource s, AudioClip c)
        {
            if (c == null) return;
            s.clip = c;
            s.Play();
        }

        void Update()
        {
            var rm = RoundManager.Instance;
            bool running = rm != null && rm.Phase == RoundPhase.Running;
            float k = running ? Mathf.InverseLerp(0f, Mathf.Log(Mathf.Max(1.01f, fullTensionAt)), Mathf.Log(Mathf.Max(1f, rm.Multiplier))) : 0f;
            float dt = Time.deltaTime;

            steps.volume = Mathf.MoveTowards(steps.volume, running ? loopVolume * masterVolume : 0f, dt * 2f);
            steps.pitch = 0.85f + 0.5f * k;
            tension.volume = Mathf.MoveTowards(tension.volume, running ? (0.15f + 0.85f * k) * loopVolume * masterVolume : 0f, dt);
            tension.pitch = 0.9f + 0.4f * k;

            var ss = SeasonSystem.Instance;
            for (int i = 0; i < 4; i++)
            {
                float w = ss != null ? ss.Weight((Season)i) : (i == 0 ? 1f : 0f);
                ambience[i].volume = w * ambienceVolume * masterVolume;
            }
        }

        void Play(AudioClip c, float vol = 1f)
        {
            if (c != null) sfx.PlayOneShot(c, vol * sfxVolume * masterVolume);
        }

        void HandlePhase(RoundPhase p)
        {
            if (p == RoundPhase.Starting) Play(betPlaced);
        }

        void HandleCashOut(float m, decimal payout) => Play(cashOut);

        void HandleCrash(float m, string distraction)
        {
            Play(crashSting);
            Play(meow, 0.9f);
            int i = (int)Distractions.FromServer(distraction);
            if (distractionClips != null && i < distractionClips.Length) Play(distractionClips[i], 0.9f);
        }
    }
}
