using System.Collections.Generic;
using UnityEngine;

namespace KittyCrash
{

    public class CatCute : MonoBehaviour
    {
        [Header("Emotes")]
        public Sprite bubble;
        public Sprite heart, exclaim, question, zzz, note, star;
        [Tooltip("Bubble position above the cat's root (0 = auto from the cat sprite).")]
        public Vector2 bubbleOffset = Vector2.zero;

        [Header("Particles & prints")]
        public Sprite heartParticle, sparkle, pawPrint;
        public float pawEvery = 0.55f;

        [Header("Bounce")]
        [Range(0f, 0.15f)] public float walkSquash = 0.06f;
        public float hopHeight = 0.55f;

        CatWalker walker;
        RoundManager rm;
        Transform visual;
        Vector3 visualPos, visualScale;
        bool animatedArt;

        SpriteRenderer bubbleSR, iconSR;
        Transform bubbleT;
        float emoteT = 99f, emoteDur;
        float hopT = 99f;
        float idleTimer = 6f;
        bool wasFidget, starShown;

        float pawDist; bool leftPaw;
        class Print { public SpriteRenderer sr; public float age, life, alpha; }
        readonly List<Print> prints = new List<Print>();
        readonly Stack<SpriteRenderer> printPool = new Stack<SpriteRenderer>();

        void Start()
        {
            walker = GetComponent<CatWalker>();
            rm = RoundManager.Instance;
            visual = walker != null ? walker.visual : null;
            if (visual != null)
            {
                visualPos = visual.localPosition;
                visualScale = visual.localScale;
                var anim = visual.GetComponent<CatSpriteAnimator>();
                animatedArt = anim != null && anim.HasFrames;
            }

            if (bubbleOffset == Vector2.zero)
            {
                float h = 1.6f;
                var sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
                if (sr != null && sr.sprite != null) h = Mathf.Max(1f, sr.bounds.max.y - transform.position.y) * 0.9f;
                bubbleOffset = new Vector2(0.55f, h);
            }

            var b = new GameObject("EmoteBubble");
            bubbleT = b.transform;
            bubbleT.SetParent(transform, false);
            bubbleSR = b.AddComponent<SpriteRenderer>();
            bubbleSR.sprite = bubble;
            bubbleSR.sortingOrder = 32;
            var i = new GameObject("Icon");
            i.transform.SetParent(bubbleT, false);
            i.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            iconSR = i.AddComponent<SpriteRenderer>();
            iconSR.sortingOrder = 33;
            bubbleT.gameObject.SetActive(false);

            if (rm != null)
            {
                rm.OnRoundStarted += () => { Emote(note, 1.3f); Hop(0.5f); starShown = false; };
                rm.OnCashOut += (m, p) => { Emote(heart, 1.8f); Hop(1f); Hearts(); };
                rm.OnCrash += (c, d) => { if (!rm.CashedOut) Emote(exclaim, 1.4f); };
            }
        }

        public void Emote(Sprite icon, float duration)
        {
            if (icon == null || bubble == null) return;
            iconSR.sprite = icon;
            emoteT = 0f;
            emoteDur = duration;
            bubbleT.gameObject.SetActive(true);
        }

        public void Hop(float strength) { hopT = 0f; hopStrength = strength; }
        float hopStrength = 1f;

        void Hearts()
        {
            var c = transform.position + new Vector3(0f, bubbleOffset.y * 0.7f, 0f);
            for (int k = 0; k < 9; k++)
                FxPool.Spawn(c + new Vector3(Random.Range(-0.5f, 0.5f), 0f, 0f), new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(1.2f, 2.4f)),
                    Color.white, Random.Range(1.4f, 2.2f), Random.Range(1.1f, 1.7f), -0.4f, 31, heartParticle);
            for (int k = 0; k < 8; k++)
                FxPool.Spawn(c, Random.insideUnitCircle * 2.5f, Color.white, 1.4f, 0.6f, 0f, 31, sparkle);
        }

        void Update()
        {
            if (walker == null) return;
            UpdateEmotes();
            UpdateBounce();
            UpdatePrints();
        }

