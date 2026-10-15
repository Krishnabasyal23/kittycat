using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// Kitty being a cat:
    ///   Idle / Starting → sits & breathes (stretches while the server rolls the round)
    ///   Running         → walks, speeding up as the multiplier rises, with random "cat stuff"
    ///                     fake-out pauses (pure suspense — they never end the round)
    ///   Ended           → gets distracted by whatever the server picked (fish / sleep / yarn …)
    /// Uses CatSpriteAnimator if frames are assigned; otherwise bobs the placeholder square.
    /// </summary>
    public class CatWalker : MonoBehaviour
    {
        [Header("Walking")]
        public float walkSpeed = 1.6f;
        public float maxSpeed = 5.5f;
        [Tooltip("Multiplier at which the cat hits max speed (server grows fast: 5x at ~9s).")]
        public float fullSpeedAtMultiplier = 6f;
        [Tooltip("Above this fraction of max speed, the Run clip plays instead of Walk.")]
        [Range(0f, 1f)] public float runThreshold = 0.6f;
        public float acceleration = 3f;
        public float stopDeceleration = 12f;

        [Header("Cat stuff (fake-out pauses)")]
        public Vector2 fidgetInterval = new Vector2(2f, 5f);
        [Range(0f, 1f)] public float fidgetSpeedFactor = 0.35f;
        public float fidgetDuration = 0.45f;

        [Header("Visuals")]
        public Transform visual;
        public CatSpriteAnimator spriteAnimator;
        [Tooltip("Square used for placeholder distraction props.")]
        public Sprite placeholderSprite;
        [Tooltip("Optional real art: Fish, Sleep (Z), Yarn, Butterfly, Laser, CardboardBox — in that order.")]
        public Sprite[] distractionSprites = new Sprite[6];

        public float CurrentSpeed { get; private set; }
        public float DistanceThisRound { get; private set; }
        public DistractionType LastDistraction { get; private set; }
        public bool IsFidgeting => fidgetLeft > 0f;

        float fidgetTimer, fidgetLeft;
        Vector3 visualBasePos, visualBaseScale;
        GameObject prop;
        bool distracted;

        void Awake()
        {
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            if (visual != null)
            {
                visualBasePos = visual.localPosition;
                visualBaseScale = visual.localScale;
                if (spriteAnimator == null) spriteAnimator = visual.GetComponent<CatSpriteAnimator>();
            }
        }

        void Start()
        {
            var rm = RoundManager.Instance;
            if (rm == null) return;
            rm.OnPhaseChanged += HandlePhase;
            rm.OnRoundStarted += HandleRoundStarted;
            rm.OnCrash += HandleCrash;
        }

        void OnDestroy()
        {
            var rm = RoundManager.Instance;
            if (rm == null) return;
            rm.OnPhaseChanged -= HandlePhase;
            rm.OnRoundStarted -= HandleRoundStarted;
            rm.OnCrash -= HandleCrash;
        }

        void Update()
        {
            var rm = RoundManager.Instance;
            bool running = rm != null && rm.Phase == RoundPhase.Running;

            float target = 0f;
            if (running)
            {
                float k = Mathf.InverseLerp(1f, fullSpeedAtMultiplier, rm.Multiplier);
                target = Mathf.Lerp(walkSpeed, maxSpeed, k);

                fidgetTimer -= Time.deltaTime;
                if (fidgetTimer <= 0f)
                {
                    fidgetLeft = fidgetDuration;
                    fidgetTimer = fidgetDuration + Random.Range(fidgetInterval.x, fidgetInterval.y);
                }
                if (fidgetLeft > 0f)
                {
                    fidgetLeft -= Time.deltaTime;
                    target *= fidgetSpeedFactor;
                }
            }

            float rate = target < CurrentSpeed ? (running ? acceleration * 2f : stopDeceleration) : acceleration;
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, target, rate * Time.deltaTime);

            float step = CurrentSpeed * Time.deltaTime;
            transform.position += Vector3.right * step;
            if (running) DistanceThisRound += step;

            UpdateVisual(rm, running);
        }

        void HandleRoundStarted()
        {
            DistanceThisRound = 0f;
            distracted = false;
            fidgetLeft = 0f;
            fidgetTimer = Random.Range(fidgetInterval.x, fidgetInterval.y);
            if (prop != null) Destroy(prop);
        }

        void HandlePhase(RoundPhase phase)
        {
            if (phase == RoundPhase.Idle || phase == RoundPhase.Starting)
            {
                distracted = false;
                if (prop != null) Destroy(prop);
            }
        }

        void HandleCrash(float crashPoint, string distractionId)
        {
            fidgetLeft = 0f;
            distracted = true;
            LastDistraction = Distractions.FromServer(distractionId);

            if (prop != null) Destroy(prop);
            prop = new GameObject("Distraction_" + LastDistraction);
            int i = (int)LastDistraction;
            Sprite art = distractionSprites != null && i < distractionSprites.Length ? distractionSprites[i] : null;
            prop.AddComponent<DistractionProp>().Init(LastDistraction, art != null ? art : placeholderSprite, art == null, transform);
        }

        void UpdateVisual(RoundManager rm, bool running)
        {
            bool animated = spriteAnimator != null && spriteAnimator.HasFrames;

            if (animated)
            {
                if (distracted) spriteAnimator.Play(Distractions.Pose(LastDistraction));
                else if (CurrentSpeed > 0.05f)
                {
                    bool fast = CurrentSpeed >= maxSpeed * runThreshold;
                    float speed = CurrentSpeed / (fast ? maxSpeed * runThreshold : walkSpeed);
                    spriteAnimator.Play(fast ? CatSpriteAnimator.Clip.Run : CatSpriteAnimator.Clip.Walk, Mathf.Clamp(speed, 0.6f, 1.8f));
                }
                else if (rm != null && rm.Phase == RoundPhase.Idle) spriteAnimator.Play(CatSpriteAnimator.Clip.Sit);
                else spriteAnimator.Play(CatSpriteAnimator.Clip.Idle);
                return;
            }

            // Placeholder square: bob while walking, breathe while idle, squash when distracted.
            if (visual == null) return;
            float t = Time.time;
            if (distracted)
            {
                visual.localPosition = visualBasePos;
                visual.localScale = new Vector3(visualBaseScale.x * 1.1f, visualBaseScale.y * 0.8f, 1f);
            }
            else if (CurrentSpeed > 0.05f)
            {
                float bob = Mathf.Abs(Mathf.Sin(t * (6f + CurrentSpeed * 2f))) * 0.1f * Mathf.Min(1f, CurrentSpeed);
                visual.localPosition = visualBasePos + Vector3.up * bob;
                visual.localScale = visualBaseScale;
            }
            else
            {
                float breathe = 1f + Mathf.Sin(t * (rm != null && rm.Phase == RoundPhase.Starting ? 8f : 2.2f)) * 0.03f;
                visual.localPosition = visualBasePos;
                visual.localScale = new Vector3(visualBaseScale.x, visualBaseScale.y * breathe, visualBaseScale.z);
            }
        }
    }
}
