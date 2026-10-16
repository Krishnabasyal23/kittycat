using System;
using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// Soft vertical sky gradient + sun with a glow, both following the camera and blending per season
    /// (pink-horizon spring, deep-blue summer, golden-hour autumn, pale winter).
    /// </summary>
    public class SkyGradient : MonoBehaviour
    {
        [Serializable]
        public class SkyColors
        {
            public Color top, horizon, sun;
            public SkyColors(Color t, Color h, Color s) { top = t; horizon = h; sun = s; }
        }

        public SkyColors spring = new SkyColors(new Color(0.45f, 0.72f, 0.98f), new Color(0.99f, 0.86f, 0.92f), new Color(1f, 0.96f, 0.86f));
        public SkyColors summer = new SkyColors(new Color(0.22f, 0.55f, 0.98f), new Color(0.74f, 0.91f, 1f), new Color(1f, 0.94f, 0.62f));
        public SkyColors autumn = new SkyColors(new Color(0.40f, 0.52f, 0.84f), new Color(1f, 0.77f, 0.50f), new Color(1f, 0.74f, 0.44f));
        public SkyColors winter = new SkyColors(new Color(0.52f, 0.63f, 0.80f), new Color(0.93f, 0.95f, 0.99f), new Color(1f, 1f, 0.95f));

        public Sprite sunSprite;
        [Tooltip("Where the sun sits on screen (0..1).")]
        public Vector2 sunViewport = new Vector2(0.8f, 0.8f);
        public float sunSize = 2.2f;

        Camera cam;
        Texture2D tex;
        SpriteRenderer sky, sun, halo;
        readonly Color32[] px = new Color32[64];

        void Start()
        {
            cam = Camera.main;
            if (cam == null) { enabled = false; return; }

            tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var skySprite = Sprite.Create(tex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);

            sky = MakeChild("Sky", skySprite, -100);
            if (sunSprite != null)
            {
                halo = MakeChild("SunHalo", FxPool.Ellipse, -99);
                sun = MakeChild("Sun", sunSprite, -98);
            }
        }

        SpriteRenderer MakeChild(string name, Sprite s, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 60f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            return sr;
        }

        SkyColors Get(Season s)
        {
            switch (s)
            {
                case Season.Spring: return spring;
                case Season.Summer: return summer;
                case Season.Autumn: return autumn;
                default: return winter;
            }
        }

        void LateUpdate()
        {
            if (sky == null) return;
            var ss = SeasonSystem.Instance;
            SkyColors a = ss != null ? Get(ss.CurrentSeason) : summer;
            SkyColors b = ss != null ? Get(ss.NextSeason) : summer;
            float k = ss != null ? ss.Blend : 0f;

            Color top = Color.Lerp(a.top, b.top, k), hor = Color.Lerp(a.horizon, b.horizon, k);
            for (int i = 0; i < 64; i++)
            {
                float t = i / 63f;                       // 0 = bottom
                float e = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 1f, t));
                px[i] = Color.Lerp(hor, top, e);
            }
            tex.SetPixels32(px);
            tex.Apply(false);

            float h = cam.orthographicSize * 2f * 1.15f;
            float w = h * cam.aspect * 1.1f;
            sky.transform.localScale = new Vector3(w, h / 64f, 1f);

            if (sun != null)
            {
                Color sc = Color.Lerp(a.sun, b.sun, k);
                float size = cam.orthographicSize;
                var pos = new Vector3((sunViewport.x - 0.5f) * 2f * size * cam.aspect, (sunViewport.y - 0.5f) * 2f * size, 60f);
                sun.transform.localPosition = pos;
                halo.transform.localPosition = pos + new Vector3(0, 0, 0.1f);
                float sunScale = sunSize / Mathf.Max(0.01f, sunSprite.bounds.size.x);
                sun.transform.localScale = Vector3.one * sunScale;
                halo.transform.localScale = Vector3.one * sunSize * 3.2f;
                sun.color = sc;
                halo.color = new Color(sc.r, sc.g, sc.b, 0.38f + 0.05f * Mathf.Sin(Time.time * 0.8f));
            }
        }
    }
}
