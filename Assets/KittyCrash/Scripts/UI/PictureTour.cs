using UnityEngine;
using UnityEngine.UI;

namespace KittyCrash
{
    /// <summary>
    /// How to play without words: four animated cards built from the game's own art (the player's cat,
    /// the fish, the paw, the mood faces) and numbers only.
    ///   1. bet: a paw taps the green bet button     2. the cat walks and the multiplier climbs
    ///   3. cash out in time: tap, coins, happy face  4. the cat sees a fish first: red, sad face, bet lost
    /// Arrows or Left/Right step through, Esc or X closes. GameMenu opens it (How to play, F1) and on the first run.
    /// </summary>
    public class PictureTour : MonoBehaviour
    {
        const string SeenKey = "kc_tour_seen";
        static readonly Color Bg = new Color(0.07f, 0.06f, 0.12f, 0.98f);
        static readonly Color Card = new Color(0.14f, 0.13f, 0.22f, 1f);
        static readonly Color Green = new Color(0.30f, 0.90f, 0.50f, 1f);
        static readonly Color Orange = new Color(1f, 0.65f, 0.18f, 1f);
        static readonly Color Red = new Color(1f, 0.36f, 0.38f, 1f);
        static readonly Color Ink = new Color(0.96f, 0.95f, 1f, 1f);
        static readonly Color Dark = new Color(0.08f, 0.07f, 0.12f, 1f);

        public static bool Seen => PlayerPrefs.GetInt(SeenKey, 0) == 1;

        int page;
        float t;
        Font bold;
        RectTransform stage;
        Image cat, paw, fish, face, button, bang, chipBg;
        Text mult, chip, buttonText, coins;
        Image[] dots = new Image[4];
        Sprite[] walk;
        Sprite happy, sad, calm, circle;

        public static PictureTour Open(Transform canvas, Font bold)
        {
            var go = new GameObject("PictureTour", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var tour = go.AddComponent<PictureTour>();
            tour.bold = bold;
            tour.Build();
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
            return tour;
        }

        public void Close() => Destroy(gameObject);

        void Build()
        {
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            gameObject.AddComponent<Image>().color = Bg;
            circle = CircleSprite();
            FindArt();

            var card = Rect("Card", root);
            Center(card, new Vector2(0f, 40f), new Vector2(1000f, 640f));
            card.gameObject.AddComponent<Image>().color = Card;
            stage = card;

            mult = Label(card, "1.00x", 120, Ink);
            Place(mult.rectTransform, new Vector2(0f, 200f), new Vector2(700f, 150f));
            cat = Img(card, walk != null && walk.Length > 0 ? walk[0] : calm, new Vector2(-170f, -60f), new Vector2(260f, 260f));
            fish = Img(card, FishSprite(), new Vector2(230f, -110f), new Vector2(120f, 120f));
            bang = Img(card, CuteSprite("exclaim"), new Vector2(-120f, 110f), new Vector2(80f, 80f));
            face = Img(card, calm, new Vector2(330f, 180f), new Vector2(130f, 130f));

            button = Img(card, null, new Vector2(170f, -210f), new Vector2(360f, 110f));
            buttonText = Label(button.rectTransform, "10", 54, Dark);
            Stretch(buttonText.rectTransform);
            chipBg = Img(card, circle, new Vector2(-300f, -210f), new Vector2(100f, 100f));
            chipBg.color = Orange;
            chip = Label(chipBg.rectTransform, "10", 44, Dark);
            Stretch(chip.rectTransform);
            coins = Label(card, "", 56, Green);
            Place(coins.rectTransform, new Vector2(170f, -100f), new Vector2(300f, 80f));
            paw = Img(card, CuteSprite("pawPrint"), new Vector2(260f, -260f), new Vector2(90f, 90f));

            // navigation: arrows, dots, close (symbols only)
            NavButton(root, "<", new Vector2(-600f, 40f), () => Go(page - 1));
            NavButton(root, ">", new Vector2(600f, 40f), () => Go(page + 1));
            var close = NavButton(root, "X", Vector2.zero, Close);
            var cr = (RectTransform)close.transform;
            cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
            cr.anchoredPosition = new Vector2(-40f, -40f);
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                dots[i] = Img(root, circle, new Vector2(-60f + i * 40f, -330f), new Vector2(22f, 22f));
                var b = dots[i].gameObject.AddComponent<Button>();
                b.onClick.AddListener(() => Go(k));
            }
            Go(0);
        }

        void Go(int p)
        {
            if (p >= 4) { Close(); return; }
            page = Mathf.Clamp(p, 0, 3);
            t = 0f;
            for (int i = 0; i < 4; i++) dots[i].color = i == page ? Orange : new Color(1f, 1f, 1f, 0.25f);
        }

