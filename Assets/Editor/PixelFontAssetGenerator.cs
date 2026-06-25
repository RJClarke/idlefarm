using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TextCoreFontAsset = UnityEngine.TextCore.Text.FontAsset;
using TextCoreAtlasPop = UnityEngine.TextCore.Text.AtlasPopulationMode;

/// <summary>
/// Editor utility: bakes UI Toolkit (TextCore) SDF FontAssets from the pixel-font TTFs the
/// designer dropped into Assets/Fonts/Pixel Fonts. UITK uses UnityEngine.TextCore.Text.FontAsset
/// (NOT TMPro.TMP_FontAsset), so these are the assets a UITK USS -unity-font-definition can use.
/// Run via the Tools menu; outputs into Assets/Fonts/UITK SDF.
/// </summary>
public static class PixelFontAssetGenerator
{
    private static readonly string[] Sources =
    {
        "Assets/Fonts/Pixel Fonts/Munro/Munro.ttf",
        "Assets/Fonts/Pixel Fonts/F77MinecraftRegular.ttf",
        "Assets/Fonts/Pixel Fonts/Wellbutrin.ttf",
        "Assets/Fonts/Pixel Fonts/Cayetano/Cayetano.ttf",
        "Assets/Fonts/Pixel Fonts/Cayetano/CayetanoRoundBold.ttf",
    };

    private const string OutDir = "Assets/Fonts/UITK SDF";

    [MenuItem("Tools/Fonts/Generate Pixel Font SDF Assets (UITK)")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(OutDir))
            AssetDatabase.CreateFolder("Assets/Fonts", "UITK SDF");

        int made = 0;
        foreach (string src in Sources)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(src);
            if (font == null) { Debug.LogWarning($"[PixelFontGen] Missing font: {src}"); continue; }

            string assetName = Path.GetFileNameWithoutExtension(src) + " Pixel";
            string outPath = $"{OutDir}/{assetName}.asset";

            // Remove any prior asset (and its sub-assets) so we don't orphan atlases/materials.
            if (AssetDatabase.LoadAssetAtPath<TextCoreFontAsset>(outPath) != null)
                AssetDatabase.DeleteAsset(outPath);

            // RASTER_HINTED = hard-edged bitmap glyphs (no SDF smoothing), hinted to the pixel
            // grid. Combined with Point filtering below, this keeps pixel fonts crisp/blocky
            // instead of fuzzy. Small sampling size + tiny padding suits a low-res pixel font.
            TextCoreFontAsset fa = TextCoreFontAsset.CreateFontAsset(
                font, 32, 1, GlyphRenderMode.RASTER_HINTED, 1024, 1024,
                TextCoreAtlasPop.Dynamic, true);

            if (fa == null) { Debug.LogWarning($"[PixelFontGen] Failed to create asset for {src}"); continue; }

            fa.name = assetName;
            AssetDatabase.CreateAsset(fa, outPath);

            if (fa.atlasTextures != null && fa.atlasTextures.Length > 0 && fa.atlasTextures[0] != null)
            {
                fa.atlasTextures[0].name = assetName + " Atlas";
                fa.atlasTextures[0].filterMode = FilterMode.Point; // no bilinear blur when scaled
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            }
            if (fa.material != null)
            {
                fa.material.name = assetName + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }

            EditorUtility.SetDirty(fa);
            made++;
            Debug.Log($"[PixelFontGen] Created {outPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PixelFontGen] Done — {made} SDF font asset(s) in {OutDir}.");
    }
}
