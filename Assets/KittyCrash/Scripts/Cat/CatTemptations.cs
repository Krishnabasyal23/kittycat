using System.Collections.Generic;
using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// Fake-out distractions DURING the walk: every few seconds a fish / yarn ball / butterfly / laser dot / box shows up
    /// ahead of kitty. Kitty slows down, stares ("?")… and keeps walking. Pure suspense — "is THIS the one?" — the real
    /// distraction still only comes at the crash point. Uses the sprites from CatWalker › Distraction Sprites.
    /// Put it on the Cat (root).
    /// </summary>
    public class CatTemptations : MonoBehaviour
    {
        public Vector2 interval = new Vector2(3.5f, 7f);
        [Tooltip("How long kitty slows down and stares.")]
        public float stareSeconds = 0.9f;
        public float triggerDistance = 1.3f;
        public float propScale = 1f;
        public int sortingOrder = 6;

        public static float LastTemptTime = -99f;
        public static string LastTemptName = "";

        enum Kind { Fish = 0, Yarn = 2, Butterfly = 3, Laser = 4, Box = 5 }
        static readonly Kind[] Kinds = { Kind.Fish, Kind.Yarn, Kind.Butterfly, Kind.Laser, Kind.Box };

        class Prop { public Transform t; public SpriteRenderer sr; public Kind kind; public float age, phase, baseY; public bool triggered, leaving; }

        readonly List<Prop> props = new List<Prop>();
        CatWalker walker;
        CatCute cute;
        Camera cam;
        float timer;

        void Start()
        {
            walker = GetComponent<CatWalker>();
            cute = GetComponent<CatCute>();
            cam = Camera.main;
            timer = Random.Range(interval.x, interval.y) * 0.6f;
            var rm = RoundManager.Instance;
            if (rm != null) rm.OnRoundStarted += ClearAll;
        }

        void OnDestroy()
        {
            var rm = RoundManager.Instance;
            if (rm != null) rm.OnRoundStarted -= ClearAll;
        }

        void ClearAll()
        {
            foreach (var p in props) if (p.t != null) Destroy(p.t.gameObject);
            props.Clear();
            timer = Random.Range(interval.x, interval.y) * 0.6f;
        }

        void Update()
        {
            var rm = RoundManager.Instance;
            Tension.TemptBoost = Mathf.MoveTowards(Tension.TemptBoost, 0f, Time.deltaTime * 0.25f);
            bool running = rm != null && rm.Phase == RoundPhase.Running && !rm.IsWarmingUp;

            if (running)
            {
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    Spawn();
                    timer = Random.Range(interval.x, interval.y);
                }
            }

            float catX = transform.position.x;
            float left = cam != null ? cam.ViewportToWorldPoint(Vector3.zero).x - 3f : catX - 20f;
            for (int i = props.Count - 1; i >= 0; i--)
            {
                var p = props[i];
                if (p.t == null) { props.RemoveAt(i); continue; }
                p.age += Time.deltaTime;
                Animate(p, catX);

                if (!p.triggered && running && Mathf.Abs(p.t.position.x - catX) < triggerDistance)
                {
                    p.triggered = true;
                    if (walker != null) walker.Tempt(stareSeconds);
                    if (cute != null) cute.Emote(cute.question, stareSeconds + 0.3f);
                    Tension.TemptBoost = Mathf.Max(Tension.TemptBoost, 0.28f);
                    LastTemptTime = Time.time;
                    LastTemptName = Name(p.kind);
                }
                if (p.triggered && p.t.position.x < catX - 1.2f) p.leaving = true;

                if (p.t.position.x < left || (p.leaving && (p.kind == Kind.Butterfly || p.kind == Kind.Laser) && p.sr.color.a <= 0.01f))
                {
                    Destroy(p.t.gameObject);
                    props.RemoveAt(i);
                }
            }
        }

        void Spawn()
        {
            if (cam == null) cam = Camera.main;
            var kind = Kinds[Random.Range(0, Kinds.Length)];
            Sprite sp = null;
            if (walker != null && walker.distractionSprites != null && (int)kind < walker.distractionSprites.Length)
                sp = walker.distractionSprites[(int)kind];

            var go = new GameObject("Temptation_" + kind);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
            if (sp != null) sr.sprite = sp;
            else { sr.sprite = FxPool.Ellipse; sr.color = PlaceholderColor(kind); go.transform.localScale = Vector3.one * 0.5f; }

            float right = cam != null ? cam.ViewportToWorldPoint(new Vector3(1f, 0f, 0f)).x : transform.position.x + 8f;
            float groundY = transform.position.y;
            float h = sr.sprite != null ? sr.sprite.bounds.size.y * propScale : 0.5f;
            float y = groundY + h * 0.5f;
            if (kind == Kind.Butterfly) y = groundY + Random.Range(1.2f, 2f);
            if (kind == Kind.Laser) y = groundY + 0.08f;
            go.transform.position = new Vector3(right + 1f, y, 0f);
            if (sp != null) go.transform.localScale = Vector3.one * propScale;

            props.Add(new Prop { t = go.transform, sr = sr, kind = kind, phase = Random.value * 10f, baseY = y });
        }

        void Animate(Prop p, float catX)
        {
            float t = p.age + p.phase;
            var pos = p.t.position;
            switch (p.kind)
            {
                case Kind.Fish: // flop
                    pos.y = p.baseY + Mathf.Abs(Mathf.Sin(t * 5f)) * 0.25f;
                    p.t.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 10f) * 18f);
                    break;
                case Kind.Yarn: // rolls toward the cat
                    pos.x -= Time.deltaTime * 0.6f;
                    p.t.Rotate(0f, 0f, Time.deltaTime * 140f);
                    break;
                case Kind.Butterfly: // flutter, flies off after being noticed
                    pos.y = p.baseY + Mathf.Sin(t * 3f) * 0.35f + (p.leaving ? p.age * 0.02f : 0f);
                    pos.x += Mathf.Sin(t * 1.3f) * Time.deltaTime * 0.8f + (p.leaving ? Time.deltaTime * 2.5f : 0f);
                    p.t.localScale = new Vector3(Mathf.Abs(Mathf.Sin(t * 14f)) * 0.6f + 0.4f, 1f, 1f) * propScale;
                    if (p.leaving) Fade(p);
                    break;
                case Kind.Laser: // jittery dot that zips away
                    pos.x += (Mathf.PerlinNoise(t * 3f, 0f) - 0.5f) * Time.deltaTime * 6f + (p.leaving ? Time.deltaTime * 6f : 0f);
                    p.sr.color = new Color(1f, 1f, 1f, p.leaving ? Mathf.Max(0f, p.sr.color.a - Time.deltaTime * 2f) : 0.7f + 0.3f * Mathf.Sin(t * 20f));
                    break;
                case Kind.Box: // wobble
                    p.t.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 6f) * (p.triggered && !p.leaving ? 8f : 2f));
                    break;
            }
            p.t.position = pos;
        }

        static void Fade(Prop p)
        {
            var c = p.sr.color;
            c.a = Mathf.Max(0f, c.a - Time.deltaTime * 0.8f);
            p.sr.color = c;
        }

        static string Name(Kind k)
        {
            switch (k)
            {
                case Kind.Fish: return "a fish";
                case Kind.Yarn: return "a yarn ball";
                case Kind.Butterfly: return "a butterfly";
                case Kind.Laser: return "a laser dot";
                default: return "a box";
            }
        }

        static Color PlaceholderColor(Kind k)
        {
            switch (k)
            {
                case Kind.Fish: return new Color(0.45f, 0.65f, 0.85f);
                case Kind.Yarn: return new Color(0.9f, 0.3f, 0.4f);
                case Kind.Butterfly: return new Color(1f, 0.6f, 0.8f);
                case Kind.Laser: return Color.red;
                default: return new Color(0.75f, 0.55f, 0.35f);
            }
        }
    }
}
