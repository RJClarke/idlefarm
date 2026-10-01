using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm Game > Seeds > Apply Seed Progression: copies SeedLadder onto the CropData assets, reorders
/// CropDatabase.allCrops into the one fixed crop order, makes Radish the only starting crop, and
/// applies the welcome / compost letter copy changes. Idempotent — safe to run again after tuning
/// SeedLadder (it overwrites the per-crop values with the ladder's).
/// </summary>
public static class SeedProgressionTools
{
    private const string DatabasePath = "Assets/Data/Crops/CropDatabase.asset";
    private const string CatalogPath = "Assets/Resources/LetterCatalog.asset";

    public const string WelcomeBody =
        "Dear {farmName},\n\nWelcome to the valley! This old farm has sat quiet for years, and we're all so glad someone is bringing it back to life.\n\n" +
        "I've left a sack of radish seeds in your shed. They're quick to grow and hard to get wrong - just the thing for a first harvest. Choose a field, and your helpers will plant, water and harvest for you.\n\n" +
        "Every harvest earns Money to buy more seeds, plus Coins you get to keep. Hazel at the seed stall in the Market has more kinds when you're ready - here's a little something toward your first packet.\n\n" +
        "- Mayor Bramble";

    public const string CompostBody =
        "Hey {farmName}!\n\nHeard you've been reading up on composting. Good news - I can put together a Compost Bay for you now. Swing by the shop and grab one; your soil will thank you.\n\n" +
        "Oh! And ask Hazel about corn. Those big stalks make twice the compost when a plant doesn't make it.\n\n- Pippa";

    [MenuItem("Farm Game/Seeds/Apply Seed Progression")]
    public static void Apply()
    {
        var db = AssetDatabase.LoadAssetAtPath<CropDatabase>(DatabasePath);
        if (db == null) { Debug.LogError("[Seeds] CropDatabase.asset not found"); return; }

        var byName = AssetDatabase.FindAssets("t:CropData")
            .Select(g => AssetDatabase.LoadAssetAtPath<CropData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null)
            .ToDictionary(c => c.cropName);

        var ordered = new System.Collections.Generic.List<CropData>();
        foreach (SeedLadder.Entry e in SeedLadder.Entries)
        {
            if (!byName.TryGetValue(e.cropName, out CropData crop))
            {
                Debug.LogError($"[Seeds] No CropData named '{e.cropName}' - fix the asset or SeedLadder.");
                return;
            }
            Undo.RecordObject(crop, "Apply Seed Progression");
            crop.unlockCost = e.cost;
            crop.unlockFeatureFlag = e.featureFlag ?? "";
            crop.isStarter = e.starter;
            crop.coinValue = e.coinValue;
            crop.compostMultiplier = e.compostMultiplier;
            EditorUtility.SetDirty(crop);
            ordered.Add(crop);
        }

        Undo.RecordObject(db, "Apply Seed Progression");
        db.allCrops = ordered;
        db.startingCrops = ordered.Where(c => c.isStarter).ToList();
        EditorUtility.SetDirty(db);

        ApplyLetterCopy();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Seeds] Applied seed progression to {ordered.Count} crops: {string.Join(", ", ordered.Select(c => c.cropName))}");
    }

    private static void ApplyLetterCopy()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LetterCatalogSO>(CatalogPath);
        if (catalog == null || catalog.letters == null) { Debug.LogWarning("[Seeds] LetterCatalog not found"); return; }
        Undo.RecordObject(catalog, "Apply Seed Progression copy");
        foreach (LetterDef l in catalog.letters)
        {
            if (l == null) continue;
            if (l.id == "welcome") { l.body = WelcomeBody; l.rewardKind = RewardKind.Coins; l.rewardAmount = 50; }
            if (l.id == "compost_bay_unlock") l.body = CompostBody;
        }
        EditorUtility.SetDirty(catalog);
    }
}
