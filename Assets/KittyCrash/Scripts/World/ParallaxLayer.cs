using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// Endless background layer (mountains, hills, clouds…) with parallax and seasonal sprites.
    /// Builds 3 side-by-side tiles at runtime; each tile has a SeasonalSprite so the art crossfades
    /// between seasons. The images must tile seamlessly left↔right.
    /// follow = how much the layer moves WITH the camera: 1 = glued to the screen (infinitely far),
    /// 0 = fixed in the world. Far mountains ≈ 0.9, hills ≈ 0.6.
    /// </summary>
    public class ParallaxLayer : MonoBehaviour
    {
        public Sprite spring, summer, autumn, winter;
        [Range(0f, 1f)] public float follow = 0.8f;
        [Tooltip("World Y of the bottom of the image (sprites use a bottom pivot).")]
        public float baselineY = -2.5f;
        [Tooltip("Extra sideways drift in units/second (clouds).")]
        public float drift = 0f;
        public int sortingOrder = -40;

        Transform[] tiles;
        float width;
        float driftOffset;
        Camera cam;

        void Start()
        {
            cam = Camera.main;
            var first = spring != null ? spring : summer != null ? summer : autumn != null ? autumn : winter;
            if (first == null) { enabled = false; return; }
            width = first.bounds.size.x;

            tiles = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Tile_" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = first;          // must be set before SeasonalSprite.Awake reads it
                sr.sortingOrder = sortingOrder;
                var ss = go.AddComponent<SeasonalSprite>();
                ss.spring = spring; ss.summer = summer; ss.autumn = autumn; ss.winter = winter;
                ss.tintRole = SeasonalSprite.TintRole.None;
                ss.shadeVariation = 0f;
                tiles[i] = go.transform;
            }
        }

        void LateUpdate()
        {
            if (tiles == null || cam == null || width <= 0f) return;
            driftOffset += drift * Time.deltaTime;

            float camX = cam.transform.position.x;
            float originX = camX * follow + driftOffset;
            int baseIndex = Mathf.FloorToInt((camX - originX) / width + 0.5f);
            for (int i = 0; i < 3; i++)
                tiles[i].position = new Vector3(originX + (baseIndex + i - 1) * width, baselineY, transform.position.z);
        }
    }
}
