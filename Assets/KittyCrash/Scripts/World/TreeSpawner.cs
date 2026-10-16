using System.Collections.Generic;
using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// Pool of scenery (trees, bushes, flowers) placed along the path ahead of the camera and recycled behind it.
    /// Put several prefabs in the list for variety. Their colours come from SeasonalSprite components.
    /// Several spawners can run at once as depth rows: Back (small, darker, behind), Main, Front (in front of the cat).
    /// </summary>
    public class TreeSpawner : MonoBehaviour
    {
        public enum Row { Main, Back, Front }

        public Row row = Row.Main;
        public GameObject[] prefabs;
        public int poolSize = 16;
        public float groundY = -3f;
        public Vector2 spacing = new Vector2(2.5f, 7f);
        public Vector2 scaleRange = new Vector2(0.8f, 1.4f);

        [Header("Depth row look")]
        [Tooltip("Force every sprite in this row onto one sorting order (crossfade layer = order + 1). Off = keep each prefab's own order.")]
        public bool overrideSorting = false;
        public int sortingOrder = -16;
        [Tooltip("Darken (< 1) far rows so they read as distance.")]
        [Range(0.3f, 1.2f)] public float brightness = 1f;
        [Tooltip("Randomly mirror plants for variety.")]
        public bool randomFlip = true;
        [Tooltip("Random up/down offset per plant.")]
        public float yJitter = 0f;

        readonly List<Transform> pool = new List<Transform>();
        float nextX;
        Camera cam;

        /// <summary>The main scenery row (the one the editor tools edit).</summary>
        public static TreeSpawner FindMain()
        {
#if UNITY_2023_1_OR_NEWER
            var all = Object.FindObjectsByType<TreeSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var all = Object.FindObjectsOfType<TreeSpawner>(true);
#endif
            foreach (var s in all) if (s.row == Row.Main) return s;
            return all.Length > 0 ? all[0] : null;
        }

        void Start()
        {
            cam = Camera.main;
            if (prefabs == null || prefabs.Length == 0) return;

            nextX = cam != null ? cam.ViewportToWorldPoint(Vector3.zero).x - 2f : -10f;
            for (int i = 0; i < poolSize; i++)
            {
                var prefab = prefabs[Random.Range(0, prefabs.Length)];
                if (prefab == null) continue;
                var t = Instantiate(prefab, transform).transform;
                Dress(t);
                pool.Add(t);
                Place(t);
            }
        }

        void Dress(Transform t)
        {
            foreach (var sr in t.GetComponentsInChildren<SpriteRenderer>(true))
            {
                bool cross = sr.gameObject.name == "SeasonCrossfade";
                if (overrideSorting) sr.sortingOrder = sortingOrder + (cross ? 1 : 0);
                if (randomFlip && !cross && Random.value < 0.5f) sr.flipX = !sr.flipX;
            }
            foreach (var ss in t.GetComponentsInChildren<SeasonalSprite>(true)) ss.brightness = brightness;
            if (brightness != 1f)
                foreach (var sr in t.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sr.GetComponent<SeasonalSprite>() == null && sr.gameObject.name != "SeasonCrossfade")
                        sr.color = new Color(sr.color.r * brightness, sr.color.g * brightness, sr.color.b * brightness, sr.color.a);
        }

        void LateUpdate()
        {
            if (cam == null) return;
            float left = cam.ViewportToWorldPoint(Vector3.zero).x - 4f;
            foreach (var t in pool)
                if (t.position.x < left) Place(t);
        }

        void Place(Transform t)
        {
            t.position = new Vector3(nextX, groundY + (yJitter > 0f ? Random.Range(-yJitter, yJitter) : 0f), 0f);
            float s = Random.Range(scaleRange.x, scaleRange.y);
            t.localScale = new Vector3(s, s, 1f);
            nextX += Random.Range(spacing.x, spacing.y);
        }
    }
}
