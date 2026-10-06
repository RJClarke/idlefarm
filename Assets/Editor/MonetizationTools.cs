using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Farm Game > Monetization: build the Resources assets, apply the gem price changes,
/// and bake the HUD Gift button. All idempotent; runnable via Temp/menu.request.</summary>
public static class MonetizationTools
{
    private const string TuningPath = "Assets/Resources/FreeGiftTuning.asset";
    private const string CatalogPath = "Assets/Resources/StoreCatalog.asset";
    private const string ArtPath = "Assets/Resources/MonetizationArt.asset";

    private const string ChestClosedPath = "Assets/Sprites/UI/Icons/Cute/RpgThings/Chest_Red.png";
    private const string ChestOpenPath = "Assets/Sprites/UI/Icons/Cute/RpgThings/Chest_GoldOpen.png";
    private const string PassArtPath = "Assets/Sprites/UI/Icons/Monetization/NoAds_48.png";
    private const string GemIconPath = "Assets/Sprites/UI/Icons/Icons_Essential/Gem.png";
    private const string CoinIconPath = "Assets/Sprites/UI/Icons/Icons_Essential/Coin.png";
    private const string ChickenPath = "Assets/Data/Animals/Animal_Chicken.asset";

    [MenuItem("Farm Game/Monetization/Build Assets")]
    public static void BuildAssets()
    {
        var tuning = AssetDatabase.LoadAssetAtPath<FreeGiftTuning>(TuningPath);
        if (tuning == null) { tuning = ScriptableObject.CreateInstance<FreeGiftTuning>(); AssetDatabase.CreateAsset(tuning, TuningPath); }

        var catalog = AssetDatabase.LoadAssetAtPath<StoreCatalogSO>(CatalogPath);
        if (catalog == null) { catalog = ScriptableObject.CreateInstance<StoreCatalogSO>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
        var products = new List<StoreProductDef>(catalog.products ?? new StoreProductDef[0]);
        int retired = products.RemoveAll(p => p != null && System.Array.IndexOf(StoreDefaults.RetiredIds, p.id) >= 0);
        int added = 0;
        foreach (StoreProductDef d in StoreDefaults.Products)
            if (products.TrueForAll(p => p == null || p.id != d.id)) { products.Add(d); added++; }
        Sprite gem = LoadSprite(GemIconPath), chestClosed = LoadSprite(ChestClosedPath);
        Sprite coinSack = LoadSprite("Assets/Sprites/UI/Icons/Cute/RpgThings/CoinSack_Brown.png");
        StoreProductDef[] defaults = StoreDefaults.Products;
        foreach (StoreProductDef p in products)
        {
            if (p == null) continue;
            if (p.grantsSkinIds == null || p.grantsSkinIds.Length == 0)
            {
                StoreProductDef d = System.Array.Find(defaults, x => x.id == p.id);
                if (d != null && d.grantsSkinIds != null && d.grantsSkinIds.Length > 0) p.grantsSkinIds = d.grantsSkinIds;
            }
            // Pass copy is code-owned (user asked for "No ads. Ever."): keep the asset in step with StoreDefaults.
            if (p.id == StoreDefaults.PassId && p.description != StoreDefaults.PassDescription) p.description = StoreDefaults.PassDescription;
            if (p.icon != null) continue;
            if (p.section == StoreSection.Gems) p.icon = gem;
            else if (p.section == StoreSection.Pass || p.id == StoreDefaults.StarterId) p.icon = chestClosed;
            else if (p.section == StoreSection.Boosts) p.icon = coinSack;
        }
        if (retired > 0) Debug.Log($"[Monetization] Retired {retired} catalog products.");
        catalog.products = products.ToArray();
        EditorUtility.SetDirty(catalog);

        var art = AssetDatabase.LoadAssetAtPath<MonetizationArt>(ArtPath);
        if (art == null) { art = ScriptableObject.CreateInstance<MonetizationArt>(); AssetDatabase.CreateAsset(art, ArtPath); }
        if (art.chestClosed == null) art.chestClosed = chestClosed;
        if (art.chestOpen == null) art.chestOpen = LoadSprite(ChestOpenPath);
        if (art.gemIcon == null) art.gemIcon = gem;
        if (art.coinIcon == null) art.coinIcon = LoadSprite(CoinIconPath);
        if (art.passArt == null) { ConfigurePixelSprite(PassArtPath); art.passArt = LoadSprite(PassArtPath); }
        if (art.numberFont == null)
        {
            var almanac = Resources.Load<AlmanacArt>("AlmanacArt");
            if (almanac != null) art.numberFont = almanac.titleFont;
        }
        EditorUtility.SetDirty(art);

        AssetDatabase.SaveAssets();
        Debug.Log($"[Monetization] Built assets: tuning, catalog (+{added} products), art " +
                  $"(chest {(art.chestClosed != null ? "ok" : "MISSING")}/{(art.chestOpen != null ? "ok" : "MISSING")}, " +
                  $"font {(art.numberFont != null ? "ok" : "MISSING")}).");
    }

    [MenuItem("Farm Game/Monetization/Apply Gem Prices")]
    public static void ApplyGemPrices()
    {
        var chicken = AssetDatabase.LoadAssetAtPath<AnimalData>(ChickenPath);
        if (chicken != null)
        {
            var so = new SerializedObject(chicken);
            so.FindProperty("gemCost").intValue = 120;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(chicken);
        }
        else Debug.LogError("[Monetization] Chicken asset not found at " + ChickenPath);

        var research = Object.FindFirstObjectByType<ResearchManager>(FindObjectsInactive.Include);
        if (research != null)
        {
            var so = new SerializedObject(research);
            SerializedProperty defs = so.FindProperty("slotDefs");
            for (int i = 0; i < defs.arraySize; i++)
            {
                SerializedProperty d = defs.GetArrayElementAtIndex(i);
                if (d.FindPropertyRelative("unlockType").enumValueIndex == (int)ResearchManager.SlotUnlockType.Gems)
                    d.FindPropertyRelative("costAmount").intValue = 200;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(research.gameObject.scene);
            EditorSceneManager.SaveScene(research.gameObject.scene);
        }
        else Debug.LogError("[Monetization] No ResearchManager in the open scene.");

        AssetDatabase.SaveAssets();
        Debug.Log("[Monetization] Applied gem prices: Chicken 120, research gem slot 200.");
    }

    private const string FontPath = "Assets/Fonts/NotoSans-Regular SDF.asset";

    /// <summary>Clones EggClaimButton into a FreeGiftButton directly above it (70,550), swaps in the chest,
    /// adds timer + FREE/AD tag labels, and registers it with LocationModeController's Market hide list.</summary>
    [MenuItem("Farm Game/Monetization/Bake Gift Button")]
    public static void BakeGiftButton()
    {
        var egg = Object.FindFirstObjectByType<EggClaimButton>(FindObjectsInactive.Include);
        if (egg == null) { Debug.LogError("[Monetization] No EggClaimButton in the open scene to copy."); return; }
        Transform parent = egg.transform.parent;

        Transform existing = parent.Find("FreeGiftButton");
        if (existing != null) Object.DestroyImmediate(existing.gameObject); // rebake from scratch
        GameObject go = Object.Instantiate(egg.gameObject, parent);
        go.name = "FreeGiftButton";
        go.SetActive(true);
        Undo.RegisterCreatedObjectUndo(go, "Bake Gift Button");

        // Read the clone's EggClaimButton references (they point at the clone's own children), then drop it.
        var eggSo = new SerializedObject(go.GetComponent<EggClaimButton>());
        var btn = eggSo.FindProperty("button").objectReferenceValue as Button;
        var img = eggSo.FindProperty("buttonImage").objectReferenceValue as Image;
        var dot = eggSo.FindProperty("notificationDot").objectReferenceValue as GameObject;
        var emoji = eggSo.FindProperty("emojiText").objectReferenceValue as TextMeshProUGUI;
        Object.DestroyImmediate(go.GetComponent<EggClaimButton>());
        if (emoji != null) emoji.gameObject.SetActive(false);

        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = new Vector2(70, 550);
        rt.sizeDelta = new Vector2(100, 100);
        rt.localScale = Vector3.one;

        MonetizationArt art = AssetDatabase.LoadAssetAtPath<MonetizationArt>(ArtPath);
        if (img != null)
        {
            if (art != null && art.chestClosed != null) img.sprite = art.chestClosed;
            img.preserveAspect = true;
            img.color = Color.white;
        }

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        TextMeshProUGUI timer = NewLabel(go.transform, "TimerLabel", font, 24, new Vector2(0.5f, 0f), new Vector2(0, -16), new Vector2(140, 34));

        var badgeGo = new GameObject("TagBadge", typeof(RectTransform), typeof(Image));
        badgeGo.transform.SetParent(go.transform, false);
        var badgeRt = (RectTransform)badgeGo.transform;
        badgeRt.anchorMin = badgeRt.anchorMax = new Vector2(1f, 1f);
        badgeRt.anchoredPosition = new Vector2(4, -2);
        badgeRt.sizeDelta = new Vector2(82, 30);
        badgeGo.GetComponent<Image>().color = new Color(0.35f, 0.22f, 0.10f, 0.95f);
        badgeGo.GetComponent<Image>().raycastTarget = false;
        TextMeshProUGUI tag = NewLabel(badgeGo.transform, "TagLabel", font, 19, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(82, 30));
        tag.textWrappingMode = TextWrappingModes.NoWrap;
        tag.overflowMode = TextOverflowModes.Overflow;

        if (go.GetComponent<CanvasGroup>() == null) go.AddComponent<CanvasGroup>();
        var gift = go.AddComponent<FreeGiftButton>();
        var so = new SerializedObject(gift);
        so.FindProperty("button").objectReferenceValue = btn != null ? btn : go.GetComponent<Button>();
        so.FindProperty("icon").objectReferenceValue = img;
        so.FindProperty("notificationDot").objectReferenceValue = dot;
        so.FindProperty("timerLabel").objectReferenceValue = timer;
        so.FindProperty("tagBadge").objectReferenceValue = badgeGo;
        so.FindProperty("tagLabel").objectReferenceValue = tag;
        so.ApplyModifiedPropertiesWithoutUndo();

        var lmc = Object.FindFirstObjectByType<LocationModeController>(FindObjectsInactive.Include);
        if (lmc != null)
        {
            var lso = new SerializedObject(lmc);
            SerializedProperty list = lso.FindProperty("hideAtMarketByName");
            bool present = false;
            for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).stringValue == "FreeGiftButton") present = true;
            if (!present)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = "FreeGiftButton";
                lso.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        else Debug.LogWarning("[Monetization] No LocationModeController; the gift button won't hide at the Market.");

        go.transform.SetSiblingIndex(egg.transform.GetSiblingIndex() + 1);
        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene);
        Debug.Log("[Monetization] Baked FreeGiftButton into " + go.scene.name + ".");
    }

