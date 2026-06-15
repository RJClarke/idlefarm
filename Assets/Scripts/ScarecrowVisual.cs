using UnityEngine;

/// <summary>
/// Generates a pixel-art scarecrow sprite at runtime.
/// Attach to a GameObject with a SpriteRenderer.
/// Matches the 32 PPU pixel-art style of the project.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ScarecrowVisual : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private Sprite spriteOverride;
    [SerializeField] private int textureSize = 32;
    [SerializeField] private int sortingOrder = 10;

    private void Awake()
    {
        if (spriteOverride != null)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            sr.sprite = spriteOverride;
            sr.sortingOrder = sortingOrder;
        }
        else
        {
            BuildScarecrowSprite();
        }

        YSort.Ensure(gameObject, isStatic: true);
    }

    private void BuildScarecrowSprite()
    {
        Texture2D tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        // Start transparent
        Color clear = new Color(0, 0, 0, 0);
        Color[] pixels = new Color[textureSize * textureSize];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        tex.SetPixels(pixels);

        // Colors
        Color pole    = new Color32(101, 67, 33, 255);   // dark brown pole
        Color shirt1  = new Color32(200, 100, 50, 255);  // orange plaid
        Color shirt2  = new Color32(220, 120, 60, 255);  // light orange plaid
        Color burlap1 = new Color32(210, 180, 140, 255); // burlap edge
        Color burlap2 = new Color32(222, 195, 160, 255); // burlap center
        Color hat1    = new Color32(80, 50, 20, 255);    // dark brown hat
        Color hat2    = new Color32(100, 60, 25, 255);   // hat highlight
        Color eye     = new Color32(40, 40, 40, 255);    // black eyes
        Color hand    = new Color32(210, 180, 140, 255); // burlap "glove"

        // ── Pole (rows 0-9, columns 15-16) ──
        for (int y = 0; y <= 9; y++)
        {
            tex.SetPixel(15, y, pole);
            tex.SetPixel(16, y, pole);
        }

        // ── Body / Shirt (rows 10-18, columns 13-18), plaid pattern ──
        for (int y = 10; y <= 18; y++)
        {
            for (int x = 13; x <= 18; x++)
            {
                tex.SetPixel(x, y, ((x + y) % 2 == 0) ? shirt1 : shirt2);
            }
        }

        // ── Arms (rows 16-17, columns 7-24) — horizontal cross bar ──
        for (int y = 16; y <= 17; y++)
        {
            for (int x = 7; x <= 24; x++)
            {
                tex.SetPixel(x, y, ((x + y) % 2 == 0) ? shirt1 : shirt2);
            }
        }

        // ── Hands (burlap "gloves" at arm tips) ──
        tex.SetPixel(6, 15, hand); tex.SetPixel(7, 15, hand);
        tex.SetPixel(6, 18, hand); tex.SetPixel(7, 18, hand);
        tex.SetPixel(24, 15, hand); tex.SetPixel(25, 15, hand);
        tex.SetPixel(24, 18, hand); tex.SetPixel(25, 18, hand);

        // ── Head (rows 19-23, burlap sack) ──
        // Row 19: narrow top of sack
        tex.SetPixel(14, 19, burlap1); tex.SetPixel(15, 19, burlap2);
        tex.SetPixel(16, 19, burlap2); tex.SetPixel(17, 19, burlap1);
        // Rows 20-22: full face
        for (int y = 20; y <= 22; y++)
        {
            tex.SetPixel(13, y, burlap1);
            tex.SetPixel(14, y, burlap2);
            tex.SetPixel(15, y, burlap2);
            tex.SetPixel(16, y, burlap2);
            tex.SetPixel(17, y, burlap2);
            tex.SetPixel(18, y, burlap1);
        }
        // Eyes (row 21)
        tex.SetPixel(15, 21, eye);
        tex.SetPixel(17, 21, eye);
        // Row 23: narrow bottom of face
        tex.SetPixel(14, 23, burlap1); tex.SetPixel(15, 23, burlap2);
        tex.SetPixel(16, 23, burlap2); tex.SetPixel(17, 23, burlap1);

        // ── Hat (rows 24-28) ──
        // Brim (row 24): wide
        for (int x = 11; x <= 20; x++)
            tex.SetPixel(x, 24, (x % 2 == 0) ? hat1 : hat2);
        // Band (row 25): same width
        for (int x = 11; x <= 20; x++)
            tex.SetPixel(x, 25, (x % 2 == 0) ? hat2 : hat1);
        // Taper (rows 26-28)
        for (int x = 12; x <= 19; x++)
            tex.SetPixel(x, 26, (x % 2 == 0) ? hat1 : hat2);
        for (int x = 13; x <= 18; x++)
            tex.SetPixel(x, 27, (x % 2 == 0) ? hat2 : hat1);
        for (int x = 14; x <= 17; x++)
            tex.SetPixel(x, 28, hat1);

        tex.Apply();

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex,
            new Rect(0, 0, textureSize, textureSize),
            new Vector2(0.5f, 0f), // pivot at bottom-center (feet on ground)
            textureSize);
        sr.sortingOrder = sortingOrder;
    }
}
