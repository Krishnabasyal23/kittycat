using UnityEngine;

namespace KittyCrash
{
    /// <summary>Follows the cat horizontally (flat pitch, so Y stays fixed).</summary>
    [DefaultExecutionOrder(-10)]
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        [Tooltip("Keep the cat this far left of screen centre so you see what's ahead.")]
        public float offsetX = 3f;
        public float smoothing = 4f;

        void LateUpdate()
        {
            if (target == null) return;
            var p = transform.position;
            float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            p.x = Mathf.Lerp(p.x, target.position.x + offsetX, k);
            transform.position = p;
        }
    }
}
