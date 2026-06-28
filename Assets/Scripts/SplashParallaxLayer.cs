using UnityEngine;

/// <summary>
/// Auto-scrolls a single parallax layer on the splash/title screen and wraps it seamlessly.
///
/// The Craftpix background layers are authored 3× tiled horizontally, so shifting the layer by one
/// "tile" (spriteWidth / 3) and snapping back is invisible — the texture repeats exactly. Unlike the
/// pack's ParallaxEffect, this has NO dependency on a "Player" tag or camera movement, so it works on
/// a static title scene. Uses unscaled time so it animates regardless of Time.timeScale.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SplashParallaxLayer : MonoBehaviour
{
    [Tooltip("Scroll speed in world units per second. Set faster for nearer layers.")]
    public float speed = 1f;

    [Tooltip("Scroll direction: -1 = left, +1 = right. Keep consistent across layers for a unified wind.")]
    public float directionX = -1f;

    private float tileWidth;
    private float startX;

    private void Start()
    {
        var sr = GetComponent<SpriteRenderer>();
        // The art is tiled 3×; one seamless wrap unit is a third of the full sprite width.
        tileWidth = sr != null && sr.sprite != null ? sr.bounds.size.x / 3f : 0f;
        startX = transform.position.x;
    }

    private void Update()
    {
        if (tileWidth <= 0f) return;

        float dir = directionX < 0f ? -1f : 1f;
        transform.position += Vector3.right * (dir * speed * Time.unscaledDeltaTime);

        float dx = transform.position.x - startX;
        if (Mathf.Abs(dx) >= tileWidth)
            transform.position -= Vector3.right * (Mathf.Sign(dx) * tileWidth);
    }
}