        void UpdateEmotes()
        {
            // automatic emotes
            if (rm != null)
            {
                bool fidget = walker.IsFidgeting;
                if (fidget && !wasFidget && Random.value < 0.6f) Emote(question, 0.9f);
                wasFidget = fidget;

                if (rm.Phase == RoundPhase.Running && !starShown && rm.Multiplier >= 5f) { Emote(star, 1.2f); starShown = true; }

                if (rm.Phase == RoundPhase.Idle)
                {
                    idleTimer -= Time.deltaTime;
                    if (idleTimer <= 0f) { Emote(Random.value < 0.6f ? note : heart, 1.4f); idleTimer = Random.Range(7f, 12f); }
                }
                else idleTimer = Random.Range(4f, 7f);
            }

            if (!bubbleT.gameObject.activeSelf) return;
            emoteT += Time.deltaTime;
            float pop = emoteT < 0.18f ? Mathf.Lerp(0.2f, 1.2f, emoteT / 0.18f) : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((emoteT - 0.18f) / 0.12f));
            float fade = 1f - Mathf.Clamp01((emoteT - emoteDur) / 0.25f);
            bubbleT.localScale = Vector3.one * pop * 0.3f;
            bubbleT.localPosition = new Vector3(bubbleOffset.x, bubbleOffset.y + Mathf.Sin(emoteT * 4f) * 0.05f, 0f);
            bubbleSR.color = new Color(1f, 1f, 1f, fade);
            iconSR.color = new Color(1f, 1f, 1f, fade);
            if (fade <= 0f) bubbleT.gameObject.SetActive(false);
        }

        void UpdateBounce()
        {
            if (visual == null || !animatedArt) return; // placeholder square already bobs via CatWalker

            float sp = walker.CurrentSpeed;
            float sq = 0f;
            if (sp > 0.05f) sq = Mathf.Sin(Time.time * (7f + sp * 2.2f)) * walkSquash * Mathf.Clamp01(sp);
            else sq = Mathf.Sin(Time.time * 2.2f) * 0.015f; // breathing

            float hopY = 0f;
            if (hopT < 0.5f)
            {
                hopT += Time.deltaTime;
                float k = Mathf.Clamp01(hopT / 0.42f);
                hopY = Mathf.Sin(k * Mathf.PI) * hopHeight * hopStrength;
                sq += (k < 0.15f ? -0.12f : k > 0.85f ? -0.1f : 0.08f) * hopStrength; // crouch, stretch, land
            }

            visual.localScale = new Vector3(visualScale.x * (1f - sq * 0.6f), visualScale.y * (1f + sq), visualScale.z);
            visual.localPosition = visualPos + new Vector3(0f, hopY, 0f);
        }

        void UpdatePrints()
        {
            float sp = walker.CurrentSpeed;
            if (sp > 0.3f && pawPrint != null)
            {
                pawDist += sp * Time.deltaTime;
                if (pawDist >= pawEvery)
                {
                    pawDist = 0f;
                    leftPaw = !leftPaw;
                    var ss = SeasonSystem.Instance;
                    float snow = ss != null ? ss.Weight(Season.Winter) : 0f;
                    var sr = printPool.Count > 0 ? printPool.Pop() : NewPrint();
                    sr.gameObject.SetActive(true);
                    sr.transform.position = transform.position + new Vector3(-0.15f, leftPaw ? 0.06f : -0.02f, 0f);
                    var p = new Print { sr = sr, age = 0f, life = Mathf.Lerp(2.5f, 7f, snow), alpha = Mathf.Lerp(0.22f, 0.5f, snow) };
                    sr.color = Color.Lerp(new Color(0.35f, 0.25f, 0.18f), new Color(0.55f, 0.62f, 0.78f), snow);
                    prints.Add(p);
                }
            }

            for (int i = prints.Count - 1; i >= 0; i--)
            {
                var p = prints[i];
                p.age += Time.deltaTime;
                float a = p.alpha * (1f - Mathf.Clamp01(p.age / p.life));
                var c = p.sr.color; c.a = a; p.sr.color = c;
                if (p.age >= p.life) { p.sr.gameObject.SetActive(false); printPool.Push(p.sr); prints.RemoveAt(i); }
            }
        }

        SpriteRenderer NewPrint()
        {
            var go = new GameObject("PawPrint");
            go.transform.localScale = Vector3.one * 0.9f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = pawPrint;
            sr.sortingOrder = -19; // just above the ground
            return sr;
        }
    }
}
