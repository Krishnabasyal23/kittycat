using System.Collections.Generic;
using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// Pool of scenery (trees, bushes) placed along the path ahead of the camera and recycled behind it.
    /// Put several prefabs in the list for variety. Their colours come from SeasonalSprite components.
    /// </summary>
    public class TreeSpawner : MonoBehaviour
    {
        public GameObject[] prefabs;
        public int poolSize = 16;
        public float groundY = -3f;
        public Vector2 spacing = new Vector2(2.5f, 7f);
        public Vector2 scaleRange = new Vector2(0.8f, 1.4f);

        readonly List<Transform> pool = new List<Transform>();
        float nextX;
        Camera cam;

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
                pool.Add(t);
                Place(t);
            }
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
            t.position = new Vector3(nextX, groundY, 0f);
            float s = Random.Range(scaleRange.x, scaleRange.y);
            t.localScale = new Vector3(s, s, 1f);
            nextX += Random.Range(spacing.x, spacing.y);
        }
    }
}
