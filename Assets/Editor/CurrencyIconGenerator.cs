using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot editor utility to bake procedural placeholder icons for the top-bar UI.
/// Writes Gem.png + Compost.png to Assets/Sprites/UI/Icons/. Re-running overwrites.
/// </summary>
public static class CurrencyIconGenerator
{
    private const string OutDir = "Assets/Sprites/UI/Icons";
    private const int Size = 32;

    [MenuItem("Farm Game/UI/Generate Currency Icons (Gem + Compost)")]
    public static void Generate()
    {
        Directory.CreateDirectory(OutDir);
        Write("Gem.png", BuildGem());
        Write("Compost.png", BuildCompost());
        AssetDatabase.Refresh();
        ConfigureSprite($"{OutDir}/Gem.png");
        ConfigureSprite($"{OutDir}/Compost.png");
        Debug.Log("[CurrencyIconGenerator] Generated Gem.png + Compost.png in " + OutDir);
    }

    // ─── Gem (purple diamond, matches AnimalVisual gem palette) ───
    private static Texture2D BuildGem()
    {
        Color body      = new Color(0.659f, 0.333f, 0.969f);   // #A855F7
        Color highlight = new Color(0.82f,  0.62f,  1.00f);
        Color outline   = new Color(0.32f,  0.12f,  0.58f);
        Color clear     = Color.clear;

        var tex = NewTex();
        var px  = NewBuf();

        // Diamond: two stacked triangles with subtle facet highlight on the upper-left.
        float cx = (Size - 1) / 2f;
        float cy = (Size - 1) / 2f;
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float dx = Mathf.Abs((x - cx) / (Size * 0.42f));
            float dy = Mathf.Abs((y - cy) / (Size * 0.48f));
            float d = dx + dy;
            if (d > 1f) continue;
            bool edge = d > 0.92f;
            bool hi   = x < cx && y > cy && d < 0.78f;
            px[y * Size + x] = edge ? outline : (hi ? highlight : body);
        }

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // ─── Compost (green lump/leaves) ───
    private static Texture2D BuildCompost()
    {
        Color body      = new Color(0.439f, 0.788f, 0.392f);   // #70C964 — matches FloatingText compost color
        Color shade     = new Color(0.30f,  0.58f,  0.27f);
        Color highlight = new Color(0.65f,  0.92f,  0.55f);
        Color outline   = new Color(0.18f,  0.35f,  0.16f);

        var tex = NewTex();
        var px  = NewBuf();

        // Three overlapping blobs to suggest a compost pile.
        Vector2[] centers = { new Vector2(11, 12), new Vector2(20, 11), new Vector2(15, 19) };
        float[] radii   = { 7.5f, 7f, 8f };

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float bestDist = float.MaxValue;
            int blob = -1;
            for (int i = 0; i < centers.Length; i++)
            {
                float d = Vector2.Distance(new Vector2(x, y), centers[i]) / radii[i];
                if (d < bestDist) { bestDist = d; blob = i; }
            }
            if (bestDist > 1f) continue;

            bool edge = bestDist > 0.88f;
            bool lit  = bestDist < 0.45f && (x + y) % 2 == 0;
            bool dim  = blob == 2 && bestDist > 0.55f;
            px[y * Size + x] = edge ? outline : (lit ? highlight : (dim ? shade : body));
        }

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static Texture2D NewTex()
    {
        var t = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        return t;
    }

    private static Color[] NewBuf()
    {
        var buf = new Color[Size * Size];
        for (int i = 0; i < buf.Length; i++) buf[i] = Color.clear;
        return buf;
    }

    private static void Write(string fileName, Texture2D tex)
    {
        File.WriteAllBytes($"{OutDir}/{fileName}", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    private static void ConfigureSprite(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.textureType         = TextureImporterType.Sprite;
        imp.spriteImportMode    = SpriteImportMode.Single;
        imp.filterMode          = FilterMode.Point;
        imp.spritePixelsPerUnit = 32;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled       = false;
        imp.SaveAndReimport();
    }
}
