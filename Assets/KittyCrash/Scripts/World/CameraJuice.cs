using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// Tension camera: slowly zooms in on the cat as the multiplier climbs (ground stays pinned to the
    /// bottom of the screen), shakes when kitty gets distracted, small punch on cash-out.
    /// Sits on the Main Camera next to CameraFollow (which only moves X; this only touches Y + size).
    /// </summary>
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(Camera))]
    public class CameraJuice : MonoBehaviour
    {
        [Header("Zoom")]
        public float minSize = 4.4f;
        [Tooltip("Multiplier at which the zoom is fully in.")]
        public float fullZoomAtMultiplier = 10f;
        public float zoomSmoothing = 1.5f;
        public bool keepGroundPinned = true;

        [Header("Shake")]
        public float crashShake = 0.22f;
        public float cashOutPunch = 0.08f;
        public float shakeDecay = 3.5f;
        public float shakeFrequency = 25f;

        Camera cam;
        float baseSize, baseY, size, shake;

        void Start()
        {
            cam = GetComponent<Camera>();
            baseSize = size = cam.orthographicSize;
            baseY = transform.position.y;

            var rm = RoundManager.Instance;
            if (rm != null)
            {
                rm.OnCrash += HandleCrash;
                rm.OnCashOut += HandleCashOut;
            }
        }

        void OnDestroy()
        {
            var rm = RoundManager.Instance;
            if (rm == null) return;
            rm.OnCrash -= HandleCrash;
            rm.OnCashOut -= HandleCashOut;
        }

        void HandleCrash(float m, string d) => shake = Mathf.Max(shake, crashShake);
        void HandleCashOut(float m, decimal p) => shake = Mathf.Max(shake, cashOutPunch);

        void LateUpdate()
        {
            var rm = RoundManager.Instance;
            float target = baseSize;
            if (rm != null && rm.Phase == RoundPhase.Running)
            {
                float k = Mathf.InverseLerp(0f, Mathf.Log(Mathf.Max(1.01f, fullZoomAtMultiplier)), Mathf.Log(Mathf.Max(1f, rm.Multiplier)));
                target = Mathf.Lerp(baseSize, minSize, k);
            }

            size = Mathf.Lerp(size, target, 1f - Mathf.Exp(-zoomSmoothing * Time.deltaTime));
            cam.orthographicSize = size;

            shake = Mathf.MoveTowards(shake, 0f, shakeDecay * shake * Time.deltaTime + 0.0005f);
            float offset = (Mathf.PerlinNoise(Time.time * shakeFrequency, 0.37f) - 0.5f) * 2f * shake;

            var p = transform.position;
            p.y = baseY - (keepGroundPinned ? baseSize - size : 0f) + offset;
            transform.position = p;
        }
    }
}
