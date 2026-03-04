using UnityEngine;

/// <summary>
/// Generates a pixel-art fence sprite at runtime.
/// Draws an L-shaped fence along the zone's exposed edges:
///   Zone 1 (top-left): bottom + right edges
///   Zone 2 (top-right): bottom + left edges
///   Zone 3 (bottom-left): top + right edges
///   Zone 4 (bottom-right): top + left edges
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class FenceVisual : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private int textureSize = 32;
    [SerializeField] private int sortingOrder = 9;

    [Header("Zone Configuration")]
    [Tooltip("Set by EquipmentManager at spawn time")]
    public int zoneId = 1;

    private void Start()
    {
        BuildFenceSprite();
    }

    private void BuildFenceSprite()
    {
        Texture2D tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        // Start transparent
        Color clear = new Color(0, 0, 0, 0);
        Color[] pixels = new Color[textureSize * textureSize];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        tex.SetPixels(pixels);

        Color post  = new Color32(120, 80, 40, 255);  // dark brown posts
        Color rail  = new Color32(180, 140, 90, 255);  // light brown rails
        Color cap   = new Color32(100, 65, 30, 255);   // post caps

        // Determine which two edges to draw based on zone
        bool drawTop    = (zoneId == 3 || zoneId == 4);
        bool drawBottom = (zoneId == 1 || zoneId == 2);
        bool drawLeft   = (zoneId == 2 || zoneId == 4);
        bool drawRight  = (zoneId == 1 || zoneId == 3);

        int s = textureSize;

        // Draw horizontal edge (top or bottom)
        if (drawTop)
            DrawHorizontalFence(tex, s - 4, s); // rows near top
        if (drawBottom)
            DrawHorizontalFence(tex, 0, s);      // rows near bottom

        // Draw vertical edge (left or right)
        if (drawLeft)
            DrawVerticalFence(tex, 0, s);        // columns near left
        if (drawRight)
            DrawVerticalFence(tex, s - 4, s);    // columns near right

        // Corner post at the L-junction
        int cx = drawRight ? s - 4 : 0;
        int cy = drawTop ? s - 4 : 0;
        for (int dx = 0; dx < 4; dx++)
            for (int dy = 0; dy < 4; dy++)
                tex.SetPixel(cx + dx, cy + dy, post);

        tex.Apply();

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex,
            new Rect(0, 0, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize);
        sr.sortingOrder = sortingOrder;
    }

    private void DrawHorizontalFence(Texture2D tex, int startY, int size)
    {
        Color post = new Color32(120, 80, 40, 255);
        Color rail = new Color32(180, 140, 90, 255);

        // Posts every 8 pixels
        for (int x = 0; x < size; x += 8)
        {
            for (int dy = 0; dy < 4; dy++)
            {
                tex.SetPixel(x, startY + dy, post);
                tex.SetPixel(x + 1, startY + dy, post);
            }
        }

        // Rails (two horizontal bars)
        for (int x = 0; x < size; x++)
        {
            tex.SetPixel(x, startY + 1, rail);
            tex.SetPixel(x, startY + 3, rail);
        }
    }

    private void DrawVerticalFence(Texture2D tex, int startX, int size)
    {
        Color post = new Color32(120, 80, 40, 255);
        Color rail = new Color32(180, 140, 90, 255);

        // Posts every 8 pixels
        for (int y = 0; y < size; y += 8)
        {
            for (int dx = 0; dx < 4; dx++)
            {
                tex.SetPixel(startX + dx, y, post);
                tex.SetPixel(startX + dx, y + 1, post);
            }
        }

        // Rails (two vertical bars)
        for (int y = 0; y < size; y++)
        {
            tex.SetPixel(startX + 1, y, rail);
            tex.SetPixel(startX + 3, y, rail);
        }
    }
}
