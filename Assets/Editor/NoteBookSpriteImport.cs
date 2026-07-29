using UnityEditor;
using UnityEngine;

/// <summary>
/// Conforms the NoteBook UI pack PNGs to the project's pixel-art convention: Sprite type, Point
/// filter, Clamp wrap, 32 PPU, uncompressed, no mipmaps — same settings as BuildingSpriteImport.
///
/// Unlike that one this runs automatically, because these sprites are 9-sliced into UITK panels:
/// a bilinear-filtered pack sprite renders as a blurred smear at panel scale, and the failure is
/// subtle enough to be mistaken for the art being low quality rather than mis-imported.
/// OnPreprocessTexture catches anything dropped in later; the load-time pass fixes files already
/// imported with Unity's defaults and is a no-op once they conform.
/// </summary>
public class NoteBookSpriteImport : AssetPostprocessor
{
    private const string Dir = "Assets/Sprites/UI/NoteBook";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Dir)) return;
        Apply((TextureImporter)assetImporter);
    }

    private static void Apply(TextureImporter imp)
    {
        imp.textureType = TextureImporterType.Sprite;
        imp.filterMode = FilterMode.Point;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.spritePixelsPerUnit = 32;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
    }

    /// <summary>True when the importer already matches the convention, so the pass below can skip
    /// it — otherwise every domain reload would trigger a pointless reimport of the whole folder.</summary>
    private static bool Conforms(TextureImporter imp)
        => imp.textureType == TextureImporterType.Sprite
        && imp.filterMode == FilterMode.Point
        && imp.wrapMode == TextureWrapMode.Clamp
        && Mathf.Approximately(imp.spritePixelsPerUnit, 32f)
        && imp.textureCompression == TextureImporterCompression.Uncompressed
        && !imp.mipmapEnabled;

    [InitializeOnLoadMethod]
    private static void ConformExisting()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Dir });
        int fixedCount = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is not TextureImporter imp) continue;
            if (Conforms(imp)) continue;
            Apply(imp);
            imp.SaveAndReimport();
            fixedCount++;
        }
        if (fixedCount > 0)
            Debug.Log($"[NoteBookImport] Conformed {fixedCount} sprite(s) to Point/Clamp/32 PPU/uncompressed.");
    }
}