    private static TextMeshProUGUI NewLabel(Transform parent, string name, TMP_FontAsset font, float size,
                                            Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
    {
        var lgo = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        lgo.transform.SetParent(parent, false);
        var lrt = (RectTransform)lgo.transform;
        lrt.anchorMin = lrt.anchorMax = anchor;
        lrt.anchoredPosition = pos;
        lrt.sizeDelta = sizeDelta;
        var t = lgo.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.98f, 0.93f, 0.82f);
        t.outlineWidth = 0.2f;
        t.outlineColor = new Color32(40, 25, 10, 255);
        t.raycastTarget = false;
        return t;
    }

    private const string SkinCatalogPath = "Assets/Resources/SkinCatalog.asset";

    private const string HayloftDir = "Assets/Sprites/Buildings/Hayloft/";
    private static readonly string[] HayloftColours = { "Red", "Green", "Grey", "Yellow" };

    /// <summary>The Hayloft pack ships the barn front and its roof as separate pieces. Stack Roof_Hayloft_Top
    /// above each Front_Hayloft_* into one full barn (Hayloft/Full/Hayloft_*_Full_32x32.png), imported as a
    /// single point-filtered, uncompressed sprite. Idempotent: existing composites are left alone.</summary>
    public static void EnsureHayloftComposites()
    {
        string outDir = HayloftDir + "Full/";
        if (!AssetDatabase.IsValidFolder(outDir.TrimEnd('/'))) AssetDatabase.CreateFolder(HayloftDir.TrimEnd('/'), "Full");
        Texture2D roof = LoadRaw(HayloftDir + "Roof_Hayloft_Top_32x32.png");
        if (roof == null) { Debug.LogError("[Skins] Hayloft roof piece missing."); return; }
        foreach (string colour in HayloftColours)
        {
            string outPath = outDir + "Hayloft_" + colour + "_Full_32x32.png";
            if (System.IO.File.Exists(outPath)) continue;
            Texture2D front = LoadRaw(HayloftDir + "Front_Hayloft_" + colour + "_32x32.png");
            if (front == null || front.width != roof.width) { Debug.LogError("[Skins] Hayloft front missing/mismatched: " + colour); continue; }
            var full = new Texture2D(front.width, front.height + roof.height, TextureFormat.RGBA32, false);
            full.SetPixels(0, 0, front.width, front.height, front.GetPixels());              // texture y=0 is the bottom
            full.SetPixels(0, front.height, roof.width, roof.height, roof.GetPixels());      // roof stacked on top
            full.Apply();
            System.IO.File.WriteAllBytes(outPath, full.EncodeToPNG());
            Object.DestroyImmediate(full); Object.DestroyImmediate(front);
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(outPath);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 32;
            imp.spritePivot = new Vector2(0.5f, 0f);
            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            imp.SetTextureSettings(settings);
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
            Debug.Log("[Skins] Built full barn " + outPath);
        }
        Object.DestroyImmediate(roof);
    }

