using UnityEngine;
using UnityEngine.UI;

namespace KittyCrash
{
    /// <summary>
    /// "MEOW TO CASH OUT": switch it on (bottom-right of the game view) and any loud sound — a meow, a clap,
    /// the whole audience yelling "CASH OUT!" — cashes out your bet. A live level bar shows what the mic hears,
    /// the white tick is the trigger level (it adapts to the room's background noise).
    /// Works in the editor and desktop builds (browsers handle microphones differently, so it hides itself in WebGL).
    /// Put it on the Managers object (the installer does).
    /// </summary>
    public class VoiceCashOut : MonoBehaviour
    {
        [Tooltip("How much louder than the room's background noise a sound must be.")]
        [Range(1.5f, 12f)] public float sensitivity = 4f;
        [Tooltip("Never trigger below this loudness (0..1).")]
        [Range(0.01f, 0.5f)] public float minLevel = 0.08f;
        [Tooltip("The sound must last this long (filters out clicks).")]
        public float holdSeconds = 0.12f;

        RoundManager rm;
        Camera cam;
        RectTransform area, fill, tick;
        Text label, popup;
        Image btnBg;
        bool on;
        float level, noise = 0.02f, loudT, popupT = 99f;

#if !UNITY_WEBGL || UNITY_EDITOR
        AudioClip clip;
        string device;
        readonly float[] buf = new float[512];
#endif

        void Start()
        {
            rm = RoundManager.Instance;
            cam = Camera.main;
#if UNITY_WEBGL && !UNITY_EDITOR
            enabled = false;   // no Unity microphone API in the browser
#else
            Build();
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        void Toggle()
        {
            if (!on)
            {
                if (Microphone.devices.Length == 0) { label.text = "NO MICROPHONE FOUND"; return; }
                device = Microphone.devices[0];
                clip = Microphone.Start(device, true, 1, 16000);
                on = clip != null;
                noise = 0.02f;
            }
            else
            {
                Microphone.End(device);
                on = false;
            }
            label.text = on ? "MEOW TO CASH OUT: ON" : "MEOW TO CASH OUT: OFF";
            btnBg.color = on ? new Color(0.3f, 0.9f, 0.5f, 0.95f) : new Color(0.07f, 0.08f, 0.13f, 0.82f);
            label.color = on ? new Color(0.06f, 0.08f, 0.1f) : Color.white;
        }

        void OnDisable() { if (on && device != null) Microphone.End(device); on = false; }

        void Update()
        {
            if (cam == null) cam = Camera.main;
            if (area != null && cam != null) { area.anchorMin = new Vector2(0f, cam.rect.y); area.anchorMax = new Vector2(1f, Mathf.Min(1f, cam.rect.y + cam.rect.height)); }

            if (on && clip != null)
            {
                int pos = Microphone.GetPosition(device) - buf.Length;
                if (pos < 0) pos += clip.samples;
                clip.GetData(buf, pos);
                float sum = 0f;
                for (int i = 0; i < buf.Length; i++) sum += buf[i] * buf[i];
                float rms = Mathf.Sqrt(sum / buf.Length);
                level = Mathf.Lerp(level, rms, Time.deltaTime * 20f);

                float threshold = Mathf.Max(minLevel, noise * sensitivity);
                if (rms < threshold) noise = Mathf.Lerp(noise, rms, Time.deltaTime * 0.5f);   // learn the room
                loudT = rms > threshold ? loudT + Time.deltaTime : 0f;

                bool canCash = rm != null && rm.Phase == RoundPhase.Running && rm.HasBet && !rm.CashedOut && !rm.CashOutPending && !rm.IsWarmingUp;
                if (canCash && loudT >= holdSeconds)
                {
                    rm.CashOut();
                    loudT = 0f;
                    popupT = 0f;
                }
                fill.anchorMax = new Vector2(Mathf.Clamp01(level / 0.4f), 1f);
                tick.anchorMin = tick.anchorMax = new Vector2(Mathf.Clamp01(threshold / 0.4f), 0.5f);
            }
            else if (fill != null) fill.anchorMax = new Vector2(0f, 1f);

            if (popup != null)
            {
                popupT += Time.deltaTime;
                popup.enabled = popupT < 1.4f;
                if (popup.enabled)
                {
                    float s = popupT < 0.15f ? Mathf.Lerp(0.5f, 1.2f, popupT / 0.15f) : 1f;
                    popup.rectTransform.localScale = Vector3.one * s;
                    var c = popup.color; c.a = 1f - Mathf.Clamp01((popupT - 1f) / 0.4f); popup.color = c;
                }
            }
        }

        void Build()
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
            var cgo = new GameObject("VoiceCanvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 56;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            cgo.AddComponent<GraphicRaycaster>();

            area = R("GameArea", cgo.transform);
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = area.offsetMax = Vector2.zero;

            var btn = R("VoiceToggle", area);
            btn.anchorMin = btn.anchorMax = btn.pivot = new Vector2(1f, 0f);
            btn.anchoredPosition = new Vector2(-28f, 22f);
            btn.sizeDelta = new Vector2(300f, 56f);
            btnBg = btn.gameObject.AddComponent<Image>();
            btnBg.color = new Color(0.07f, 0.08f, 0.13f, 0.82f);
            var b = btn.gameObject.AddComponent<Button>();
            b.targetGraphic = btnBg;
            b.onClick.AddListener(Toggle);

            label = T("Label", btn, font, 19, Color.white);
            var lr = label.rectTransform; lr.anchorMin = new Vector2(0f, 0.35f); lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
            label.text = "MEOW TO CASH OUT: OFF";

            var meter = R("Meter", btn);
            meter.anchorMin = new Vector2(0.06f, 0.12f); meter.anchorMax = new Vector2(0.94f, 0.28f);
            meter.offsetMin = meter.offsetMax = Vector2.zero;
            var mbg = meter.gameObject.AddComponent<Image>(); mbg.color = new Color(0f, 0f, 0f, 0.5f); mbg.raycastTarget = false;
            fill = R("Fill", meter);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.offsetMin = fill.offsetMax = Vector2.zero;
            var fi = fill.gameObject.AddComponent<Image>(); fi.color = new Color(1f, 0.66f, 0.18f); fi.raycastTarget = false;
            tick = R("Tick", meter);
            tick.sizeDelta = new Vector2(3f, 14f);
            var ti = tick.gameObject.AddComponent<Image>(); ti.color = Color.white; ti.raycastTarget = false;

            popup = T("Popup", area, font, 54, new Color(0.4f, 0.95f, 0.55f));
            var pr = popup.rectTransform; pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.42f); pr.sizeDelta = new Vector2(900f, 80f);
            popup.text = "MEOW! CASHED OUT";
            popup.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3f, -3f);
            popup.enabled = false;
        }

        static RectTransform R(string n, Transform p)
        {
            var go = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(p, false);
            return (RectTransform)go.transform;
        }

        static Text T(string n, Transform p, Font f, int size, Color c)
        {
            var t = R(n, p).gameObject.AddComponent<Text>();
            t.font = f; t.fontSize = size; t.color = c; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
#endif
    }
}
