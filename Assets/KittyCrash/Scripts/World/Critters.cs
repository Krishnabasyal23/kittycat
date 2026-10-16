using System.Collections.Generic;
using UnityEngine;

namespace KittyCrash
{
    public enum CritterKind { Butterfly, Bee, Firefly, Bird }

    /// <summary>
    /// Ambient life that follows the seasons: butterflies & bees (spring/summer), glowing fireflies
    /// (summer/autumn), little flocks of birds crossing the sky (all year, fewer in winter).
    /// Critters fade in/out with their season during transitions.
    /// </summary>
    public class CritterSpawner : MonoBehaviour
    {
        [Header("Sprites (2 frames each)")]
        public Sprite[] butterflyFrames = new Sprite[0];   // pairs: colourA_0, colourA_1, colourB_0, ...
        public Sprite[] beeFrames = new Sprite[0];
        public Sprite[] birdFrames = new Sprite[0];
        public Sprite firefly;

        [Header("Amount")]
        public int maxCritters = 12;
        public float spawnEvery = 0.9f;
        public float groundY = -2.5f;

        readonly List<Critter> live = new List<Critter>();
        Camera cam;
        float timer;

        void Start()
        {
            cam = Camera.main;
            // fill the screen a little at start
            for (int i = 0; i < maxCritters / 2; i++) Spawn(true);
        }

        void Update()
        {
            live.RemoveAll(c => c == null);
            timer -= Time.deltaTime;
            if (timer > 0f || cam == null) return;
            timer = spawnEvery * Random.Range(0.6f, 1.4f);
            if (live.Count < Target()) Spawn(false);
        }

        int Target()
        {
            var ss = SeasonSystem.Instance;
            if (ss == null) return maxCritters;
            float winter = ss.Weight(Season.Winter);
            return Mathf.RoundToInt(Mathf.Lerp(maxCritters, maxCritters * 0.3f, winter));
        }

        void Spawn(bool anywhere)
        {
            var ss = SeasonSystem.Instance;
            Season season = ss == null ? Season.Summer : (Random.value < ss.Blend ? ss.NextSeason : ss.CurrentSeason);
            CritterKind kind = Pick(season);

            Vector3 bl = cam.ViewportToWorldPoint(Vector3.zero), tr = cam.ViewportToWorldPoint(Vector3.one);
            float x = anywhere ? Random.Range(bl.x, tr.x) : tr.x + Random.Range(0.5f, 2.5f);

            if (kind == CritterKind.Bird)
            {
                int flock = Random.Range(2, 6);
                float y = Random.Range(groundY + 3.5f, tr.y - 1f);
                for (int i = 0; i < flock; i++)
                    Make(kind, season, new Vector3(x + i * Random.Range(0.4f, 0.8f), y + Random.Range(-0.4f, 0.4f) - i * 0.15f, 0f));
                return;
            }

            float yy = kind == CritterKind.Firefly ? Random.Range(groundY + 0.3f, groundY + 3f) : Random.Range(groundY + 0.6f, groundY + 3.2f);
            Make(kind, season, new Vector3(x, yy, 0f));
        }

        static CritterKind Pick(Season s)
        {
            float r = Random.value;
            switch (s)
            {
                case Season.Spring: return r < 0.6f ? CritterKind.Butterfly : r < 0.85f ? CritterKind.Bee : CritterKind.Bird;
                case Season.Summer: return r < 0.35f ? CritterKind.Butterfly : r < 0.6f ? CritterKind.Bee : r < 0.85f ? CritterKind.Firefly : CritterKind.Bird;
                case Season.Autumn: return r < 0.25f ? CritterKind.Butterfly : r < 0.55f ? CritterKind.Firefly : CritterKind.Bird;
                default: return CritterKind.Bird;
            }
        }

