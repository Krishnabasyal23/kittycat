using System.Collections.Generic;
using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// Falling bits per season: petals (spring), pollen (summer), leaves (autumn), snow (winter).
    /// Proper crossfade: each flake belongs to ONE season and keeps its look until it leaves the
    /// screen. During a transition, new flakes are picked from the next season more and more often,
    /// so petals gradually give way to snow — nothing ever changes colour mid-air.
    /// Optional per-season sprite: SeasonSystem › Themes › Weather Sprite.
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        public Sprite defaultSprite;
        public int maxFlakes = 100;
        public float swayAmount = 0.6f;
        public int sortingOrder = 20;

        class Flake
        {
            public Transform t;
            public SpriteRenderer sr;
            public float phase, speedJitter, sizeJitter, spinDir;
            public Season season;
            public bool active;
        }

        readonly List<Flake> flakes = new List<Flake>();
        Camera cam;
        bool initialFill = true;

        void Start()
        {
            cam = Camera.main;
            for (int i = 0; i < maxFlakes; i++)
            {
                var go = new GameObject("Flake");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = defaultSprite;
                sr.sortingOrder = sortingOrder;
                go.SetActive(false);
                flakes.Add(new Flake
                {
                    t = go.transform, sr = sr,
                    phase = Random.value * 10f,
                    speedJitter = Random.Range(0.7f, 1.3f),
                    sizeJitter = Random.Range(0.7f, 1.3f),
                    spinDir = Random.value < 0.5f ? -1f : 1f,
                });
            }
        }

        void LateUpdate()
        {
            var ss = SeasonSystem.Instance;
            if (ss == null || cam == null) return;

            var a = ss.Get(ss.CurrentSeason);
            var b = ss.Get(ss.NextSeason);
            float wa = (1f - ss.Blend) * a.weatherDensity;
            float wb = ss.Blend * b.weatherDensity;
            int target = Mathf.RoundToInt(Mathf.Clamp01(wa + wb) * maxFlakes);
            float pNext = (wa + wb) > 0.0001f ? wb / (wa + wb) : 0f;

            Vector3 bl = cam.ViewportToWorldPoint(Vector3.zero);
            Vector3 tr = cam.ViewportToWorldPoint(Vector3.one);
            float dt = Time.deltaTime;

            for (int i = 0; i < flakes.Count; i++)
            {
                var f = flakes[i];
                bool wanted = i < target;

                if (!f.active)
                {
                    if (!wanted) continue;
                    f.active = true;
                    f.t.gameObject.SetActive(true);
                    Respawn(f, ss, pNext, bl, tr, initialFill);
                }

                var theme = ss.Get(f.season);
                var p = f.t.position;
                p.y -= theme.weatherFallSpeed * f.speedJitter * dt;
                p.x += Mathf.Sin(Time.time * 1.3f + f.phase) * swayAmount * dt;
                f.t.position = p;
                f.t.Rotate(0f, 0f, theme.weatherSpin * f.spinDir * dt);
                f.t.localScale = Vector3.one * theme.weatherSize * f.sizeJitter;

                bool offscreen = p.y < bl.y - 0.5f || p.x < bl.x - 1f;
                if (offscreen)
                {
                    if (wanted) Respawn(f, ss, pNext, bl, tr, false);
                    else { f.active = false; f.t.gameObject.SetActive(false); } // retire only once off-screen
                }
            }
            initialFill = false;
        }

        void Respawn(Flake f, SeasonSystem ss, float pNext, Vector3 bl, Vector3 tr, bool anywhere)
        {
            f.season = Random.value < pNext ? ss.NextSeason : ss.CurrentSeason;
            var theme = ss.Get(f.season);
            f.sr.sprite = theme.weatherSprite != null ? theme.weatherSprite : defaultSprite;
            f.sr.color = theme.weatherColor;

            float x = Random.Range(bl.x, tr.x + 3f);
            float y = anywhere ? Random.Range(bl.y, tr.y) : tr.y + Random.Range(0.2f, 1.5f);
            f.t.position = new Vector3(x, y, 0f);
        }
    }
}
