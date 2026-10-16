using System;
using UnityEngine;
using UnityEngine.UI;

namespace KittyCrash
{
    /// <summary>
    /// Pick your cat: six skins from the Pet Cats pack, cleaned up (Scale2x edge-smoothing, all on the same pixel grid,
    /// paws on one shared ground line). A small "&lt; Ginger &gt;" picker sits at the bottom-left of the game view and the
    /// choice is remembered. Put it on the Cat (root); the installer fills in the frames.
    /// </summary>
    public class CatSkins : MonoBehaviour
    {
        [Serializable]
        public class Skin
        {
            public string name;
            public Sprite[] idle, walk, run, sit, sleep, pounce;
        }

        public Skin[] skins = new Skin[0];
        public int defaultSkin = 0;
        public bool showPicker = true;

        public int Current { get; private set; }
        public event Action<int> OnSkinChanged;

        const string Key = "KittyCrash_Skin";
        CatSpriteAnimator anim;
        SpriteRenderer sr;
        Text label;
        RectTransform area;
        Camera cam;

        void Start()
        {
            var walker = GetComponent<CatWalker>();
            var visual = walker != null && walker.visual != null ? walker.visual : (transform.childCount > 0 ? transform.GetChild(0) : null);
            if (visual != null) { anim = visual.GetComponent<CatSpriteAnimator>(); sr = visual.GetComponent<SpriteRenderer>(); }
            cam = Camera.main;
            int saved = 0;
            try { saved = PlayerPrefs.GetInt(Key, defaultSkin); } catch (Exception) { saved = defaultSkin; }
            Apply(saved);
            if (showPicker && skins.Length > 1) BuildPicker();
        }

        public void Next(int dir)
        {
            if (skins.Length == 0) return;
            Apply(((Current + dir) % skins.Length + skins.Length) % skins.Length);
        }

        public void Apply(int index)
        {
            if (skins == null || skins.Length == 0 || anim == null) return;
            Current = Mathf.Clamp(index, 0, skins.Length - 1);
            var s = skins[Current];
            Set(anim.idle, s.idle, 8f, true);
            Set(anim.walk, s.walk, 10f, true);
            Set(anim.run, s.run, 14f, true);
            Set(anim.sit, s.sit, 6f, true);
            Set(anim.sleep, s.sleep, 7f, false);   // lie down once, then stay curled up (no more "push-ups")
            Set(anim.pounce, s.pounce, 10f, false);
            if (sr != null && s.idle != null && s.idle.Length > 0) sr.sprite = s.idle[0];
            if (label != null) label.text = s.name;
            try { PlayerPrefs.SetInt(Key, Current); PlayerPrefs.Save(); } catch (Exception) { }
            OnSkinChanged?.Invoke(Current);
        }

        static void Set(CatSpriteAnimator.ClipFrames clip, Sprite[] frames, float fps, bool loop)
        {
            if (clip == null || frames == null || frames.Length == 0) return;
            clip.frames = frames;
            clip.fps = fps;
            clip.loop = loop;
        }

        // ── tiny picker UI ────────────────────────────

        void BuildPicker()
        {
            var font = Resources.Load<Font>("KittyCrashFonts/Fredoka-Bold");
            if (font == null)
            {
#if UNITY_2022_2_OR_NEWER
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            }
            var cgo = new GameObject("SkinPickerCanvas", typeof(RectTransform));
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 56;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            cgo.AddComponent<GraphicRaycaster>();

            area = NewRect("GameArea", cgo.transform);
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = area.offsetMax = Vector2.zero;

            var pill = NewRect("SkinPicker", area);
            pill.anchorMin = pill.anchorMax = pill.pivot = new Vector2(0f, 0f);
            pill.anchoredPosition = new Vector2(28f, 22f);
            pill.sizeDelta = new Vector2(300f, 56f);
            var bg = pill.gameObject.AddComponent<Image>();
            bg.color = new Color(0.07f, 0.08f, 0.13f, 0.78f);

            var title = MakeText("Title", pill, font, 15, new Color(0.65f, 0.68f, 0.8f), TextAnchor.UpperCenter);
            title.text = "CAT SKIN";
            var trt = title.rectTransform; trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -3f); trt.sizeDelta = new Vector2(0f, 18f);

            label = MakeText("Name", pill, font, 24, Color.white, TextAnchor.MiddleCenter);
            var lrt = label.rectTransform; lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(56f, 0f); lrt.offsetMax = new Vector2(-56f, -14f);
            label.text = skins[Current].name;

            Arrow(pill, font, "<", new Vector2(0f, 0.5f), new Vector2(6f, -6f), () => Next(-1));
            Arrow(pill, font, ">", new Vector2(1f, 0.5f), new Vector2(-6f, -6f), () => Next(1));
        }

        void Arrow(RectTransform parent, Font font, string glyph, Vector2 anchor, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var r = NewRect("Arrow" + glyph, parent);
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = anchor;
            r.anchoredPosition = pos;
            r.sizeDelta = new Vector2(44f, 36f);
            var img = r.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 0.66f, 0.18f, 0.95f);
            var btn = r.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var t = MakeText("Glyph", r, font, 26, new Color(0.1f, 0.08f, 0.12f), TextAnchor.MiddleCenter);
            t.text = glyph;
            var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        }

        void LateUpdate()
        {
            if (area == null) return;
            if (cam == null) cam = Camera.main;
            if (cam != null) { area.anchorMin = new Vector2(0f, cam.rect.y); area.anchorMax = new Vector2(1f, Mathf.Min(1f, cam.rect.y + cam.rect.height)); }
        }

        static RectTransform NewRect(string n, Transform parent)
        {
            var go = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Text MakeText(string n, Transform parent, Font f, int size, Color c, TextAnchor a)
        {
            var t = NewRect(n, parent).gameObject.AddComponent<Text>();
            t.font = f; t.fontSize = size; t.color = c; t.alignment = a; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
