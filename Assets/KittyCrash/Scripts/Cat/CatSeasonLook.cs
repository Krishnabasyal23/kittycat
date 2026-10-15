using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// Festival look for the cat: per-season fur tint (e.g. black cat for Halloween) that blends
    /// with the season transition. Costumes (hats, masks…) are children of Cat › Visual inside
    /// "Costume_[Season]" groups with a SeasonalGroup component, so they fade in/out on their own —
    /// move/scale them in the Scene view to fit your cat's head.
    /// Tip: instead of tinting, you can also swap to a different cat colour from the pack for a season
    /// by giving that season its own CatSpriteAnimator frames (team stretch goal).
    /// </summary>
    public class CatSeasonLook : MonoBehaviour
    {
        public SpriteRenderer catRenderer;
        public Color springFur = Color.white;
        public Color summerFur = Color.white;
        [Tooltip("Halloween: dark tint turns the cat black.")]
        public Color autumnFur = new Color(0.22f, 0.2f, 0.27f);
        public Color winterFur = Color.white;

        Color baseColor = Color.white;

        void Awake()
        {
            if (catRenderer == null)
            {
                var walker = GetComponent<CatWalker>();
                if (walker != null && walker.visual != null) catRenderer = walker.visual.GetComponent<SpriteRenderer>();
            }
            if (catRenderer != null) baseColor = catRenderer.color; // keeps the placeholder's orange; white for real art
        }

        Color Fur(Season s)
        {
            switch (s)
            {
                case Season.Spring: return springFur;
                case Season.Summer: return summerFur;
                case Season.Autumn: return autumnFur;
                default: return winterFur;
            }
        }

        void LateUpdate()
        {
            var ss = SeasonSystem.Instance;
            if (ss == null || catRenderer == null) return;
            var c = baseColor * Color.Lerp(Fur(ss.CurrentSeason), Fur(ss.NextSeason), ss.Blend);
            c.a = catRenderer.color.a;
            catRenderer.color = c;
        }
    }
}
