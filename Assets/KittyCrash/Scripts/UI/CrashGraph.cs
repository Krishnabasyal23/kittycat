using UnityEngine;
using UnityEngine.UI;

namespace KittyCrash
{
    /// <summary>
    /// The classic crash-game chart: a rising curve with a glowing filled area, a dot at the head,
    /// faint grid lines, and a green marker where you cashed out. Drawn as a UI mesh, no textures needed.
    /// CrashUI feeds it every frame via SetState().
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class CrashGraph : MaskableGraphic
    {
        public Color lineColor = new Color(1f, 0.66f, 0.18f);
        public Color fillColor = new Color(1f, 0.66f, 0.18f, 0.28f);
        public Color gridColor = new Color(1f, 1f, 1f, 0.07f);
        public Color cashColor = new Color(0.3f, 0.9f, 0.5f);
        public float thickness = 5f;

        public float MaxMult { get; private set; } = 2f;
        public float MaxTime { get; private set; } = 10f;

        float growth = 0.06f, elapsed, cashMult = -1f;

        public void SetState(float growthRate, float elapsedSeconds, float cashMultiplier, Color line)
        {
            growth = Mathf.Max(0.001f, growthRate);
            elapsed = Mathf.Max(0f, elapsedSeconds);
            cashMult = cashMultiplier;
            lineColor = line;
            fillColor = new Color(line.r, line.g, line.b, 0.28f);
            float mNow = Mathf.Exp(growth * elapsed);
            MaxMult = Mathf.Max(2f, mNow * 1.2f);
            MaxTime = Mathf.Max(10f, elapsed * 1.15f);
            SetVerticesDirty();
        }

        Vector2 P(Rect r, float t)
        {
            float m = Mathf.Exp(growth * t);
            return new Vector2(r.xMin + r.width * t / MaxTime, r.yMin + r.height * (m - 1f) / (MaxMult - 1f));
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();

            for (int i = 1; i <= 3; i++)
            {
                float y = r.yMin + r.height * i / 3f;
                Quad(vh, new Vector2(r.xMin, y - 0.75f), new Vector2(r.xMin, y + 0.75f), new Vector2(r.xMax, y + 0.75f), new Vector2(r.xMax, y - 0.75f), gridColor, gridColor);
            }

            if (elapsed <= 0.0001f)
            {
                Dot(vh, new Vector2(r.xMin, r.yMin), thickness * 1.6f, lineColor);
                return;
            }

            const int N = 64;
            var transparent = new Color(fillColor.r, fillColor.g, fillColor.b, 0f);
            Vector2 prev = P(r, 0f);
            for (int i = 1; i <= N; i++)
            {
                Vector2 p = P(r, elapsed * i / N);
                // filled area (fades towards the bottom)
                Quad(vh, new Vector2(prev.x, r.yMin), prev, p, new Vector2(p.x, r.yMin), transparent, fillColor);
                // line
                Vector2 dir = (p - prev).normalized;
                Vector2 n = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);
                Quad(vh, prev - n, prev + n, p + n, p - n, lineColor, lineColor);
                prev = p;
            }

            if (cashMult > 1f)
            {
                float tc = Mathf.Log(cashMult) / growth;
                if (tc <= elapsed + 0.001f) Dot(vh, P(r, tc), thickness * 1.8f, cashColor);
            }
            Dot(vh, prev, thickness * 2.2f, Color.white);
            Dot(vh, prev, thickness * 1.5f, lineColor);
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

        static void Dot(VertexHelper vh, Vector2 c, float size, Color col)
        {
            float h = size * 0.5f;
            // octagon-ish dot from two quads
            Quad(vh, c + new Vector2(-h, -h * 0.5f), c + new Vector2(-h, h * 0.5f), c + new Vector2(h, h * 0.5f), c + new Vector2(h, -h * 0.5f), col, col);
            Quad(vh, c + new Vector2(-h * 0.5f, -h), c + new Vector2(-h * 0.5f, h), c + new Vector2(h * 0.5f, h), c + new Vector2(h * 0.5f, -h), col, col);
        }
    }
}
