#if UNITY_EDITOR
using System.IO;
using Research;
using UnityEditor;
using UnityEngine;

public static class ResearchCatalogGenerator
{
    private const string OutDir   = "Assets/Resources/Research";
    private const string TuneDir  = "Assets/Data/Research";

    [MenuItem("Farm Game/Research/Generate Tuning Asset")]
    public static void GenerateTuning()
    {
        if (!Directory.Exists(TuneDir)) Directory.CreateDirectory(TuneDir);
        string path = $"{TuneDir}/ResearchTuning.asset";
        if (AssetDatabase.LoadAssetAtPath<ResearchTuning>(path) != null)
        {
            Debug.Log($"[ResearchCatalogGenerator] ResearchTuning already exists at {path}");
            return;
        }
        var tuning = ScriptableObject.CreateInstance<ResearchTuning>();
        AssetDatabase.CreateAsset(tuning, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ResearchCatalogGenerator] ResearchTuning created at {path}");
    }

    [MenuItem("Farm Game/Research/Generate Catalog")]
    public static void Generate()
    {
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

        // Clear any pre-existing catalog so re-running gives a clean slate.
        foreach (string guid in AssetDatabase.FindAssets("t:ResearchData", new[] { OutDir }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            AssetDatabase.DeleteAsset(p);
        }

        // Soil (2)
        CreateStd("soil_water_efficiency",    "Soil: Water Efficiency",        StatKey.SoilWaterEfficiency,   ResearchTier.Tier25,           "soil",   0.010f, t:1.5f, c:2.0f);
        CreateStd("soil_quality",             "Soil: Quality",                 StatKey.SoilQuality,           ResearchTier.Tier100Absurd,    "soil",   0.005f, t:1.2f, c:3.0f, baseSecs:360f, baseCoins:150f);

        // Helper (7)
        CreateStd("helper_till_speed",        "Helper: Till Speed",            StatKey.HelperTillSpeed,       ResearchTier.Tier100Standard,  "helper", 0.005f);
        CreateStd("helper_water_speed",       "Helper: Water Speed",           StatKey.HelperWaterSpeed,      ResearchTier.Tier100Standard,  "helper", 0.005f);
        CreateStd("helper_water_efficiency",  "Helper: Water Efficiency",      StatKey.HelperWaterEfficiency, ResearchTier.Tier25,           "helper", 0.010f);
        CreateStd("helper_plant_speed",       "Helper: Plant Seeding Speed",   StatKey.HelperPlantSpeed,      ResearchTier.Tier100Standard,  "helper", 0.005f);
        CreateStd("helper_harvest_speed",     "Helper: Harvest Speed",         StatKey.HelperHarvestSpeed,    ResearchTier.Tier100Standard,  "helper", 0.005f);
        CreateStd("helper_harvest_efficiency","Helper: Harvest Efficiency",    StatKey.HelperHarvestEfficiency,ResearchTier.Tier100Absurd,   "helper", 0.005f, t:1.2f, c:3.0f, baseSecs:360f, baseCoins:150f);
        CreateBinary("max_water_heals",       "Max Water Heals Plant HP",      "helper", featureID:FeatureFlag.MaxWaterHealsPlant, cost:5000, days:3);

        // Plant (3)
        CreateStd("crop_hp",                  "Plant: Hit Points",             StatKey.CropHp,                ResearchTier.Tier25,           "plant",  0.010f);
        CreateStd("crop_growth_speed",        "Plant: Growth Speed",           StatKey.CropGrowthSpeed,       ResearchTier.Tier100Standard,  "plant",  0.005f, t:1.5f, c:2.0f);
        CreateStd("crop_bonus_sell_amount",   "Plant: Bonus Sell Amount",      StatKey.CropBonusSellAmount,   ResearchTier.Tier100Absurd,    "plant",  0.005f, t:1.2f, c:3.0f, baseSecs:360f, baseCoins:150f);

        // Animals (8 — gated by ownership)
        CreateStd("chicken_cooldown",   "Chicken: Cooldown",   StatKey.ChickenCooldown,   ResearchTier.Tier25, "animals", 0.010f, animalID:"chicken");
        CreateStd("chicken_efficiency", "Chicken: Efficiency", StatKey.ChickenEfficiency, ResearchTier.Tier25, "animals", 0.010f, animalID:"chicken");
        CreateStd("dog_cooldown",       "Dog: Cooldown",       StatKey.DogCooldown,       ResearchTier.Tier25, "animals", 0.010f, animalID:"dog");
        CreateStd("dog_efficiency",     "Dog: Efficiency",     StatKey.DogEfficiency,     ResearchTier.Tier25, "animals", 0.010f, animalID:"dog");
        CreateStd("rooster_cooldown",   "Rooster: Cooldown",   StatKey.RoosterCooldown,   ResearchTier.Tier25, "animals", 0.010f, animalID:"rooster");
        CreateStd("rooster_efficiency", "Rooster: Efficiency", StatKey.RoosterEfficiency, ResearchTier.Tier25, "animals", 0.010f, animalID:"rooster");
        CreateStd("cow_passive_compost","Cow: Passive Compost Rate", StatKey.CowPassiveCompost, ResearchTier.Tier100Standard, "animals", 0.005f, animalID:"cow");
        CreateStd("cow_run_yield",      "Cow: Run Yield",            StatKey.CowRunYield,       ResearchTier.Tier25,          "animals", 0.010f, animalID:"cow");

        // Equipment (9 — Compost Bay deferred to Plan 2)
        CreateStd("scarecrow_aoe",          "Scarecrow: AoE",           StatKey.ScarecrowAoe,           ResearchTier.Tier100Standard, "equipment", 0.005f, t:0.8f, c:0.8f, unlockID:"scarecrow_unlock");
        CreateStd("scarecrow_effectiveness","Scarecrow: Effectiveness", StatKey.ScarecrowEffectiveness, ResearchTier.Tier100Standard, "equipment", 0.005f, t:0.8f, c:0.8f, unlockID:"scarecrow_unlock");
        CreateStd("scarecrow_cooldown",     "Scarecrow: Cooldown",      StatKey.ScarecrowCooldown,      ResearchTier.Tier25,          "equipment", 0.010f, t:0.8f, c:0.8f, unlockID:"scarecrow_unlock");
        CreateStd("sprinkler_aoe",          "Sprinkler: AoE",           StatKey.SprinklerAoe,           ResearchTier.Tier100Standard, "equipment", 0.005f, unlockID:"sprinkler_unlock");
        CreateStd("sprinkler_effectiveness","Sprinkler: Effectiveness", StatKey.SprinklerEffectiveness, ResearchTier.Tier100Standard, "equipment", 0.005f, unlockID:"sprinkler_unlock");
        CreateStd("sprinkler_cooldown",     "Sprinkler: Cooldown",      StatKey.SprinklerCooldown,      ResearchTier.Tier25,          "equipment", 0.010f, unlockID:"sprinkler_unlock");
        CreateStd("fence_aoe",              "Fence: AoE",               StatKey.FenceAoe,               ResearchTier.Tier100Standard, "equipment", 0.005f, unlockID:"fence_unlock");
        CreateStd("fence_effectiveness",    "Fence: Effectiveness",     StatKey.FenceEffectiveness,     ResearchTier.Tier100Standard, "equipment", 0.005f, unlockID:"fence_unlock");
        CreateStd("fence_cooldown",         "Fence: Cooldown",          StatKey.FenceCooldown,          ResearchTier.Tier25,          "equipment", 0.010f, unlockID:"fence_unlock");
        CreateStd("compost_bay_conversion", "Compost Bay: Conversion Efficiency", StatKey.CompostBayConversion, ResearchTier.Tier100Standard, "equipment", 0.005f, unlockID:"compostbay_unlock");

        // Weather (2)
        CreateStd("storm_damage_reduction", "Storm Damage Reduction", StatKey.StormDamageReduction, ResearchTier.Tier25, "weather", 0.010f);
        CreateStd("rain_watering",          "Rain Watering",          StatKey.RainWatering,         ResearchTier.Tier25, "weather", 0.010f);

        // Meta (8)
        CreateStd("game_speed",             "Game Speed Multiplier",  StatKey.GameSpeed,            ResearchTier.Tier10, "meta", 0.9f, c:2.5f, baseSecs:3600f, baseCoins:1000f);
        CreateStd("research_speed",         "Research Speed",         StatKey.ResearchSpeed,        ResearchTier.Tier25, "meta", 0.010f, c:1.5f);
        CreateBinary("slot_3_unlock",       "Research Slot 3",        "meta", slotIndex:2, cost:10000, days:7);
        CreateBinary("slot_4_unlock",       "Research Slot 4",        "meta", slotIndex:3, cost:50000, days:21);
        CreateBinary("composting_basics",   "Composting Basics",      "meta", featureID:FeatureFlag.CompostingBasics, cost:5000, days:3);
        CreateBinary("offline_unlock",      "Offline Progress",       "meta", featureID:FeatureFlag.OfflineProgress,  cost:2500, days:1);
        CreateStd("offline_cap",            "Offline Progress Cap",   StatKey.OfflineCap,           ResearchTier.Tier25, "meta", 0.01f,  prereqID:"offline_unlock");
        CreateStd("offline_efficiency",     "Offline Progress Efficiency", StatKey.OfflineEfficiency, ResearchTier.Tier10, "meta", 0.025f, prereqID:"offline_unlock");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ResearchCatalogGenerator] Catalog written to " + OutDir);
    }

