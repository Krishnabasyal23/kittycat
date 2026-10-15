using System;
using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// Dead-simple frame animator for the Pet Cats pack — no Animator Controller needed.
    /// Slice the sheet, then select the frames for each action in the Project window and drag them
    /// all at once onto the matching "Frames" list here. Missing clips fall back sensibly
    /// (Run → Walk → Idle, Sleep → Sit → Idle, Pounce → Sit → Idle).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CatSpriteAnimator : MonoBehaviour
    {
        public enum Clip { Idle, Walk, Run, Sit, Sleep, Pounce }

        [Serializable]
        public class ClipFrames
        {
            public Sprite[] frames = new Sprite[0];
            public float fps = 10f;
            public bool loop = true;
            public bool IsValid => frames != null && frames.Length > 0;
        }

        public ClipFrames idle = new ClipFrames { fps = 6f };
        public ClipFrames walk = new ClipFrames { fps = 10f };
        public ClipFrames run = new ClipFrames { fps = 14f };
        public ClipFrames sit = new ClipFrames { fps = 6f };
        public ClipFrames sleep = new ClipFrames { fps = 4f };
        public ClipFrames pounce = new ClipFrames { fps = 12f, loop = false };

        [Tooltip("Tick if the cat sprites face LEFT (the cat walks to the right).")]
        public bool flipX;

        public bool HasFrames => idle.IsValid || walk.IsValid;
        public Clip Current { get; private set; }

        SpriteRenderer sr;
        ClipFrames active;
        float t, speedScale = 1f;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            Play(Clip.Idle);
        }

        public void Play(Clip clip, float speed = 1f)
        {
            speedScale = Mathf.Max(0.1f, speed);
            var f = Resolve(clip);
            Current = clip;
            if (f == active) return;
            active = f;
            t = 0f;
        }

        ClipFrames Resolve(Clip c)
        {
            switch (c)
            {
                case Clip.Run: return run.IsValid ? run : Resolve(Clip.Walk);
                case Clip.Walk: return walk.IsValid ? walk : idle;
                case Clip.Sleep: return sleep.IsValid ? sleep : Resolve(Clip.Sit);
                case Clip.Pounce: return pounce.IsValid ? pounce : Resolve(Clip.Sit);
                case Clip.Sit: return sit.IsValid ? sit : idle;
                default: return idle;
            }
        }

        void Update()
        {
            if (sr == null) return;
            sr.flipX = flipX;
            if (active == null || !active.IsValid) return;

            t += Time.deltaTime * active.fps * speedScale;
            int n = active.frames.Length;
            int i = active.loop ? (int)t % n : Mathf.Min((int)t, n - 1);
            sr.sprite = active.frames[i];
        }
    }
}
