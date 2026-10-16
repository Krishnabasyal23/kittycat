using UnityEngine;

namespace KittyCrash
{
    /// <summary>Server sends "fish", "sleep", "yarn" (and maybe later "butterfly", "laser", "cardboard_box").</summary>
    // New values are appended so existing scenes keep their sprite slots.
    public enum DistractionType { Fish, Sleep, Yarn, Butterfly, Laser, CardboardBox, Leaf, Snowflake, Bird, TreeClimb }

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
                case "leaf": return DistractionType.Leaf;
                case "snowflake": return DistractionType.Snowflake;
                case "bird": return DistractionType.Bird;
                case "tree": return DistractionType.TreeClimb;
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
                case DistractionType.Leaf: return "a falling leaf";
                case DistractionType.Snowflake: return "a sparkly snowflake";
                case DistractionType.Bird: return "a little bird";
                case DistractionType.TreeClimb: return "a tree to climb";
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
                case DistractionType.Laser:
                case DistractionType.Leaf:
                case DistractionType.Snowflake: return CatSpriteAnimator.Clip.Pounce;
                case DistractionType.TreeClimb: return CatSpriteAnimator.Clip.Walk;
                default: return CatSpriteAnimator.Clip.Sit;
            }
        }
    }

    
    public class DistractionProp : MonoBehaviour
    {
        DistractionType type;
        Vector3 anchor;
        float t;
        SpriteRenderer sr;
        Vector3 baseScale;
        float size = 1f;

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
                case DistractionType.Leaf:
                    anchor = c + new Vector3(1.0f, 1.6f, 0f);
                    Set(new Color(0.95f, 0.55f, 0.2f), Vector3.one * 0.25f, isPlaceholder);
                    break;
                case DistractionType.Snowflake:
                    anchor = c + new Vector3(0.9f, 1.8f, 0f);
                    Set(Color.white, Vector3.one * 0.25f, isPlaceholder);
                    break;
                case DistractionType.Bird:
                    anchor = c + new Vector3(1.5f, 0.2f, 0f);
                    Set(new Color(0.35f, 0.55f, 0.85f), Vector3.one * 0.3f, isPlaceholder);
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
            baseScale = isPlaceholder ? placeholderScale : Vector3.one * ArtSize(type);
            transform.localScale = baseScale;
        }

        static float ArtSize(DistractionType t)
        {
            switch (t)
            {
                case DistractionType.Fish: return 0.5f;
                default: return 1f;
            }
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
                case DistractionType.Leaf:        // flutters down in a zig-zag, then rests on the ground
                {
                    float groundY = anchor.y - 1.55f;
                    float posY = Mathf.Max(groundY, anchor.y - t * 0.55f);
                    float landed = posY > groundY ? 1f : 0f;
                    transform.position = new Vector3(anchor.x + Mathf.Sin(t * 3f) * 0.35f * landed, posY, 0f);
                    transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 3f) * 35f * landed);
                    break;
                }
                case DistractionType.Snowflake:   // drifts down slowly, sparkling
                    transform.position = anchor + new Vector3(Mathf.Sin(t * 1.5f) * 0.3f, -Mathf.Min(t * 0.35f, 1.2f), 0f);
                    transform.Rotate(0f, 0f, 60f * Time.deltaTime);
                    transform.localScale = baseScale * (1f + Mathf.Sin(t * 6f) * 0.08f);
                    break;
                case DistractionType.Bird:        // hops on the ground, then flies off
                    if (t < 2.2f) transform.position = anchor + new Vector3(Mathf.Floor(t * 2f) * 0.12f, Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)) * 0.12f, 0f);
                    else transform.position = anchor + new Vector3(0.5f + (t - 2.2f) * 2.5f, (t - 2.2f) * 1.8f, 0f);
                    break;
                default:                          // box just sits there, wobbling a bit
                    transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2f) * 2f);
                    break;
            }
        }
    }
}
