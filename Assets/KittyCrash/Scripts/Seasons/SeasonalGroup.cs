using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// Fades a whole group of decorations in/out with its season — use it for festive stuff
    /// (Holi colour bursts in spring, Tihar lights in autumn, snowmen/lights in winter…).
    /// Put decorations as children; they fade together with the season's weight.
    /// Parent the group under the camera for screen decor (bunting, lanterns), or in the world for props.
    /// </summary>
    public class SeasonalGroup : MonoBehaviour
    {
        public Season season;
        [Tooltip("Show only when this season is at least this visible (0 = fade fully with the season).")]
        [Range(0f, 1f)] public float minWeight = 0f;

        SpriteRenderer[] renderers;
        float[] baseAlpha;

        void Awake() => Collect();

        /// <summary>Call after adding/removing children at runtime.</summary>
        public void Collect()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            baseAlpha = new float[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) baseAlpha[i] = renderers[i].color.a;
        }

        void LateUpdate()
        {
            var ss = SeasonSystem.Instance;
            if (ss == null || renderers == null) return;

            float w = ss.Weight(season);
            w = minWeight > 0f ? Mathf.InverseLerp(minWeight, 1f, w) : w;

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                r.enabled = w > 0.001f;
                var c = r.color;
                c.a = baseAlpha[i] * w;
                r.color = c;
            }
        }
    }
}
