using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exports each sprite slice of a multi-sprite Texture2D as its own PNG.
/// Usage: select one or more Texture2D assets in the Project window, then
/// Tools → Export Sliced Sprites to PNGs. You'll be prompted to pick an
/// output folder anywhere on disk (Desktop, etc.) — files are written outside
/// the Unity project so the AssetDatabase isn't polluted.
///
/// Each texture gets its own subfolder named "<TextureName>_slices/".
/// File names use the Sprite's name (the value you see in the Sprite Editor).
/// </summary>
public static class SpriteSheetSliceExporter
{
    private const string LastOutputDirPref = "SpriteSheetSliceExporter.LastOutputDir";

    [MenuItem("Tools/Export Sliced Sprites to PNGs", priority = 200)]
    private static void ExportSelected()
    {
        Object[] selected = Selection.objects;
        if (selected == null || selected.Length == 0)
        {
            EditorUtility.DisplayDialog("Export Sliced Sprites",
                "Select one or more Texture2D assets in the Project window first.", "OK");
            return;
        }

        string defaultDir = EditorPrefs.GetString(LastOutputDirPref,
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop));
        string outputRoot = EditorUtility.OpenFolderPanel("Choose output folder for sprite slices", defaultDir, "");
        if (string.IsNullOrEmpty(outputRoot)) return; // user cancelled
        EditorPrefs.SetString(LastOutputDirPref, outputRoot);

        int textureCount = 0;
        int spriteCount = 0;

        foreach (Object obj in selected)
        {
            Texture2D tex = obj as Texture2D;
            if (tex == null) continue;

            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) continue;

            int exported = ExportTexture(tex, path, outputRoot);
            if (exported > 0)
            {
                textureCount++;
                spriteCount += exported;
            }
        }

        EditorUtility.DisplayDialog("Export Sliced Sprites",
            $"Exported {spriteCount} sprite(s) from {textureCount} texture(s) to:\n{outputRoot}", "OK");
    }

    /// <summary>
    /// Same flow as the menu item but for a single texture, callable from other tools.
    /// <paramref name="outputRoot"/> is an absolute path. Returns the number of sprites written.
    /// </summary>
    public static int ExportTexture(Texture2D tex, string assetPath, string outputRoot)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[SpriteSheetSliceExporter] No TextureImporter at '{assetPath}', skipping.");
            return 0;
        }

        // Load every sub-asset; sliced sprites appear as additional Sprite objects on the texture.
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        if (subAssets == null || subAssets.Length == 0)
        {
            Debug.LogWarning($"[SpriteSheetSliceExporter] No sub-assets found for '{assetPath}'.");
            return 0;
        }

        // Output folder: <outputRoot>/<texName>_slices/
        string texName = Path.GetFileNameWithoutExtension(assetPath);
        string outDir = Path.Combine(outputRoot, $"{texName}_slices");
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        // Need a readable texture. Temporarily enable Read/Write if it's off, then restore.
        bool restoreReadable = false;
        bool restoreCompression = false;
        TextureImporterCompression originalCompression = importer.textureCompression;
        if (!importer.isReadable)
        {
            importer.isReadable = true;
            restoreReadable = true;
        }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            restoreCompression = true;
        }
        if (restoreReadable || restoreCompression)
        {
            importer.SaveAndReimport();
        }

        // Reload the texture after reimport, since the old reference may now point at a stale GPU copy.
        tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (tex == null)
        {
            RestoreImporter(importer, restoreReadable, restoreCompression, originalCompression);
            return 0;
        }

        int written = 0;
        foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            Sprite sprite = sub as Sprite;
            if (sprite == null) continue;

            Texture2D slice = CropToTexture(tex, sprite.textureRect);
            if (slice == null) continue;

            string safeName = MakeSafeFileName(sprite.name);
            string outPath = Path.Combine(outDir, $"{safeName}.png");
            File.WriteAllBytes(outPath, slice.EncodeToPNG());
            Object.DestroyImmediate(slice);
            written++;
        }

        RestoreImporter(importer, restoreReadable, restoreCompression, originalCompression);

        Debug.Log($"[SpriteSheetSliceExporter] Wrote {written} sprite(s) to '{outDir}'.");
        return written;
    }

    private static Texture2D CropToTexture(Texture2D source, Rect rect)
    {
        int x = Mathf.RoundToInt(rect.x);
        int y = Mathf.RoundToInt(rect.y);
        int w = Mathf.RoundToInt(rect.width);
        int h = Mathf.RoundToInt(rect.height);
        if (w <= 0 || h <= 0) return null;
        if (x < 0 || y < 0 || x + w > source.width || y + h > source.height) return null;

        Color[] pixels = source.GetPixels(x, y, w, h);
        Texture2D slice = new Texture2D(w, h, TextureFormat.RGBA32, false);
        slice.SetPixels(pixels);
        slice.Apply();
        return slice;
    }

    private static void RestoreImporter(TextureImporter importer, bool restoreReadable, bool restoreCompression, TextureImporterCompression originalCompression)
    {
        bool changed = false;
        if (restoreReadable && importer.isReadable)
        {
            importer.isReadable = false;
            changed = true;
        }
        if (restoreCompression && importer.textureCompression != originalCompression)
        {
            importer.textureCompression = originalCompression;
            changed = true;
        }
        if (changed) importer.SaveAndReimport();
    }

    private static string MakeSafeFileName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "slice";
        char[] invalid = Path.GetInvalidFileNameChars();
        char[] cleaned = new char[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            cleaned[i] = System.Array.IndexOf(invalid, c) >= 0 ? '_' : c;
        }
        return new string(cleaned);
    }
}
