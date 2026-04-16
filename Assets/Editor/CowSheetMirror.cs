using System.IO;
using UnityEditor;
using UnityEngine;

// Mirrors the already-sliced Cow_32x32 rect layout + naming pattern to every other
// Cow_*.png in the same folder (Cow_Baby_32x32, Cow_Big_Black_32x32, etc.).
// Run AFTER CowSheetRenamer has been applied to the template sheet.
public static class CowSheetMirror
{
    private const string TEMPLATE_PATH   = "Assets/Sprites/Animals/Cows_32x32/Cow_32x32.png";
    private const string COWS_FOLDER     = "Assets/Sprites/Animals/Cows_32x32";

    [MenuItem("Tools/IdleFarm/Mirror Cow Sheet Slicing")]
    public static void Mirror()
    {
        // ── Load template ────────────────────────────────────────────────────
        TextureImporter template = AssetImporter.GetAtPath(TEMPLATE_PATH) as TextureImporter;
        if (template == null)
        {
            Debug.LogError($"CowSheetMirror: template importer not found at {TEMPLATE_PATH}");
            return;
        }

        SpriteMetaData[] templateSprites = template.spritesheet;
        if (templateSprites == null || templateSprites.Length == 0)
        {
            Debug.LogError("CowSheetMirror: template has no sprite metadata — run Rename Cow Sheet first.");
            return;
        }

        // ── Find target sheets ───────────────────────────────────────────────
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { COWS_FOLDER });
        int sheetsProcessed = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // Skip the template, baby cows (different sheet size), and any non-png
            if (path == TEMPLATE_PATH) continue;
            if (!path.EndsWith(".png")) continue;
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (fileName.StartsWith("Cow_Baby_")) continue;

            string baseName = fileName; // e.g. "Cow_Caramel_32x32"

            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null)
            {
                Debug.LogWarning($"CowSheetMirror: no TextureImporter for {path}, skipping.");
                continue;
            }

            // Build new metadata: same rects, names substituted to this sheet's base name
            SpriteMetaData[] newSprites = new SpriteMetaData[templateSprites.Length];
            for (int i = 0; i < templateSprites.Length; i++)
            {
                SpriteMetaData src = templateSprites[i];
                // Replace "Cow_32x32" prefix with this sheet's base name, keep the suffix
                string suffix = src.name.Substring("Cow_32x32".Length); // e.g. "_IdleR1", "_PreviewU"
                SpriteMetaData md = src;
                md.name = baseName + suffix;
                newSprites[i] = md;
            }

            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.spritesheet = newSprites;
            EditorUtility.SetDirty(ti);
            ti.SaveAndReimport();
            sheetsProcessed++;

            Debug.Log($"CowSheetMirror: applied {newSprites.Length} slices to {path}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"CowSheetMirror: done — {sheetsProcessed} sheets updated.");
    }
}
