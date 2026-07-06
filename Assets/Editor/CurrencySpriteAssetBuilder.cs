using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using TMPro;

/// <summary>
/// One-shot utility: slice CurrencySprites.png into named sub-sprites (coin/gem/cash/compost)
/// and build a TMP Sprite Asset at Resources/Sprites/ so text can use
/// &lt;sprite="CurrencySprites" name="coin"&gt; etc.
/// </summary>
public static class CurrencySpriteAssetBuilder
{
    private const string TexPath   = "Assets/Sprites/UI/Icons/CurrencySprites.png";
    private const string AssetPath = "Assets/Resources/Sprite Assets/CurrencySprites.asset";
    private const int Cell = 32;
    private static readonly string[] Names = { "coin", "gem", "cash", "compost" };

    [MenuItem("Farm Game/Build Currency Sprite Asset")]
    public static void Build()
    {
        // 1) Slice the atlas into named sub-sprites.
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexPath);
        if (importer == null) { Debug.LogError($"[CurrencySprite] No importer at {TexPath}"); return; }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.spritePixelsPerUnit = 32f;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = true;

        var metas = new List<SpriteMetaData>();
        for (int i = 0; i < Names.Length; i++)
        {
            metas.Add(new SpriteMetaData
            {
                name = Names[i],
                rect = new Rect(i * Cell, 0, Cell, Cell), // bottom-left origin, single row
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
            });
        }
#pragma warning disable 618
        importer.spritesheet = metas.ToArray();
#pragma warning restore 618
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        AssetDatabase.ImportAsset(TexPath, ImportAssetOptions.ForceUpdate);

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(TexPath)
            .OfType<Sprite>()
            .OrderBy(s => s.rect.x)
            .ToList();
        if (sprites.Count != Names.Length)
        {
            Debug.LogError($"[CurrencySprite] Expected {Names.Length} sub-sprites, got {sprites.Count}.");
            return;
        }

        // 2) Build the TMP Sprite Asset. (Must live under Resources/Sprite Assets/ to match
        //    TMP_Settings.defaultSpriteAssetPath so <sprite="CurrencySprites"> resolves.)
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Sprite Assets"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateFolder("Assets/Resources", "Sprite Assets");
        }

        var spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        AssetDatabase.CreateAsset(spriteAsset, AssetPath);
        typeof(TMP_Asset).GetField("m_Version", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(spriteAsset, "1.1.0");
        spriteAsset.spriteInfoList = new List<TMP_Sprite>();
        spriteAsset.spriteSheet = texture;
        spriteAsset.spriteCharacterTable.Clear();
        spriteAsset.spriteGlyphTable.Clear();;

        for (int i = 0; i < sprites.Count; i++)
        {
            Sprite sp = sprites[i];
            var r = sp.rect;
            var glyph = new TMP_SpriteGlyph
            {
                index = (uint)i,
                metrics = new GlyphMetrics(r.width, r.height, 0f, r.height, r.width),
                glyphRect = new GlyphRect((int)r.x, (int)r.y, (int)r.width, (int)r.height),
                scale = 1f,
                atlasIndex = 0,
                sprite = sp,
            };
            spriteAsset.spriteGlyphTable.Add(glyph);

            var character = new TMP_SpriteCharacter(0xE000u + (uint)i, glyph)
            {
                name = sp.name,
                scale = 1f,
            };
            spriteAsset.spriteCharacterTable.Add(character);
        }

        // Material referencing the atlas (TMP sprite shader).
        Shader shader = Shader.Find("TextMeshPro/Sprite");
        var mat = new Material(shader);
        mat.SetTexture(ShaderUtilities.ID_MainTex, texture);
        mat.hideFlags = HideFlags.HideInHierarchy;
        mat.name = "CurrencySprites Material";
        AssetDatabase.AddObjectToAsset(mat, spriteAsset);
        spriteAsset.material = mat;

        spriteAsset.UpdateLookupTables();
        EditorUtility.SetDirty(spriteAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CurrencySprite] Built TMP Sprite Asset with {spriteAsset.spriteCharacterTable.Count} sprites at {AssetPath}");
    }
}
