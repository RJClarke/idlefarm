using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Conforms newly-added building PNGs (dropped directly in Assets/Sprites/Buildings) to the
/// project's pixel-art convention: Sprite type, Point filter, Clamp wrap, 32 PPU, uncompressed,
/// no mipmaps. Leaves sprite slicing (Single vs Multiple) untouched. Run once after importing.
/// </summary>
public static class BuildingSpriteImport
{
    // Only the newly-added building PNGs — pre-existing buildings keep their tuned settings.
    private static readonly string[] NewFiles =
    {
        "22_Post_Office_32x32_Red_Mailbox_1_Side_1.png",
        "ME_Singles_Garden_32x32_Blue_Brown_Wood_Storage.png",
        "ME_Singles_Garden_32x32_Blue_White_Wood_Storage.png",
        "ME_Singles_Garden_32x32_Brown_Wood_Storage.png",
        "ME_Singles_Generic_Building_32x32_Hardware_Store.png",
        "ME_Singles_Generic_Building_32x32_Hardware_Store_Example.png",
        "ME_Singles_Shopping_Center_and_Markets_32x32_Market_Medium_5.png",
        "ME_Singles_Shopping_Center_and_Markets_32x32_Market_Small_11.png",
        "ME_Singles_Shopping_Center_and_Markets_32x32_Market_Small_3.png",
        "Wind_Mill_32x32.png",
    };

    [MenuItem("Tools/UI/Conform New Building Sprites (Point, 32 PPU)")]
    private static void Conform()
    {
        const string dir = "Assets/Sprites/Buildings";
        int n = 0;
        foreach (string file in NewFiles)
        {
            string path = dir + "/" + file;
            if (AssetImporter.GetAtPath(path) is not TextureImporter imp) continue;

            imp.textureType = TextureImporterType.Sprite;
            imp.filterMode = FilterMode.Point;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.spritePixelsPerUnit = 32;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            n++;
        }
        Debug.Log($"[BuildingImport] Conformed {n} building sprite(s) to Point/Clamp/32 PPU/uncompressed.");
    }
}
