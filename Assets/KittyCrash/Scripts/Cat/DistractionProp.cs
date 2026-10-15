using UnityEngine;

namespace KittyCrash
{
    /// <summary>Server sends "fish", "sleep", "yarn" (and maybe later "butterfly", "laser", "cardboard_box").</summary>
    public enum DistractionType { Fish, Sleep, Yarn, Butterfly, Laser, CardboardBox }

    public static class Distractions
    {
        public static DistractionType FromServer(string id)
        {
            switch (id)
            {
                case "fish": return DistractionType.Fish;
                case "sleep": return DistractionType.Sleep;
                case "yarn": return DistractionType.Yarn;
                case "butterfly": return DistractionType.Butterfly;
                case "laser": return DistractionType.Laser;
                case "cardboard_box": return DistractionType.CardboardBox;
                default: return (DistractionType)Random.Range(0, 3); // unknown → one of the live three
            }
        }

        public static string Pretty(DistractionType t)
        {
            switch (t)
            {
                case DistractionType.Fish: return "a tasty fish";
                case DistractionType.Sleep: return "a sudden nap";
                case DistractionType.Yarn: return "a ball of yarn";
                case DistractionType.Butterfly: return "a butterfly";
                case DistractionType.Laser: return "a laser dot";
                default: return "a cardboard box";
            }
        }

        public static CatSpriteAnimator.Clip Pose(DistractionType t)
        {
            switch (t)
            {
                case DistractionType.Sleep: return CatSpriteAnimator.Clip.Sleep;
                case DistractionType.Fish:
                case DistractionType.Yarn:
                case DistractionType.Laser: return CatSpriteAnimator.Clip.Pounce;
                default: return CatSpriteAnimator.Clip.Sit;
            }
        }
    }

    /// <summary>
    /// The thing that distracted kitty. Placeholder shapes with simple motion; put a real sprite in
    /// the matching slot on CatWalker (Distraction Sprites) and it's used instead.
    /// </summary>
    public class DistractionProp : MonoBehaviour
    {
        DistractionType type;
        Vector3 anchor;
        float t;
        SpriteRenderer sr;
        Vector3 baseScale;

        public void Init(DistractionType kind, Sprite sprite, bool isPlaceholder, Transform cat)
        {
            type = kind;
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 11;

            Vector3 c = cat.position;
            switch (type)
            {
                case DistractionType.Fish:
                    anchor = c + new Vector3(1.1f, 0.15f, 0f);
                    Set(new Color(0.45f, 0.65f, 0.85f), new Vector3(0.5f, 0.22f, 1f), isPlaceholder);
                    break;
                case DistractionType.Sleep:
                    anchor = c + new Vector3(0.35f, 0.9f, 0f);
                    Set(Color.white, Vector3.one * 0.2f, isPlaceholder);
                    break;
                case DistractionType.Yarn:
                    anchor = c + new Vector3(0.7f, 0.18f, 0f);
                    Set(new Color(0.95f, 0.3f, 0.4f), Vector3.one * 0.35f, isPlaceholder);
                    break;
                case DistractionType.Butterfly:
                    anchor = c + new Vector3(1.0f, 1.5f, 0f);
                    Set(new Color(0.65f, 0.45f, 1f), Vector3.one * 0.3f, isPlaceholder);
                    break;
                case DistractionType.Laser:
                    anchor = c + new Vector3(1.4f, 0.04f, 0f);
                    Set(new Color(1f, 0.1f, 0.1f), Vector3.one * 0.1f, isPlaceholder);
                    break;
                default:
                    anchor = c + new Vector3(1.2f, 0.35f, 0f);
                    Set(new Color(0.72f, 0.55f, 0.35f), new Vector3(0.8f, 0.7f, 1f), isPlaceholder);
                    break;
            }
            transform.position = anchor;
        }

        void Set(Color placeholderColor, Vector3 placeholderScale, bool isPlaceholder)
        {
            sr.color = isPlaceholder ? placeholderColor : Color.white;
            baseScale = isPlaceholder ? placeholderScale : Vector3.one;
            transform.localScale = baseScale;
        }

        void Update()
        {
            t += Time.deltaTime;
            switch (type)
            {
                case DistractionType.Fish:        // flops on the ground
                    transform.position = anchor + Vector3.up * Mathf.Abs(Mathf.Sin(t * 7f)) * 0.12f;
                    transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 9f) * 20f);
                    break;
                case DistractionType.Sleep:       // Z bubbles drift up and fade
                    float c = t % 1.6f;
                    transform.position = anchor + new Vector3(c * 0.3f, c * 0.8f, 0f);
                    var col = sr.color; col.a = 1f - c / 1.6f; sr.color = col;
                    break;
                case DistractionType.Yarn:        // rolls ahead and slows down
                    float k = Mathf.Exp(-t * 1.5f);
                    transform.position = anchor + Vector3.right * 1.5f * (1f - k);
                    transform.Rotate(0f, 0f, -400f * Time.deltaTime * k);
                    break;
                case DistractionType.Butterfly:   // flutters in a loop
                    transform.position = anchor + new Vector3(Mathf.Sin(t * 1.7f) * 0.7f, Mathf.Sin(t * 3.3f) * 0.35f, 0f);
                    transform.localScale = new Vector3(baseScale.x * (Mathf.Abs(Mathf.Sin(t * 14f)) * 0.8f + 0.2f), baseScale.y, 1f);
                    break;
                case DistractionType.Laser:       // zips around in front of the cat
                    transform.position = anchor + new Vector3(Mathf.Sin(t * 3.1f) * 0.9f + Mathf.Sin(t * 7.3f) * 0.3f, 0f, 0f);
                    break;
                default:                          // box just sits there, wobbling a bit
                    transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2f) * 2f);
                    break;
            }
        }
    }
}
