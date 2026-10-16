using UnityEngine;
using UnityEngine.UI;

namespace KittyCrash
{
    /// <summary>
    /// One pixel size for everything. The game world is rendered into a small texture where 1 texel = 1/16 world unit
    /// (the art's own pixel size), then scaled up to the screen with hard edges. So the cat, the plants, the
    /// backgrounds and the festival props all show perfectly square, equally-sized pixels — no more mix of big and
    /// small pixels. The UI (betting panel, mood face, text) is not affected and stays sharp.
    /// The camera position is also snapped to the pixel grid so sprites don't shimmer while scrolling.
    /// Put it on the Main Camera. Untick it to go back to normal rendering.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(Camera))]
    public class PixelGrid : MonoBehaviour
    {
        [Tooltip("Art pixels per world unit (Kitty Crash art is drawn at 32).")]
        public float pixelsPerUnit = 32f;
        public bool snapCamera = true;

        Camera cam, presenter;
        RenderTexture rt;
        Canvas canvas;
        RawImage image;
        float baseOrtho;
        Vector3 realPos;
        bool snapped;

        /// <summary>Like Camera.WorldToScreenPoint, but correct when the camera renders into the pixel-grid texture.</summary>
        public static Vector3 WorldToScreen(Camera c, Vector3 world)
        {
            var p = c.WorldToScreenPoint(world);
            var tt = c.targetTexture;
            if (tt != null) { p.x *= Screen.width / (float)tt.width; p.y *= Screen.height / (float)tt.height; }
            return p;
        }

        void OnEnable()
        {
            cam = GetComponent<Camera>();
            baseOrtho = 0f; // read on the first frame, after the UI has set the game-view zoom

            var pgo = new GameObject("PixelGridPresenter");
            pgo.hideFlags = HideFlags.DontSave;
            presenter = pgo.AddComponent<Camera>();
            presenter.cullingMask = 0;
            presenter.clearFlags = CameraClearFlags.SolidColor;
            presenter.backgroundColor = Color.black;
            presenter.depth = -100;
            presenter.orthographic = true;

            var cgo = new GameObject("PixelGridCanvas", typeof(RectTransform));
            cgo.hideFlags = HideFlags.DontSave;
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;          // under every other UI canvas
            var ig = new GameObject("World", typeof(RectTransform));
            ig.transform.SetParent(cgo.transform, false);
            image = ig.AddComponent<RawImage>();
            image.raycastTarget = false;
            var r = image.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        }

        void OnDisable()
        {
            if (cam != null) cam.targetTexture = null;
            if (rt != null) { rt.Release(); Destroy(rt); rt = null; }
            if (presenter != null) Destroy(presenter.gameObject);
            if (canvas != null) Destroy(canvas.gameObject);
            if (snapped) { transform.position = realPos; snapped = false; }
        }

        void Update()
        {
            // undo last frame's snap so follow/zoom scripts keep working with the real, smooth position
            if (snapped) { transform.position = realPos; snapped = false; }
        }

        void LateUpdate()
        {
            if (baseOrtho <= 0f) baseOrtho = cam.orthographicSize;
            int sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
            float viewH = cam.rect.height > 0.01f ? cam.rect.height : 1f;
            // screen pixels per art pixel, chosen so 1 render-texture texel = 1 art pixel at the normal zoom
            float k = Mathf.Max(1f, sh * viewH / (2f * Mathf.Max(0.1f, baseOrtho) * pixelsPerUnit));
            int rw = Mathf.Max(16, Mathf.RoundToInt(sw / k));
            int rh = Mathf.Max(16, Mathf.RoundToInt(sh / k));

            if (rt == null || rt.width != rw || rt.height != rh)
            {
                cam.targetTexture = null;
                if (rt != null) { rt.Release(); Destroy(rt); }
                rt = new RenderTexture(rw, rh, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, name = "PixelGridRT" };
                rt.Create();
                cam.targetTexture = rt;
                image.texture = rt;
            }

            if (snapCamera)
            {
                realPos = transform.position;
                float step = 2f * cam.orthographicSize / Mathf.Max(1f, rt.height * viewH);   // world size of one texel
                transform.position = new Vector3(Mathf.Round(realPos.x / step) * step, Mathf.Round(realPos.y / step) * step, realPos.z);
                snapped = true;
            }
        }
    }
}
