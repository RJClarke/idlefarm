#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates the Farm Upgrades catalog (FarmUpgradeData assets) into Resources/FarmUpgrades.
/// Re-runnable: clears the folder first. Tuning numbers here are first-pass starting points
/// per the design (high-cap tracks ~500-1000 on a gentle curve + breakpoints; capped tracks
/// ~20-40; Water Capacity a very-low, very-slow buffer).
/// </summary>
public static class FarmUpgradeCatalogGenerator
{
    private const string OutDir = "Assets/Resources/FarmUpgrades";

    [MenuItem("Farm Game/Farm Upgrades/Generate Catalog")]
    public static void Generate()
    {
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

        foreach (string guid in AssetDatabase.FindAssets("t:FarmUpgradeData", new[] { OutDir }))
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));

        // ───────── Soil ─────────
        Create("cash_yield", "Fertilizer A", "Increase Cash earned per harvest.", "🌾",
            FarmUpgradeSection.Soil, FarmUpgradeKey.CashYield,
            maxLevel: 500, bonusPerLevel: 0.01f, highCap: true,
            baseCoin: 50, coinGrowth: 1.06f, baseMoney: 5000);

        Create("coin_yield", "Fertilizer B", "Increase Coins banked per harvest.", "🪙",
            FarmUpgradeSection.Soil, FarmUpgradeKey.CoinYield,
            maxLevel: 500, bonusPerLevel: 0.01f, highCap: true,
            baseCoin: 75, coinGrowth: 1.06f, baseMoney: 6000);

        Create("soil_quality", "Soil Quality", "Boost ALL crop output. The master multiplier.", "🏆",
            FarmUpgradeSection.Soil, FarmUpgradeKey.SoilQuality,
            maxLevel: 1000, bonusPerLevel: 0.005f, highCap: true,
            baseCoin: 200, coinGrowth: 1.05f, baseMoney: 20000);

        Create("compost_yield", "Compost Yield", "More compost from crops and the cow.", "💩",
            FarmUpgradeSection.Soil, FarmUpgradeKey.CompostYield,
            maxLevel: 30, bonusPerLevel: 0.02f,
            baseCoin: 100, coinGrowth: 1.12f);

        Create("soil_prep", "Soil Prep", "Helpers till soil faster.", "⛏️",
            FarmUpgradeSection.Soil, FarmUpgradeKey.SoilPrep,
            maxLevel: 25, bonusPerLevel: 0.02f,
            baseCoin: 80, coinGrowth: 1.12f);

        // ───────── Water ─────────
        Create("water_retention", "Water Retention", "Soil dries out more slowly.", "💧",
            FarmUpgradeSection.Water, FarmUpgradeKey.WaterRetention,
            maxLevel: 40, bonusPerLevel: 0.015f,
            baseCoin: 60, coinGrowth: 1.10f);

        // Very-low, very-slow buffer track. Each level adds moisture-points of headroom above 100%.
        Create("water_capacity", "Water Capacity", "Hold water above 100% for a deeper reserve.", "🛢️",
            FarmUpgradeSection.Water, FarmUpgradeKey.WaterCapacity,
            maxLevel: 8, bonusPerLevel: 5f, unit: "pts",
            baseCoin: 500, coinGrowth: 1.6f, coinBpEvery: 2, coinBpMult: 3f, baseMoney: 50000);

        Create("drying_grace", "Drying Grace", "Longer grace before a dry crop takes damage.", "🌧️",
            FarmUpgradeSection.Water, FarmUpgradeKey.DryingGrace,
            maxLevel: 25, bonusPerLevel: 0.04f,
            baseCoin: 70, coinGrowth: 1.10f);

        Create("watering_power", "Watering Power", "Each watering adds more moisture.", "🚿",
            FarmUpgradeSection.Water, FarmUpgradeKey.WateringPower,
            maxLevel: 25, bonusPerLevel: 0.03f,
            baseCoin: 70, coinGrowth: 1.10f);

        // ───────── Crops ─────────
        Create("growth_rate", "Growth Rate", "Crops grow faster.", "🌱",
            FarmUpgradeSection.Crops, FarmUpgradeKey.GrowthRate,
            maxLevel: 500, bonusPerLevel: 0.006f, highCap: true,
            baseCoin: 100, coinGrowth: 1.06f, baseMoney: 8000);

        Create("slow_decay", "Slow Decay", "Dried-out crops lose health more slowly.", "🥀",
            FarmUpgradeSection.Crops, FarmUpgradeKey.SlowDecay,
            maxLevel: 40, bonusPerLevel: 0.03f,
            baseCoin: 60, coinGrowth: 1.10f);

        Create("rot_resistance", "Rot Resistance", "Overripe crops rot more slowly.", "🍂",
            FarmUpgradeSection.Crops, FarmUpgradeKey.RotResistance,
            maxLevel: 30, bonusPerLevel: 0.03f,
            baseCoin: 60, coinGrowth: 1.10f);

        Create("crop_hardiness", "Crop Hardiness", "Crops take less damage from threats and weather.", "🛡️",
            FarmUpgradeSection.Crops, FarmUpgradeKey.CropHardiness,
            maxLevel: 30, bonusPerLevel: 0.03f,
            baseCoin: 90, coinGrowth: 1.12f);

        Create("head_start", "Head Start", "New crops begin partway into their first stage.", "⏩",
            FarmUpgradeSection.Crops, FarmUpgradeKey.HeadStart,
            maxLevel: 20, bonusPerLevel: 0.01f,
            baseCoin: 120, coinGrowth: 1.14f);

        Create("bountiful_harvest", "Bountiful Harvest", "Chance for a harvest to yield double.", "✨",
            FarmUpgradeSection.Crops, FarmUpgradeKey.BountifulHarvest,
            maxLevel: 40, bonusPerLevel: 0.01f,
            baseCoin: 150, coinGrowth: 1.10f);

        Create("seed_refund", "Seed Refund", "Chance to get a seed back when harvesting.", "♻️",
            FarmUpgradeSection.Crops, FarmUpgradeKey.SeedRefund,
            maxLevel: 30, bonusPerLevel: 0.01f,
            baseCoin: 120, coinGrowth: 1.12f);

        // ───────── Land (per-zone Zone Level) ─────────
        for (int z = 1; z <= 4; z++)
        {
            Create($"zone_level_z{z}", $"Zone {z} Level", $"Boost everything grown in Zone {z}.", "🏞️",
                FarmUpgradeSection.Land, FarmUpgradeKey.ZoneLevel,
                maxLevel: 500, bonusPerLevel: 0.005f, highCap: true,
                baseCoin: 100, coinGrowth: 1.06f, baseMoney: 8000, zoneTarget: z);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        int count = AssetDatabase.FindAssets("t:FarmUpgradeData", new[] { OutDir }).Length;
        Debug.Log($"[FarmUpgradeCatalogGenerator] Generated {count} Farm Upgrade assets in {OutDir}");
    }

    private static void Create(
        string id, string displayName, string subtext, string icon,
        FarmUpgradeSection section, string effectKey,
        int maxLevel, float bonusPerLevel,
        long baseCoin, float coinGrowth,
        string unit = "%", bool highCap = false,
        int coinBpEvery = 25, float coinBpMult = 2.0f,
        long baseMoney = 5000, float moneyGrowth = 1.12f,
        int zoneTarget = 0)
    {
        var d = ScriptableObject.CreateInstance<FarmUpgradeData>();
        d.upgradeID = id;
        d.displayName = displayName;
        d.subtext = subtext;
        d.icon = icon;
        d.section = section;
        d.effectKey = effectKey;
        d.zoneTarget = zoneTarget;
        d.maxLevel = maxLevel;
        d.isHighCap = highCap;
        d.bonusPerLevel = bonusPerLevel;
        d.bonusUnit = unit;

        d.baseCoinCost = baseCoin;
        d.coinGrowthPerLevel = coinGrowth;
        d.coinBreakpointEvery = coinBpEvery;
        d.coinBreakpointMultiplier = coinBpMult;

        d.baseMoneyCost = baseMoney;
        d.moneyGrowthPerLevel = moneyGrowth;
        d.moneyBreakpointEvery = coinBpEvery;
        d.moneyBreakpointMultiplier = coinBpMult;

        AssetDatabase.CreateAsset(d, $"{OutDir}/{id}.asset");
    }
}
#endif