    private static void CreateStd(
        string id, string displayName, string statKey, ResearchTier tier, string branch,
        float bonusPerLevel,
        float t = 1.0f, float c = 1.0f,
        float baseSecs = 120f, float baseCoins = 50f,
        string prereqID = "", string unlockID = "", string animalID = "")
    {
        var so = ScriptableObject.CreateInstance<ResearchData>();
        so.researchID         = id;
        so.displayName        = displayName;
        so.description        = AutoDescription(bonusPerLevel, tier);
        so.branchID           = branch;
        so.tier               = tier;
        so.targetStatKey      = statKey;
        so.bonusPerLevel      = bonusPerLevel;
        so.baseCostCoins      = baseCoins;
        so.baseDurationSecs   = baseSecs;
        so.timeDifficulty     = t;
        so.costDifficulty     = c;
        so.prerequisiteResearchID = prereqID;
        so.requiredUnlockID   = unlockID;
        so.requiredAnimalID   = animalID;
        Save(so, id);
    }

    private static string AutoDescription(float bonusPerLevel, ResearchTier tier)
    {
        int max = tier.MaxLevel();
        if (Mathf.Approximately(bonusPerLevel, 0f)) return "One-time unlock.";
        float maxBonus = bonusPerLevel * max;
        // Game Speed reads in "multiplier" units (0.9/lvl → +900% at L10)
        if (bonusPerLevel >= 0.5f)
            return $"+{bonusPerLevel:F1}x per level. Maxes at {1f + maxBonus:F1}x.";
        return $"+{bonusPerLevel * 100f:F1}% per level. Maxes at +{maxBonus * 100f:F0}%.";
    }

    private static void CreateBinary(
        string id, string displayName, string branch,
        string featureID = "", int slotIndex = -1,
        int cost = 5000, int days = 3,
        string prereqID = "", string unlockID = "", string animalID = "")
    {
        var so = ScriptableObject.CreateInstance<ResearchData>();
        so.researchID         = id;
        so.displayName        = displayName;
        so.description        = displayName + " — one-time unlock.";
        so.branchID           = branch;
        so.tier               = ResearchTier.Binary;
        so.targetStatKey      = "";
        so.bonusPerLevel      = 0f;
        so.binaryFixedCost    = cost;
        so.binaryFixedDurationSecs = days * 86400f;
        so.unlocksFeatureID   = featureID;
        so.unlocksSlotIndex   = slotIndex;
        so.prerequisiteResearchID = prereqID;
        so.requiredUnlockID   = unlockID;
        so.requiredAnimalID   = animalID;
        Save(so, id);
    }

    private static void Save(ResearchData so, string id)
    {
        string path = $"{OutDir}/Research_{id}.asset";
        AssetDatabase.CreateAsset(so, path);
    }
}
#endif
