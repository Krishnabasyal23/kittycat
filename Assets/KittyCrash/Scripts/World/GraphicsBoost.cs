using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KittyCrash
{
    /// <summary>
    /// URP "make it look good" pass, all from code:
    ///   • a Global 2D Light whose colour/intensity follows the seasons (warm summer, golden autumn,
    ///     cool blue winter, soft pink spring) and blends during transitions
    ///   • post-processing: gentle bloom (snow, lights and coins glow), vignette, a touch of saturation/contrast
    /// Needs the Universal Render Pipeline (your project uses it). If this file ever causes a compile error,
    /// just delete it — nothing else depends on it.
    /// </summary>
    public class GraphicsBoost : MonoBehaviour
    {
        [Header("Seasonal light (colour, intensity)")]
        public Color springLight = new Color(1f, 0.96f, 0.98f); public float springIntensity = 1.05f;
        public Color summerLight = new Color(1f, 0.97f, 0.88f); public float summerIntensity = 1.1f;
        public Color autumnLight = new Color(1f, 0.86f, 0.72f); public float autumnIntensity = 1.0f;
        public Color winterLight = new Color(0.84f, 0.91f, 1f); public float winterIntensity = 1.0f;

        [Header("Post-processing")]
        [Range(0f, 2f)] public float bloomIntensity = 0.45f;
        [Range(0f, 1.5f)] public float bloomThreshold = 0.9f;
        [Range(0f, 1f)] public float vignette = 0.24f;
        [Range(-50f, 50f)] public float saturation = 12f;
        [Range(-50f, 50f)] public float contrast = 8f;

        Light2D globalLight;

        void Start()
        {
            // Global 2D light
            var lightGO = new GameObject("Seasonal Global Light");
            lightGO.transform.SetParent(transform, false);
            globalLight = lightGO.AddComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
            globalLight.intensity = 1f;

            // Post-processing volume
            var volGO = new GameObject("Post Volume");
            volGO.transform.SetParent(transform, false);
            var vol = volGO.AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(bloomIntensity);
            bloom.threshold.Override(bloomThreshold);
            bloom.scatter.Override(0.6f);

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(vignette);
            vig.smoothness.Override(0.45f);

            var ca = profile.Add<ColorAdjustments>(true);
            ca.saturation.Override(saturation);
            ca.contrast.Override(contrast);

            vol.sharedProfile = profile;

            var cam = Camera.main;
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = true;
            }
        }

        void LateUpdate()
        {
            var ss = SeasonSystem.Instance;
            if (ss == null || globalLight == null) return;
            Get(ss.CurrentSeason, out var c1, out var i1);
            Get(ss.NextSeason, out var c2, out var i2);
            globalLight.color = Color.Lerp(c1, c2, ss.Blend);
            globalLight.intensity = Mathf.Lerp(i1, i2, ss.Blend);
        }

        void Get(Season s, out Color c, out float i)
        {
            switch (s)
            {
                case Season.Spring: c = springLight; i = springIntensity; break;
                case Season.Summer: c = summerLight; i = summerIntensity; break;
                case Season.Autumn: c = autumnLight; i = autumnIntensity; break;
                default: c = winterLight; i = winterIntensity; break;
            }
        }
    }
}