        void Update()
        {
            if (Left()) Go(page - 1);
            else if (Right()) Go(page + 1);
            else if (Esc()) { Close(); return; }

            t += Time.unscaledDeltaTime;
            float loop = Mathf.Repeat(t, 4f);
            if (walk != null && walk.Length > 0) cat.sprite = walk[Mathf.FloorToInt(t * 10f) % walk.Length];

            // defaults, then each card shows its own story
            Show(fish, false);
            Show(bang, false);
            coins.text = "";
            face.sprite = calm;
            mult.color = Ink;
            button.color = Green;
            buttonText.text = "10";
            Show(paw, true);
            chipBg.enabled = chip.enabled = true;
            chipBg.rectTransform.anchoredPosition = new Vector2(-300f, -210f);
            float m = 1f;

            switch (page)
            {
                case 0: // bet: the paw taps the bet button, the chip jumps into it
                    m = 1f;
                    paw.rectTransform.anchoredPosition = new Vector2(260f, -260f) + Vector2.up * Mathf.Abs(Mathf.Sin(loop * 3f)) * 30f;
                    bool tapped = loop > 1.6f;
                    button.color = tapped ? new Color(0.2f, 0.6f, 0.35f) : Green;
                    chipBg.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(-300f, -210f), new Vector2(170f, -210f), Mathf.Clamp01((loop - 1.6f) * 2f));
                    chipBg.enabled = chip.enabled = loop < 2.1f;
                    face.sprite = tapped ? happy : calm;
                    break;
                case 1: // the cat walks, the multiplier climbs
                    Show(paw, false);
                    m = Mathf.Exp(0.35f * loop);
                    button.color = Orange;
                    buttonText.text = (10f * m).ToString("0");
                    face.sprite = m > 2.5f ? happy : calm;
                    break;
                case 2: // cash out in time: tap at about 2x, coins, happy face
                    m = Mathf.Exp(0.35f * Mathf.Min(loop, 2f));
                    bool cashed = loop >= 2f;
                    button.color = cashed ? Green : Orange;
                    buttonText.text = (10f * m).ToString("0");
                    paw.rectTransform.anchoredPosition = new Vector2(260f, -260f) + Vector2.up * (cashed ? 0f : Mathf.Abs(Mathf.Sin(loop * 4f)) * 30f);
                    coins.text = cashed ? "+" + (10f * m).ToString("0") : "";
                    coins.rectTransform.anchoredPosition = new Vector2(170f, -100f + (cashed ? (loop - 2f) * 40f : 0f));
                    mult.color = cashed ? Green : Ink;
                    face.sprite = cashed ? happy : calm;
                    break;
                case 3: // the cat sees a fish first: red, bet lost
                    Show(paw, false);
                    m = Mathf.Exp(0.35f * Mathf.Min(loop, 2.2f));
                    bool distracted = loop >= 2.2f;
                    Show(fish, loop >= 1.6f);
                    Show(bang, distracted);
                    mult.color = distracted ? Red : Ink;
                    button.color = distracted ? new Color(0.35f, 0.33f, 0.42f) : Orange;
                    buttonText.text = distracted ? "-10" : (10f * m).ToString("0");
                    face.sprite = distracted ? sad : calm;
                    break;
            }
            mult.text = m.ToString("0.00") + "x";
            Show(face, true);
            Show(cat, true);
        }

        /// <summary>Show an image only if its art was found (a missing sprite would draw a white box).</summary>
        static void Show(Image img, bool on) => img.enabled = on && img.sprite != null;

        // ── art from the scene (anything missing is simply not shown) ──

        void FindArt()
        {
            var skins = FindAnyObjectByType<CatSkins>();
            if (skins != null && skins.skins != null && skins.skins.Length > 0)
                walk = skins.skins[Mathf.Clamp(skins.Current, 0, skins.skins.Length - 1)].walk;
            var mood = FindAnyObjectByType<MoodFace>();
            if (mood != null) { happy = mood.win != null ? mood.win : mood.happy; sad = mood.sad; calm = mood.calm; }
        }

        static Sprite FishSprite()
        {
            var walker = FindAnyObjectByType<CatWalker>();
            int i = (int)DistractionType.Fish;
            return walker != null && walker.distractionSprites != null && i < walker.distractionSprites.Length ? walker.distractionSprites[i] : null;
        }

        static Sprite CuteSprite(string which)
        {
            var cute = FindAnyObjectByType<CatCute>();
            if (cute == null) return null;
            return which == "pawPrint" ? cute.pawPrint : cute.exclaim;
        }

        // ── input ──

        static bool Left()
        {
#if ENABLE_INPUT_SYSTEM
            var k = UnityEngine.InputSystem.Keyboard.current;
            return k != null && k.leftArrowKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.LeftArrow);
#endif
        }

        static bool Right()
        {
#if ENABLE_INPUT_SYSTEM
            var k = UnityEngine.InputSystem.Keyboard.current;
            return k != null && (k.rightArrowKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#endif
        }

        static bool Esc()
        {
#if ENABLE_INPUT_SYSTEM
            var k = UnityEngine.InputSystem.Keyboard.current;
            return k != null && k.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        // ── widgets ──

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Center(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        static void Place(RectTransform r, Vector2 pos, Vector2 size) => Center(r, pos, size);

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        }

        Image Img(Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
        {
            var img = Rect("Image", parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            Center(img.rectTransform, pos, size);
            return img;
        }

        Text Label(Transform parent, string text, int size, Color color)
        {
            var t = Rect("Text", parent).gameObject.AddComponent<Text>();
            t.font = bold;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        Button NavButton(Transform parent, string glyph, Vector2 pos, System.Action onClick)
        {
            var img = Rect("Nav", parent).gameObject.AddComponent<Image>();
            img.sprite = circle;
            img.color = Card;
            Center(img.rectTransform, pos, new Vector2(96f, 96f));
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick());
            var t = Label(img.rectTransform, glyph, 48, Ink);
            Stretch(t.rectTransform);
            return b;
        }

        static Sprite CircleSprite()
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N / 2f, N / 2f));
                    byte a = (byte)(Mathf.Clamp01(N / 2f - d) * 255f);
                    px[y * N + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
