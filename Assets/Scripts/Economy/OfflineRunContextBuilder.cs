using System.Collections.Generic;
using UnityEngine;
using Research;

/// <summary>Result of a built+simulated offline run, with tax-adjusted payouts and CropData mapping.</summary>
public class OfflineRunOutcome
{
    public OfflineRunResult result;
    public int taxedCoins;        // floor(result.coinsBanked * (1 - effectiveTax))
    public int taxedResumeMoney;  // floor(result.finalMoney  * (1 - effectiveTax))
    public int compostGranted;    // untaxed
    public Dictionary<CropData, int> harvestedByCrop = new Dictionary<CropData, int>();
    /// <summary>Sim crop id (CropData.cropName) → CropData, for mapping per-zone results back.</summary>
    public Dictionary<string, CropData> cropById = new Dictionary<string, CropData>();
}

/// <summary>
/// Builds an OfflineSimContext from live game state (saved run snapshot + upgrades + equipment + live
/// wave/storm tuning), runs OfflineRunSimulator, and exposes the result plus tax-adjusted payouts.
/// Glue (singleton + CropData access) — not unit-tested; the pure pieces live in EconomyCore.
/// </summary>
public static class OfflineRunContextBuilder
{
    /// <summary>
    /// Returns null if offline progress isn't unlocked, no active run, no zones, or zero gap.
    /// `awaySeconds` is the real wall-clock gap (already &gt;= MinGap when called).
    /// </summary>
    public static OfflineRunOutcome BuildAndSimulate(float awaySeconds, float startFarmSeconds, int startMoney)
    {
        var research = ResearchManager.Instance;
        if (research != null && !research.IsFeatureUnlocked(FeatureFlag.OfflineProgress))
            return null;

        var seeds = SeedSelectionPopup.Instance != null
            ? SeedSelectionPopup.Instance.LoadAndApplySavedSelections() : null;
        if (seeds == null || seeds.Count == 0) return null;
        if (FarmGrid.Instance == null) return null;

        var tuning = new OfflineSimTuning();
        PopulateLiveTuning(tuning);
        // Helper planting throughput scales with plant-speed research (so faster helpers plant/replant more
        // offline). Harvest-speed and helper-count scaling aren't modeled in v1 (the sim harvests instantly
        // and uses a single farm-wide throughput) — see plan simplifications.
        tuning.plantsPerSecond *= 1f + Mathf.Max(0f, Bonus(StatKey.HelperPlantSpeed));

        var ctx = new OfflineSimContext
        {
            awaySeconds = ClampAway(awaySeconds, research),
            startFarmSeconds = startFarmSeconds,
            startMoney = startMoney,
            maxGameSpeed = 1f + Bonus(StatKey.GameSpeed),
            tuning = tuning,
            seedBagDiscount = Bonus(StatKey.SeedBagDiscount),
            seedBagSizeBonus = Bonus(StatKey.SeedBagSize),
        };

        // zones + CropData -> SimCrop (bake yield/coin/growth research into the SimCrop values)
        float sellBonus = Bonus(StatKey.CropBonusSellAmount);
        float coinBonus = Bonus(StatKey.CropBonusCoinAmount);
        float growthBonus = Bonus(StatKey.CropGrowthSpeed);
        var cropById = new Dictionary<string, CropData>();
        foreach (var kv in seeds)
        {
            CropData crop = kv.Value;
            if (crop == null) continue;
            cropById[crop.cropName] = crop;
            ctx.zones.Add(new SimZone
            {
                zoneId = kv.Key,
                tileCount = FarmGrid.Instance.TileCountPerZone,
                crop = new SimCrop
                {
                    id = crop.cropName,
                    growSeconds = crop.TotalGrowthTime / (1f + Mathf.Max(0f, growthBonus)),
                    harvestWindowSeconds = crop.harvestWindowSeconds,
                    harvestValue = Mathf.RoundToInt(crop.harvestValue * (1f + sellBonus)),
                    coinValue = Mathf.Max(1, Mathf.RoundToInt(crop.coinValue * (1f + coinBonus))),
                    bagBaseCost = crop.seedBagBaseCost,
                    bagSize = crop.seedBagSize,
                    tier = crop.tier,
                }
            });
        }
        if (ctx.zones.Count == 0) return null;

        // mitigation: scan equipped assignments + research effectiveness, stack per cause
        ResolveMitigation(ctx, tuning, seeds);

        var result = OfflineRunSimulator.Simulate(ctx);

        float effBonus = Bonus(StatKey.OfflineEfficiency);
        var outcome = new OfflineRunOutcome
        {
            result = result,
            taxedCoins = OfflineTax.Payout(result.coinsBanked, effBonus),
            taxedResumeMoney = OfflineTax.Payout(result.finalMoney, effBonus),
            compostGranted = result.compostGained, // untaxed
        };
        outcome.cropById = cropById;
        foreach (var kv in result.harvestedByCropId)
            if (cropById.TryGetValue(kv.Key, out var c)) outcome.harvestedByCrop[c] = kv.Value;
        return outcome;
    }

