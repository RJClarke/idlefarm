using UnityEditor;
using UnityEngine;

// One-shot utility: conform imported Craftpix icon packs to the project's pixel-art
// convention (Sprite, Point filter, Clamp wrap, PPU 32, uncompressed). Run via menu.
public static class RavenIconImportFixer
{
    private static readonly string[] Folders =
    {
        "Assets/Sprites/UI/Icons/Raven",
        "Assets/Sprites/UI/Icons/Cute",
    };

    [MenuItem("Farm Game/Fix Imported Icon Packs")]
    public static void Fix()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", Folders);
        int changed = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.filterMode = FilterMode.Point;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.spritePixelsPerUnit = 32f;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
                changed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }
        Debug.Log($"[IconImportFixer] Conformed {changed} textures across {Folders.Length} root folders (Sprite/Point/Clamp/PPU32/Uncompressed).");
    }
}
