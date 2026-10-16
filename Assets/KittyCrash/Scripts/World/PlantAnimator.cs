using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// Plays a plant's animation frames (swaying flowers, rustling bushes) and keeps the season swap working:
    /// each season can have its own frame list; empty lists fall back to Frames.
    /// Every copy starts on a random frame so a row of flowers doesn't move in lock-step.
    /// Works together with SeasonalSprite (it feeds the current frame into its season slots).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlantAnimator : MonoBehaviour
    {
        public Sprite[] frames;
        public Sprite[] springFrames, summerFrames, autumnFrames, winterFrames;
        public float fps = 6f;
        [Tooltip("Random speed difference between copies.")]
        [Range(0f, 0.5f)] public float speedVariation = 0.2f;

        SpriteRenderer sr;
        SeasonalSprite seasonal;
        float t, speed;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            seasonal = GetComponent<SeasonalSprite>();
            speed = fps * (1f + Random.Range(-speedVariation, speedVariation));
            t = Random.Range(0f, 32f);
        }

        static Sprite Pick(Sprite[] list, Sprite[] fallback, int i)
        {
            var l = list != null && list.Length > 0 ? list : fallback;
            if (l == null || l.Length == 0) return null;
            return l[i % l.Length];
        }

        void Update()
        {
            t += Time.deltaTime * speed;
            int i = Mathf.FloorToInt(t);

            if (seasonal != null)
            {
                seasonal.spring = Pick(springFrames, frames, i);
                seasonal.summer = Pick(summerFrames, frames, i);
                seasonal.autumn = Pick(autumnFrames, frames, i);
                seasonal.winter = Pick(winterFrames, frames, i);
                if (SeasonSystem.Instance == null && frames != null && frames.Length > 0) sr.sprite = Pick(frames, null, i);
            }
            else if (frames != null && frames.Length > 0) sr.sprite = Pick(frames, null, i);
        }
    }
}
