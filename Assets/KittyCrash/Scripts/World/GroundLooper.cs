using UnityEngine;

namespace KittyCrash
{
    /// <summary>Endless flat ground: leapfrogs segments from behind the camera to in front of it.</summary>
    public class GroundLooper : MonoBehaviour
    {
        public Transform[] segments;
        public float segmentWidth = 30f;

        Camera cam;

        void Start() => cam = Camera.main;

        void LateUpdate()
        {
            if (cam == null || segments == null || segments.Length == 0) return;
            float left = cam.ViewportToWorldPoint(Vector3.zero).x;

            foreach (var s in segments)
            {
                if (s != null && s.position.x + segmentWidth * 0.5f < left - 1f)
                    s.position += Vector3.right * segmentWidth * segments.Length;
            }
        }
    }
}