    /// <summary>Single, point-filtered, uncompressed sprite import (pixel art that stays crisp when scaled up).</summary>
    private static void ConfigurePixelSprite(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); imp = AssetImporter.GetAtPath(path) as TextureImporter; }
        if (imp == null) { Debug.LogWarning("[Monetization] No importer for " + path); return; }
        if (imp.textureType == TextureImporterType.Sprite && imp.filterMode == FilterMode.Point &&
            imp.textureCompression == TextureImporterCompression.Uncompressed && imp.spriteImportMode == SpriteImportMode.Single) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 32;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.SaveAndReimport();
    }

    /// <summary>PNG bytes -> readable texture, regardless of the asset's Read/Write import flag.</summary>
    private static Texture2D LoadRaw(string path)
    {
        if (!System.IO.File.Exists(path)) return null;
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        return t.LoadImage(System.IO.File.ReadAllBytes(path)) ? t : null;
    }

    [MenuItem("Farm Game/Monetization/Build Skin Catalog")]
    public static void BuildSkinCatalog()
    {
        EnsureHayloftComposites();
        var cat = AssetDatabase.LoadAssetAtPath<SkinCatalogSO>(SkinCatalogPath);
        if (cat == null) { cat = ScriptableObject.CreateInstance<SkinCatalogSO>(); AssetDatabase.CreateAsset(cat, SkinCatalogPath); }
        var list = new List<SkinDef>(cat.skins ?? new SkinDef[0]);
        int added = 0, missing = 0;
        foreach (SkinDef d in SkinDefaults.All)
            if (list.TrueForAll(s => s == null || s.id != d.id)) { list.Add(d); added++; }
        SkinDef[] defaults = SkinDefaults.All;
        foreach (SkinDef s in list)
        {
            if (s == null) continue;
            // Art paths are code-owned (names/prices stay editable): re-sync and reload art when a default moved.
            SkinDef def = System.Array.Find(defaults, x => x.id == s.id);
            if (def != null && def.assetPath != s.assetPath) { s.assetPath = def.assetPath; s.texture = null; s.sprite = null; }
            if (s.kind == SkinKind.AnimalSheet && s.texture == null) s.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(s.assetPath);
            if (s.kind == SkinKind.BuildingSprite && s.sprite == null) s.sprite = LoadSprite(s.assetPath);
            if ((s.kind == SkinKind.AnimalSheet && s.texture == null) || (s.kind == SkinKind.BuildingSprite && s.sprite == null))
            { missing++; Debug.LogWarning("[Skins] Art not found for " + s.id + " at " + s.assetPath); }
        }
        cat.skins = list.ToArray();
        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Monetization] Built skin catalog: {cat.skins.Length} skins (+{added}), {missing} missing art.");
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null)
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Sprite sp) { s = sp; break; }
        if (s == null) Debug.LogWarning("[Monetization] Sprite not found: " + path);
        return s;
    }
}
