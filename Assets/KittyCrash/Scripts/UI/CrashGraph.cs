using UnityEngine;
using UnityEngine.UI;

namespace KittyCrash
{
    /// <summary>
    /// "Multiplier over time": the payout multiplier grows the same way every round (m = e^(g·t)),
    /// the only random thing is WHERE it stops (when kitty gets distracted). The chart shows:
    ///   • the curve so far (glowing line + soft fill)        • a dashed line at your auto cash-out target
    ///   • a green dot where you cashed out                    • a red marker where kitty stopped
    /// Drawn as a UI mesh (no textures), so it's sharp at any resolution. CrashUI feeds it every frame.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class CrashGraph : MaskableGraphic
    {
        public Color lineColor = new Color(1f, 0.66f, 0.18f);
        public Color fillColor = new Color(1f, 0.66f, 0.18f, 0.28f);
        public Color gridColor = new Color(1f, 1f, 1f, 0.07f);
        public Color cashColor = new Color(0.3f, 0.9f, 0.5f);
        public Color targetColor = new Color(0.45f, 0.95f, 0.6f, 0.75f);
        public Color crashColor = new Color(1f, 0.35f, 0.35f);
        public float thickness = 5f;

        public float MaxMult { get; private set; } = 2f;
        public float MaxTime { get; private set; } = 10f;

        float growth = 0.06f, elapsed, cashMult = -1f, target = -1f;
        bool crashed;

        public void SetState(float growthRate, float elapsedSeconds, float cashMultiplier, Color line)
            => SetState(growthRate, elapsedSeconds, cashMultiplier, line, -1f, false);

        public void SetState(float growthRate, float elapsedSeconds, float cashMultiplier, Color line, float targetMult, bool hasCrashed)
        {
            growth = Mathf.Max(0.001f, growthRate);
            elapsed = Mathf.Max(0f, elapsedSeconds);
            cashMult = cashMultiplier;
            target = targetMult;
            crashed = hasCrashed;
            lineColor = line;
            fillColor = new Color(line.r, line.g, line.b, 0.28f);
            float mNow = Mathf.Exp(growth * elapsed);
            MaxMult = Mathf.Max(2f, mNow * 1.2f);
            if (target > 1f && target < 50f) MaxMult = Mathf.Max(MaxMult, target * 1.08f);
            MaxTime = Mathf.Max(10f, elapsed * 1.15f);
            SetVerticesDirty();
        }

        /// <summary>Local position (inside this rect) of the multiplier value on the y axis.</summary>
        public float LocalY(float mult)
        {
            var r = rectTransform.rect;
            return r.yMin + r.height * Mathf.Clamp01((mult - 1f) / (MaxMult - 1f));
        }

        public float LocalX(float seconds)
        {
            var r = rectTransform.rect;
            return r.xMin + r.width * Mathf.Clamp01(seconds / MaxTime);
        }

        public float TimeFor(float mult) => mult > 1f ? Mathf.Log(mult) / growth : 0f;

        Vector2 P(Rect r, float t)
        {
            float m = Mathf.Exp(growth * t);
            return new Vector2(r.xMin + r.width * t / MaxTime, r.yMin + r.height * (m - 1f) / (MaxMult - 1f));
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();

            // grid
            for (int i = 1; i <= 3; i++)
            {
                float y = r.yMin + r.height * i / 3f;
                Quad(vh, new Vector2(r.xMin, y - 0.75f), new Vector2(r.xMin, y + 0.75f), new Vector2(r.xMax, y + 0.75f), new Vector2(r.xMax, y - 0.75f), gridColor, gridColor);
            }
            // axes
            var axis = new Color(1f, 1f, 1f, 0.18f);
            Quad(vh, new Vector2(r.xMin, r.yMin - 1f), new Vector2(r.xMin, r.yMin + 1f), new Vector2(r.xMax, r.yMin + 1f), new Vector2(r.xMax, r.yMin - 1f), axis, axis);

            // dashed target line (auto cash-out)
            if (target > 1f && target <= MaxMult)
            {
                float y = r.yMin + r.height * (target - 1f) / (MaxMult - 1f);
                for (float x = r.xMin; x < r.xMax; x += 16f)
                {
                    float x2 = Mathf.Min(x + 9f, r.xMax);
                    Quad(vh, new Vector2(x, y - 1.2f), new Vector2(x, y + 1.2f), new Vector2(x2, y + 1.2f), new Vector2(x2, y - 1.2f), targetColor, targetColor);
                }
            }

            if (elapsed <= 0.0001f)
            {
                Circle(vh, new Vector2(r.xMin, r.yMin), thickness * 1.1f, lineColor);
                return;
            }

            const int N = 96;
            var transparent = new Color(fillColor.r, fillColor.g, fillColor.b, 0f);
            var glow = new Color(lineColor.r, lineColor.g, lineColor.b, 0.22f);
            Vector2 prev = P(r, 0f);
            Vector2 prevN = Vector2.zero;
            for (int i = 1; i <= N; i++)
            {
                Vector2 p = P(r, elapsed * i / N);
                Quad(vh, new Vector2(prev.x, r.yMin), prev, p, new Vector2(p.x, r.yMin), transparent, fillColor);
                Vector2 dir = (p - prev).normalized;
                Vector2 n = new Vector2(-dir.y, dir.x);
                Vector2 nPrev = i == 1 ? n : (prevN + n).normalized; // mitred joints = smooth line
                // soft glow + core line
                Quad(vh, prev - nPrev * thickness * 1.3f, prev + nPrev * thickness * 1.3f, p + n * thickness * 1.3f, p - n * thickness * 1.3f, glow, glow);
                Quad(vh, prev - nPrev * thickness * 0.5f, prev + nPrev * thickness * 0.5f, p + n * thickness * 0.5f, p - n * thickness * 0.5f, lineColor, lineColor);
                prev = p;
                prevN = n;
            }

            if (cashMult > 1f)
            {
                float tc = TimeFor(cashMult);
                if (tc <= elapsed + 0.001f)
                {
                    var c = P(r, tc);
                    Circle(vh, c, thickness * 1.9f, Color.white);
                    Circle(vh, c, thickness * 1.4f, cashColor);
                }
            }

            if (crashed)
            {
                // red "X" where kitty stopped
                float s = thickness * 1.8f;
                Line(vh, prev + new Vector2(-s, -s), prev + new Vector2(s, s), thickness * 0.9f, crashColor);
                Line(vh, prev + new Vector2(-s, s), prev + new Vector2(s, -s), thickness * 0.9f, crashColor);
            }
            else
            {
                Circle(vh, prev, thickness * 1.6f, Color.white);
                Circle(vh, prev, thickness * 1.1f, lineColor);
            }
        }

        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color bottom, Color top)
        {
            int i = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.color = bottom; v.position = a; vh.AddVert(v);
            v.color = top; v.position = b; vh.AddVert(v);
            v.color = top; v.position = c; vh.AddVert(v);
            v.color = bottom; v.position = d; vh.AddVert(v);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        static void Line(VertexHelper vh, Vector2 a, Vector2 b, float w, Color c)
        {
            Vector2 n = new Vector2(-(b - a).normalized.y, (b - a).normalized.x) * (w * 0.5f);
            Quad(vh, a - n, a + n, b + n, b - n, c, c);
        }

        static void Circle(VertexHelper vh, Vector2 c, float radius, Color col)
        {
            const int Seg = 20;
            int start = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.color = col;
            v.position = c;
            vh.AddVert(v);
            for (int i = 0; i <= Seg; i++)
            {
                float a = i * Mathf.PI * 2f / Seg;
                v.position = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                vh.AddVert(v);
            }
            for (int i = 1; i <= Seg; i++) vh.AddTriangle(start, start + i, start + i + 1);
        }
    }
}