    private static float Bonus(string key)
        => ResearchManager.Instance != null ? ResearchManager.Instance.GetBonus(key) : 0f;

    private static float ClampAway(float awaySeconds, ResearchManager research)
    {
        float capHours = research != null ? research.GetBonus(StatKey.OfflineCap) : 0f;
        if (capHours <= 0f) return awaySeconds;          // 0 = no cap configured
        return Mathf.Min(awaySeconds, capHours * 3600f);
    }

    private static void PopulateLiveTuning(OfflineSimTuning t)
    {
        if (ThreatWaveManager.Instance != null)
        {
            var w = ThreatWaveManager.Instance.GetOfflineWaveConfig();
            t.waveIntervalSeconds = w.waveIntervalSeconds;
            t.deerStartWave = w.deerStartWave; t.deerCountInterval = w.deerCountInterval; t.maxDeer = w.maxDeer;
            t.crowStartWave = w.crowStartWave; t.crowCountInterval = w.crowCountInterval; t.maxCrows = w.maxCrows;
            t.baseHunger = w.baseHunger; t.crowBaseHunger = w.crowBaseHunger; t.hungerScalePerWave = w.hungerScalePerWave;
        }
        if (ThunderstormManager.Instance != null)
        {
            t.stormWaveInterval = ThunderstormManager.Instance.StormWaveInterval;
            t.lightningStrikeInterval = ThunderstormManager.Instance.LightningStrikeInterval;
        }
    }

    private static void ResolveMitigation(OfflineSimContext ctx, OfflineSimTuning t, Dictionary<int, CropData> seeds)
    {
        bool hasFence = false, hasScarecrow = false, hasSprinkler = false;
        if (EquipmentManager.Instance != null)
            foreach (var zoneId in seeds.Keys)
            {
                var eq = EquipmentManager.Instance.GetAssignment(zoneId);
                if (eq == null) continue;
                if (eq.equipmentType == EquipmentType.Fence) hasFence = true;
                else if (eq.equipmentType == EquipmentType.Scarecrow) hasScarecrow = true;
                else if (eq.equipmentType == EquipmentType.Sprinkler) hasSprinkler = true;
            }

        bool hasDog = Object.FindFirstObjectByType<FarmDog>() != null; // FarmDog has no singleton; scan scene

        float deer = OfflineMitigation.Stack(
            OfflineMitigation.Reduction(hasFence, t.fenceDeerReduction, Bonus(StatKey.FenceEffectiveness)),
            OfflineMitigation.Reduction(hasDog,   t.dogDeerReduction,   Bonus(StatKey.DogEfficiency)));
        float crow = OfflineMitigation.Reduction(hasScarecrow, t.scarecrowCrowReduction, Bonus(StatKey.ScarecrowEffectiveness));
        float dry = OfflineMitigation.Stack(
            OfflineMitigation.Reduction(hasSprinkler, t.sprinklerDryReduction, Bonus(StatKey.SprinklerEffectiveness)),
            Mathf.Clamp01(Bonus(StatKey.SoilWaterEfficiency) + Bonus(StatKey.HelperWaterEfficiency)));
        float lightning = Mathf.Clamp01(Bonus(StatKey.StormDamageReduction));

        ctx.deerLossReduction = deer;
        ctx.crowLossReduction = crow;
        ctx.dryLossReduction = dry;
        ctx.lightningLossReduction = lightning;
    }
}
