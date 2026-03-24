using UnityEngine;

/// <summary>
/// Generates a pixel-art dog sprite at runtime.
/// Placeholder until real dog animation assets are added.
/// Matches the 32 PPU pixel-art style of the project.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class DogVisual : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private int textureSize = 32;
    [SerializeField] private int sortingOrder = 12;

    private void Awake()
    {
        BuildDogSprite();
    }

    private void BuildDogSprite()
    {
        Texture2D tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        // Start transparent
        Color clear = new Color(0, 0, 0, 0);
        Color[] pixels = new Color[textureSize * textureSize];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        tex.SetPixels(pixels);

        // Colors
        Color body   = new Color32(180, 130, 70, 255);  // golden brown
        Color dark   = new Color32(140, 95, 45, 255);   // darker brown (ears, back)
        Color belly  = new Color32(220, 195, 155, 255); // light tan belly
        Color nose   = new Color32(40, 30, 20, 255);    // dark nose
        Color eye    = new Color32(30, 25, 20, 255);    // dark eyes
        Color tongue = new Color32(220, 100, 100, 255); // pink tongue

        // ── Legs (rows 0-4) ──
        // Front legs
        for (int y = 0; y <= 4; y++)
        {
            tex.SetPixel(9, y, body);
            tex.SetPixel(10, y, body);
            tex.SetPixel(13, y, body);
            tex.SetPixel(14, y, body);
        }
        // Back legs
        for (int y = 0; y <= 4; y++)
        {
            tex.SetPixel(20, y, body);
            tex.SetPixel(21, y, body);
            tex.SetPixel(24, y, body);
            tex.SetPixel(25, y, body);
        }

        // ── Body (rows 5-12) ──
        for (int y = 5; y <= 12; y++)
        {
            for (int x = 8; x <= 26; x++)
            {
                if (y <= 7)
                    tex.SetPixel(x, y, belly);
                else
                    tex.SetPixel(x, y, body);
            }
        }

        // Back stripe
        for (int x = 10; x <= 24; x++)
            tex.SetPixel(x, 12, dark);

        // ── Tail (rows 11-16, curving up from back) ──
        tex.SetPixel(27, 11, body);
        tex.SetPixel(28, 12, body);
        tex.SetPixel(28, 13, body);
        tex.SetPixel(29, 14, body);
        tex.SetPixel(29, 15, dark);
        tex.SetPixel(28, 16, dark);

        // ── Neck (rows 13-15) ──
        for (int y = 13; y <= 15; y++)
        {
            tex.SetPixel(7, y, body);
            tex.SetPixel(8, y, body);
            tex.SetPixel(9, y, body);
        }

        // ── Head (rows 13-19) ──
        for (int y = 13; y <= 19; y++)
        {
            for (int x = 3; x <= 8; x++)
            {
                tex.SetPixel(x, y, body);
            }
        }

        // Muzzle (rows 13-15, extending forward)
        for (int y = 13; y <= 15; y++)
        {
            tex.SetPixel(1, y, body);
            tex.SetPixel(2, y, body);
        }

        // Nose
        tex.SetPixel(1, 15, nose);
        tex.SetPixel(1, 14, nose);

        // Tongue
        tex.SetPixel(1, 13, tongue);
        tex.SetPixel(2, 13, tongue);

        // Eyes
        tex.SetPixel(4, 17, eye);

        // ── Ears (rows 19-22) ──
        tex.SetPixel(3, 20, dark);
        tex.SetPixel(3, 21, dark);
        tex.SetPixel(4, 21, dark);
        tex.SetPixel(7, 20, dark);
        tex.SetPixel(7, 21, dark);
        tex.SetPixel(8, 21, dark);

        tex.Apply();

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex,
            new Rect(0, 0, textureSize, textureSize),
            new Vector2(0.5f, 0f), // pivot at bottom-center
            textureSize);
        sr.sortingOrder = sortingOrder;
    }
}
