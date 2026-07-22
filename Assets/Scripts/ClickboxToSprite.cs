using UnityEngine;

/// <summary>
/// Locks a BoxCollider2D to its SpriteRenderer's visible sprite, so the tap target always matches
/// the art — no matter what Transform scale the object is drawn at.
///
/// Why this exists: a BoxCollider2D's Size is authored in the object's LOCAL space and is then
/// multiplied by the Transform's Scale. A building drawn at Scale 4 with a hand-typed Size of
/// 15×15 ends up with a 60×60 world-unit tap box — even when its sprite is barely 1×2 units. Scale
/// the object up to make it look bigger and the tap box inflates right along with it (this is what
/// let the Lake's clickbox "swallow" the screen). This component removes the guesswork: it sets the
/// collider's Size to the sprite's bounds and its Offset to the sprite's center whenever the sprite
/// or transform changes, in the editor and at runtime.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class ClickboxToSprite : MonoBehaviour
{
    [Tooltip("Scales the tap box relative to the sprite. 1 = exactly the sprite rect; lower it (e.g. " +
             "0.85) to tuck the tap box inside transparent padding. X = width, Y = height.")]
    [SerializeField] private Vector2 fit = Vector2.one;

    private void Awake() => Apply();
    private void OnEnable() => Apply();
    private void OnValidate() => Apply();

    /// <summary>Resize the BoxCollider2D to hug the current sprite. Safe to call any time.</summary>
    public void Apply()
    {
        var sr = GetComponent<SpriteRenderer>();
        var box = GetComponent<BoxCollider2D>();
        if (sr == null || box == null || sr.sprite == null) return;

        // sprite.bounds is in local space, BEFORE the Transform scale — exactly what the collider
        // Size wants, since the collider is scaled by the Transform the same way the sprite is.
        Bounds b = sr.sprite.bounds;
        box.size = new Vector2(b.size.x * fit.x, b.size.y * fit.y);
        box.offset = b.center; // respects off-center pivots (e.g. bottom-left art)
    }
}
