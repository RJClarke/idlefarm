using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Conforms pixel-art textures to the project convention:
// Sprite (single), Point filter, Clamp wrap, PPU 32, no mipmaps, uncompressed.
//
// Usage: select one or more folders (or textures) in the Project window, then
// Tools > Icon Packs > Conform Selected Pixel-Art Import Settings.
// Unity's default import is soft Bilinear / PPU 100 / compressed, so every new
// pack needs this pass.
public static class IconPackImportConformer
{
    const float TargetPPU = 32f;

    [MenuItem("Tools/Icon Packs/Conform Selected Pixel-Art Import Settings")]
    public static void ConformSelection()
    {
        var roots = Selection.objects
            .Select(AssetDatabase.GetAssetPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToArray();

        if (roots.Length == 0)
        {
            Debug.LogWarning("[IconPackImportConformer] Nothing selected. Select folder(s) or texture(s) in the Project window first.");
            return;
        }

        int changed = Conform(roots);
        Debug.Log($"[IconPackImportConformer] Conformed {changed} texture(s) from {roots.Length} selected item(s).");
    }

    // Conforms every Texture2D found under the given asset paths (folders or files).
    public static int Conform(IEnumerable<string> assetPaths)
    {
        var folders = assetPaths.Where(AssetDatabase.IsValidFolder).ToArray();
        var guids = new HashSet<string>();

        if (folders.Length > 0)
            foreach (var g in AssetDatabase.FindAssets("t:Texture2D", folders)) guids.Add(g);

        // Also accept directly-selected texture files.
        foreach (var p in assetPaths.Where(p => !AssetDatabase.IsValidFolder(p)))
        {
            var g = AssetDatabase.AssetPathToGUID(p);
            if (!string.IsNullOrEmpty(g) && AssetImporter.GetAtPath(p) is TextureImporter) guids.Add(g);
        }

        int changed = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter ti) continue;

                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = TargetPPU;
                ti.filterMode = FilterMode.Point;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed;

                EditorUtility.SetDirty(ti);
                ti.SaveAndReimport();
                changed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }
        return changed;
    }
}
