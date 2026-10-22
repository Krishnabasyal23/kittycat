using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KittyCrash
{
    /// <summary>
    /// Small footer with the build version: "Catino v2.0.0-alpha.4 · server 2.0.0-alpha.4" (or "offline"),
    /// plus the server's promo line such as "HAPPY HOUR (demo)".
    /// Installs itself at runtime in any scene that has a RoundManager, so no scene or prefab edit is needed.
    /// </summary>
    public class VersionFooter : MonoBehaviour
    {
        /// <summary>Keep in step with the git tag (v2.0.0-alpha.N) and VERSION in KittyAPI/main.py.</summary>
        public const string ClientVersion = "2.0.0-alpha.6";

        static readonly Color Muted = new Color(1f, 1f, 1f, 0.55f);
        static readonly Color Promo = new Color(1f, 0.82f, 0.24f, 0.95f);

        Text label;
        float nextRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded += (scene, mode) => TryAdd();
            TryAdd();
        }

        static void TryAdd()
        {
            if (RoundManager.Instance == null || FindAnyObjectByType<VersionFooter>() != null) return;
            new GameObject("VersionFooter").AddComponent<VersionFooter>();
        }

        void Awake()
        {
            var font = Resources.Load<Font>("KittyCrashFonts/Fredoka-Medium");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var cgo = new GameObject("VersionFooterCanvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            var rt = new GameObject("VersionText", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(cgo.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-14f, 8f);
            rt.sizeDelta = new Vector2(700f, 24f);

            label = rt.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = 16;
            label.alignment = TextAnchor.LowerRight;
            label.raycastTarget = false;
            label.supportRichText = true;
            label.color = Muted;
            Refresh();
        }

        void Update()
        {
            // the server version arrives after /api/health answers, so check again now and then
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 1f;
            Refresh();
        }

        void Refresh()
        {
            var rm = RoundManager.Instance;
            var api = rm != null ? rm.Backend as ApiBackend : null;

            string text = "Catino v" + ClientVersion;
            if (api == null) text += "  ·  offline";
            else if (!string.IsNullOrEmpty(api.ServerVersion)) text += "  ·  server " + api.ServerVersion;
            else text += "  ·  server …";

            if (api != null && !string.IsNullOrEmpty(api.Promo))
                text += "  ·  <color=#" + ColorUtility.ToHtmlStringRGBA(Promo) + ">" + api.Promo + "</color>";

            label.text = text;
        }
    }
}
