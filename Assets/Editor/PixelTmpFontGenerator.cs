using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

/// <summary>
/// One-shot editor utility: bakes a TMP <see cref="TMP_FontAsset"/> from a pixel TTF so TextMeshPro
/// labels (which need a TMP_FontAsset, not the TextCore FontAssets in Fonts/UITK SDF used by UI Toolkit)
/// can render in our pixel fonts. Saved into Resources so it can be loaded by name at runtime.
///
/// Run via Tools ▸ Fonts ▸ Generate Cayetano TMP SDF. Idempotent (overwrites the output asset).
/// </summary>
public static class PixelTmpFontGenerator
{
    private const string SourceTtf = "Assets/Fonts/Pixel Fonts/Cayetano/CayetanoRoundBold.ttf";
    private const string OutputPath = "Assets/Resources/Fonts/CayetanoRoundBold SDF.asset";

    [MenuItem("Tools/Fonts/Generate Cayetano TMP SDF")]
    public static void GenerateCayetano()
    {
        Generate(SourceTtf, OutputPath, "CayetanoRoundBold SDF");
    }

    private static void Generate(string ttfPath, string outputPath, string assetName)
    {
        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (sourceFont == null)
        {
            Debug.LogError($"[PixelTmpFontGenerator] Source font not found at {ttfPath}");
            return;
        }

        // Dynamic SDF atlas: glyphs render on demand from the source TTF at runtime, so we don't have to
        // pre-bake a character set. 90px sampling / 9px padding / 1024² atlas matches TMP's own defaults
        // and gives crisp scaling for the small world-space hint.
        var fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);

        if (fontAsset == null)
        {
            Debug.LogError("[PixelTmpFontGenerator] TMP_FontAsset.CreateFontAsset returned null.");
            return;
        }

        fontAsset.name = assetName;

        var dir = System.IO.Path.GetDirectoryName(outputPath);
        if (!AssetDatabase.IsValidFolder(dir))
            System.IO.Directory.CreateDirectory(dir);

        // Replace any prior output so re-runs stay clean.
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outputPath) != null)
            AssetDatabase.DeleteAsset(outputPath);

        AssetDatabase.CreateAsset(fontAsset, outputPath);

        // The material + atlas texture are created in-memory by CreateFontAsset; persist them as
        // sub-assets of the font so the asset is self-contained (this is what TMP's own creator does).
        if (fontAsset.material != null)
        {
            fontAsset.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }
        if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
        {
            fontAsset.atlasTextures[0].name = assetName + " Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(outputPath);
        Debug.Log($"[PixelTmpFontGenerator] Generated TMP font asset at {outputPath}");
    }
}
