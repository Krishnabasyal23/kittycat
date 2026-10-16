using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// Makes any sprite (ground, tree, bush, flower…) follow the seasons smoothly. Two ways, use either or both:
    ///  1. Seasonal sprites: drag a Spring/Summer/Autumn/Winter version of the art into the slots.
    ///     During a transition the next season's sprite CROSSFADES in on top — no popping.
    ///     Empty slots fall back to the original sprite.
    ///  2. Tint: multiplies the sprite by the season colour for its role (good for light/greyscale art
    ///     and the placeholder squares). Set Tint Role = None to keep the art's own colours.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SeasonalSprite : MonoBehaviour
    {
        public enum TintRole { None, Ground, Leaves, Trunk, Distant }

        public Sprite spring, summer, autumn, winter;
        public TintRole tintRole = TintRole.None;
        [Tooltip("Distant role only: 0 = same as sky (far haze), 1 = same as ground (close).")]
        [Range(0f, 1f)] public float distantMix = 0.5f;
        [Tooltip("Random light/dark variation so repeated plants don't look identical.")]
        public float shadeVariation = 0.1f;
        [Tooltip("Overall brightness (far-away depth rows use < 1).")]
        public float brightness = 1f;

        SpriteRenderer main, fade;
        Sprite original;
        float shade = 1f;

        void Awake()
        {
            main = GetComponent<SpriteRenderer>();
            original = main.sprite;
            shade = 1f + Random.Range(-shadeVariation, shadeVariation);

            var go = new GameObject("SeasonCrossfade");
            go.transform.SetParent(transform, false);
            fade = go.AddComponent<SpriteRenderer>();
            fade.sharedMaterial = main.sharedMaterial;
            fade.sortingLayerID = main.sortingLayerID;
            fade.sortingOrder = main.sortingOrder + 1;  // just above the original (leave gaps of 2 between layers)
            fade.drawMode = main.drawMode;
            fade.size = main.size;
            fade.enabled = false;
        }

        Sprite For(Season s)
        {
            Sprite sp = null;
            switch (s)
            {
                case Season.Spring: sp = spring; break;
                case Season.Summer: sp = summer; break;
                case Season.Autumn: sp = autumn; break;
                case Season.Winter: sp = winter; break;
            }
            return sp != null ? sp : original;
        }

        Color TintFor(SeasonSystem.Snapshot s)
        {
            Color c;
            switch (tintRole)
            {
                case TintRole.Ground: c = s.ground; break;
                case TintRole.Leaves: c = s.leaves; break;
                case TintRole.Trunk: c = s.trunk; break;
                case TintRole.Distant: c = Color.Lerp(s.sky, s.ground, distantMix) * 0.92f; break;
                default: c = Color.white; break;
            }
            float k = shade * brightness;
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        void LateUpdate()
        {
            var ss = SeasonSystem.Instance;
            if (ss == null) return;

            Color tint = TintFor(ss.Current);
            Sprite a = For(ss.CurrentSeason);
            Sprite b = For(ss.NextSeason);

            main.sprite = a;
            main.color = tint;

            bool crossfade = ss.InTransition && b != null && b != a;
            fade.enabled = crossfade;
            if (crossfade)
            {
                fade.sprite = b;
                fade.flipX = main.flipX;
                fade.size = main.size;
                fade.color = new Color(tint.r, tint.g, tint.b, ss.Blend);
            }
        }
    }
}
