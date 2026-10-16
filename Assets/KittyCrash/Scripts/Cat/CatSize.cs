using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// How big kitty is compared to the size the props, effects and tree were designed for (Cat scale 2).
    ///   Cat scale 2 → K = 1      Cat scale 1 → K = 0.5
    /// Everything placed around the cat multiplies by K, so changing the Cat's scale in the Inspector is all
    /// you need to do. Ui is a gentler factor for text, so it stays readable.
    /// </summary>
    public static class CatSize
    {
        const float DesignedScale = 2f;
        static CatWalker cat;

        public static float K
        {
            get
            {
                if (cat == null)
                {
#if UNITY_2023_1_OR_NEWER
                    cat = Object.FindAnyObjectByType<CatWalker>();
#else
                    cat = Object.FindObjectOfType<CatWalker>();
#endif
                }
                return cat != null ? Mathf.Max(0.05f, Mathf.Abs(cat.transform.lossyScale.y) / DesignedScale) : 1f;
            }
        }

        /// <summary>For text and UI around the cat: shrinks less than the art (0.625 when K = 0.5).</summary>
        public static float Ui => 0.25f + 0.75f * K;
    }
}
