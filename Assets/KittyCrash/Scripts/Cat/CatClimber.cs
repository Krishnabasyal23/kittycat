using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// "It's a cat": when the distraction is TreeClimb, a tree appears just ahead, kitty trots to it, scrambles up the
    /// trunk (body tilted, legs running) and sits on the branch until the next round. Put it on the Cat (root);
    /// CatWalker hands control over while <see cref="Busy"/> is true.
    /// </summary>
    public class CatClimber : MonoBehaviour
    {
        public Sprite treeAutumn, treeGreen;
        [Tooltip("Height of the branch above the ground, in world units (matches the climb-tree art).")]
        public float branchHeight = 1.45f;
        public float climbSeconds = 1.1f;
        public int treeSortingOrder = 6;

        public bool Busy { get; private set; }

        CatWalker walker;
        CatSpriteAnimator anim;
        Transform visual;
        GameObject tree;
        float baseY, t, treeX;
        enum Step { None, Approach, Climb, Hop, Sit }
        Step step;

        void Awake()
        {
            walker = GetComponent<CatWalker>();
            baseY = transform.position.y;
        }

        void Start()
        {
            visual = walker != null ? walker.visual : null;
            anim = visual != null ? visual.GetComponent<CatSpriteAnimator>() : null;
            var rm = RoundManager.Instance;
            if (rm != null)
            {
                rm.OnRoundStarted += ResetClimb;
                rm.OnPhaseChanged += p => { if (p == RoundPhase.Idle) ResetClimb(); };
            }
        }

        public void StartClimb()
        {
            ResetClimb();
            var ss = SeasonSystem.Instance;
            Sprite art = ss != null && ss.DominantSeason == Season.Autumn ? treeAutumn : (treeGreen != null ? treeGreen : treeAutumn);
            if (art == null) return;
            tree = new GameObject("ClimbTree");
            var sr = tree.AddComponent<SpriteRenderer>();
            sr.sprite = art;
            sr.sortingOrder = treeSortingOrder;
            float size = CatSize.K;                     // the tree grows and shrinks with kitty
            tree.transform.localScale = Vector3.one * size;
            treeX = transform.position.x + 1.6f * size;
            // whatever the pivot, put the bottom of the tree on the ground
            tree.transform.position = new Vector3(treeX, baseY - art.bounds.min.y * size, 0f);
            Busy = true;
            step = Step.Approach;
            t = 0f;
        }

        void ResetClimb()
        {
            if (tree != null) Destroy(tree);
            tree = null;
            Busy = false;
            step = Step.None;
            var p = transform.position;
            transform.position = new Vector3(p.x, baseY, p.z);
            if (visual != null) visual.localRotation = Quaternion.identity;
        }

        void Update()
        {
            if (!Busy) return;
            t += Time.deltaTime;
            var p = transform.position;
            float size = CatSize.K;
            float trunkX = treeX - 0.25f * size;
            switch (step)
            {
                case Step.Approach:
                    p.x = Mathf.MoveTowards(p.x, trunkX, Time.deltaTime * 2.5f);
                    if (anim != null) anim.Play(CatSpriteAnimator.Clip.Walk, 1.2f);
                    if (Mathf.Abs(p.x - trunkX) < 0.01f) { step = Step.Climb; t = 0f; }
                    break;
                case Step.Climb:
                {
                    float k = Mathf.Clamp01(t / climbSeconds);
                    p.y = baseY + branchHeight * size * Mathf.SmoothStep(0f, 1f, k);
                    if (visual != null) visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 72f, Mathf.Clamp01(t / 0.2f)));
                    if (anim != null) anim.Play(CatSpriteAnimator.Clip.Run, 1.4f);
                    if (k >= 1f) { step = Step.Hop; t = 0f; }
                    break;
                }
                case Step.Hop:   // swing onto the branch
                {
                    float k = Mathf.Clamp01(t / 0.3f);
                    p.x = Mathf.Lerp(trunkX, treeX + 0.45f * size, k);
                    p.y = baseY + branchHeight * size + Mathf.Sin(k * Mathf.PI) * 0.25f * size;
                    if (visual != null) visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(72f, 0f, k));
                    if (k >= 1f) { step = Step.Sit; t = 0f; }
                    break;
                }
                case Step.Sit:
                    if (anim != null) anim.Play(CatSpriteAnimator.Clip.Sit);
                    break;
            }
            transform.position = p;
        }
    }
}