        void Make(CritterKind kind, Season season, Vector3 pos)
        {
            Sprite[] frames;
            switch (kind)
            {
                case CritterKind.Butterfly:
                    if (butterflyFrames.Length < 2) return;
                    int pair = Random.Range(0, butterflyFrames.Length / 2) * 2;
                    frames = new[] { butterflyFrames[pair], butterflyFrames[pair + 1] };
                    break;
                case CritterKind.Bee: frames = beeFrames; break;
                case CritterKind.Bird: frames = birdFrames; break;
                default: frames = firefly != null ? new[] { firefly } : null; break;
            }
            if (frames == null || frames.Length == 0 || frames[0] == null) return;

            var go = new GameObject(kind.ToString());
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var c = go.AddComponent<Critter>();
            c.Init(kind, season, frames, cam);
            live.Add(c);
        }
    }

    public class Critter : MonoBehaviour
    {
        CritterKind kind;
        Season season;
        Sprite[] frames;
        SpriteRenderer sr;
        Camera cam;
        Vector3 basePos;
        float t, phase, speed, fps, fadeIn;

        public void Init(CritterKind k, Season s, Sprite[] f, Camera c)
        {
            kind = k; season = s; frames = f; cam = c;
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            basePos = transform.position;
            phase = Random.value * 10f;
            switch (kind)
            {
                case CritterKind.Butterfly: speed = Random.Range(-0.4f, 0.5f); fps = 7f; sr.sortingOrder = Random.value < 0.5f ? 14 : -7; break;
                case CritterKind.Bee: speed = Random.Range(-0.8f, 0.9f); fps = 16f; sr.sortingOrder = 14; break;
                case CritterKind.Firefly: speed = Random.Range(-0.2f, 0.2f); fps = 0f; sr.sortingOrder = 15; break;
                default: speed = Random.Range(-3.2f, -2f); fps = 5f; sr.sortingOrder = -28; break;
            }
            // butterflies, bees and fireflies match kitty's size; birds stay as they are
            float scale = kind == CritterKind.Bird ? 0.9f : (kind == CritterKind.Firefly ? 0.7f : 1.1f) * CatSize.K;
            transform.localScale = Vector3.one * scale;
        }

        void Update()
        {
            t += Time.deltaTime;
            fadeIn = Mathf.MoveTowards(fadeIn, 1f, Time.deltaTime * 1.5f);
            if (fps > 0f && frames.Length > 1) sr.sprite = frames[(int)(t * fps + phase) % frames.Length];

            basePos.x += speed * Time.deltaTime;
            Vector3 p = basePos;
            switch (kind)
            {
                case CritterKind.Butterfly:
                    p += new Vector3(Mathf.Sin(t * 0.9f + phase) * 0.8f, Mathf.Sin(t * 2.1f + phase) * 0.5f + Mathf.Sin(t * 6.3f) * 0.12f, 0f);
                    break;
                case CritterKind.Bee:
                    p += new Vector3(Mathf.Sin(t * 1.7f + phase) * 0.6f, Mathf.Sin(t * 4.1f + phase) * 0.25f, 0f);
                    break;
                case CritterKind.Firefly:
                    p += new Vector3(Mathf.Sin(t * 0.5f + phase) * 0.7f, Mathf.Sin(t * 0.8f + phase * 2f) * 0.4f, 0f);
                    break;
                default:
                    p.y += Mathf.Sin(t * 1.5f + phase) * 0.15f;
                    break;
            }
            if (kind == CritterKind.Bee) sr.flipX = p.x < transform.position.x; // bee faces where it flies
            transform.position = p;

            float w = SeasonSystem.Instance != null ? SeasonSystem.Instance.Weight(season) : 1f;
            float a = fadeIn * Mathf.Clamp01(w * 1.5f);
            if (kind == CritterKind.Firefly) a *= 0.55f + 0.45f * Mathf.Sin(t * 3f + phase);
            var c = sr.color; c.a = a; sr.color = c;

            float left = cam != null ? cam.ViewportToWorldPoint(Vector3.zero).x : -999f;
            if (p.x < left - 3f || t > 60f || (w <= 0.001f && fadeIn >= 1f)) Destroy(gameObject);
        }
    }
}
