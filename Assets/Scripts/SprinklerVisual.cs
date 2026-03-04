using UnityEngine;

/// <summary>
/// Generates a pixel-art sprinkler sprite at runtime.
/// Circular sprinkler device, pivot at center-bottom.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SprinklerVisual : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private int textureSize = 32;
    [SerializeField] private int sortingOrder = 10;

    private void Awake()
    {
        BuildSprinklerSprite();
    }

    private void BuildSprinklerSprite()
    {
        Texture2D tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        // Start transparent
        Color clear = new Color(0, 0, 0, 0);
        Color[] pixels = new Color[textureSize * textureSize];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        tex.SetPixels(pixels);

        Color pipe    = new Color32(100, 100, 110, 255); // grey metal
        Color pipeHi  = new Color32(140, 140, 155, 255); // metal highlight
        Color base1   = new Color32(80, 80, 90, 255);    // dark base
        Color base2   = new Color32(60, 60, 70, 255);    // darker base
        Color nozzle  = new Color32(50, 130, 200, 255);  // blue nozzle tip
        Color water   = new Color32(100, 180, 240, 180); // water droplets (semi-transparent)

        int cx = textureSize / 2; // center x = 16

        // ── Base plate (rows 0-3) ──
        for (int x = cx - 5; x <= cx + 4; x++)
        {
            tex.SetPixel(x, 0, base2);
            tex.SetPixel(x, 1, base1);
        }
        for (int x = cx - 4; x <= cx + 3; x++)
        {
            tex.SetPixel(x, 2, base1);
            tex.SetPixel(x, 3, base1);
        }

        // ── Vertical pipe (rows 4-14) ──
        for (int y = 4; y <= 14; y++)
        {
            tex.SetPixel(cx - 1, y, pipe);
            tex.SetPixel(cx, y, pipeHi);
            tex.SetPixel(cx + 1, y, pipe);
        }

        // ── Nozzle head (rows 15-18) ──
        for (int x = cx - 3; x <= cx + 2; x++)
        {
            tex.SetPixel(x, 15, pipe);
            tex.SetPixel(x, 16, pipeHi);
        }
        for (int x = cx - 2; x <= cx + 1; x++)
        {
            tex.SetPixel(x, 17, nozzle);
            tex.SetPixel(x, 18, nozzle);
        }

        // ── Water spray (rows 19-26) — fan shape ──
        // Left spray
        tex.SetPixel(cx - 4, 20, water); tex.SetPixel(cx - 5, 22, water);
        tex.SetPixel(cx - 6, 24, water); tex.SetPixel(cx - 3, 21, water);
        tex.SetPixel(cx - 7, 26, water);
        // Center spray
        tex.SetPixel(cx - 1, 20, water); tex.SetPixel(cx, 22, water);
        tex.SetPixel(cx, 24, water);     tex.SetPixel(cx + 1, 21, water);
        tex.SetPixel(cx - 1, 26, water); tex.SetPixel(cx + 1, 25, water);
        // Right spray
        tex.SetPixel(cx + 3, 20, water); tex.SetPixel(cx + 4, 22, water);
        tex.SetPixel(cx + 5, 24, water); tex.SetPixel(cx + 2, 21, water);
        tex.SetPixel(cx + 6, 26, water);

        tex.Apply();

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex,
            new Rect(0, 0, textureSize, textureSize),
            new Vector2(0.5f, 0f), // pivot at bottom-center
            textureSize);
        sr.sortingOrder = sortingOrder;
    }
}
