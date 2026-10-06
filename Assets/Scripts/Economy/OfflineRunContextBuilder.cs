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
    /// <summary>The equipped animal the away-run simulated (null if none does anything during a run).</summary>
    public AnimalData animal;
    /// <summary>Collect was switched on when the player left: it pauses while away, so the
    /// welcome-back popup says why the harvests were sold.</summary>
    public bool collectPausedWhileAway;
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

        // zones + CropData -> SimCrop. Payouts, growth and health use the same CropStats maths as
        // play (Research, Barn skills, farm upgrades per field); random bonuses use their average.
        float luck = (1f + Mathf.Clamp01(FarmUpgrades.BountifulChance + FarmSkillsManager.MilestoneChanceOf(FarmSkillTrack.Harvesting)))
                     * FarmSkillsManager.GoldenExpectedMultiplier;
        var cropById = new Dictionary<string, CropData>();
        foreach (var kv in seeds)
        {
            CropData crop = kv.Value;
            if (crop == null) continue;
            cropById[crop.cropName] = crop;
            int zone = kv.Key;
            // Pest damage scales with appetite and lands on the crop's health; the loss rates were
            // tuned on average crops, so both are taken relative to that average.
            float hpFactor = AverageCropHp / Mathf.Max(1f, CropStats.Health(crop).total);
            ctx.zones.Add(new SimZone
            {
                zoneId = zone,
                tileCount = FarmGrid.Instance.TileCountPerZone,
                compostPerLoss = CompostPerLoss(zone, crop),
                crop = new SimCrop
                {
                    id = crop.cropName,
                    growSeconds = CropStats.StageSeconds(crop, GrowthStage.Seed) + CropStats.StageSeconds(crop, GrowthStage.Sprout)
                                  + CropStats.StageSeconds(crop, GrowthStage.Sapling),
                    regrowSeconds = crop.canRegrow ? CropStats.RegrowSeconds(crop) : 0f,
                    harvestWindowSeconds = crop.harvestWindowSeconds,
                    harvestValue = Mathf.RoundToInt(CropStats.Money(crop, zone).total * luck),
                    coinValue = Mathf.Max(1, Mathf.RoundToInt(CropStats.Coins(crop, zone).total * luck)),
                    bagBaseCost = crop.seedBagBaseCost,
                    bagSize = crop.seedBagSize,
                    tier = crop.tier,
                    deerLoss = crop.deerAppetite * hpFactor,
                    crowLoss = crop.crowAppetite * hpFactor,
                    dryLoss = crop.moistureDepletionRate, // water research/upgrades are already in dryLossReduction
                }
            });
        }
        if (ctx.zones.Count == 0) return null;

        // mitigation: scan equipped assignments + research effectiveness, stack per cause
        ResolveMitigation(ctx, tuning, seeds);
        AnimalData animal = ResolveAnimal(ctx);

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
        outcome.animal = animal;
        outcome.collectPausedWhileAway = ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.CollectMode;
        return outcome;
    }

    /// <summary>Health of a typical crop when the loss rates were tuned (most crops sat at 78-80).</summary>
    private const float AverageCropHp = 80f;

    /// <summary>Compost per plant lost in this field: only a field with a Compost Bay makes any,
    /// with the same formula as CompostBay (tier x bay power x upgrades x the crop's own bonus).</summary>
    private static int CompostPerLoss(int zone, CropData crop)
    {
        EquipmentData eq = EquipmentManager.Instance != null ? EquipmentManager.Instance.GetAssignment(zone) : null;
        if (eq == null || string.IsNullOrEmpty(eq.equipmentID) || !eq.equipmentID.Contains("compost")) return 0;
        float conversion = EquipmentManager.Instance.GetEffectiveWaterPower(eq);
        float cropMult = crop.compostMultiplier > 0f ? crop.compostMultiplier : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(crop.tier * conversion * FarmUpgrades.CompostMultiplier * cropMult));
    }

    /// <summary>What the equipped animal does during the away-run: a guard's chases (rest time
    /// with the same Research bonuses as play) or the cow's grazing (same compost per crop).
    /// Gift animals do nothing here - their eggs wait to be tapped, like in play.</summary>
    private static AnimalData ResolveAnimal(OfflineSimContext ctx)
    {
        AnimalData a = AnimalManager.Instance != null ? AnimalManager.Instance.GetEquippedAnimal() : null;
        if (a == null || a.visualPrefab == null) return null;

        var guard = a.visualPrefab.GetComponent<AnimalDefender>();
        if (guard != null)
        {
            ctx.animal = new SimAnimal
            {
                chaseCooldownSeconds = guard.BaseChaseCooldown / Mathf.Max(0.01f, guard.ChaseCooldownDivisor),
                chasesDeer = System.Array.IndexOf(guard.Chases, AnimalThreatType.Deer) >= 0,
                chasesCrows = System.Array.IndexOf(guard.Chases, AnimalThreatType.Crow) >= 0,
            };
            return a;
        }

        var cow = a.visualPrefab.GetComponent<Cow>();
        if (cow != null)
        {
            int lump = Mathf.RoundToInt(cow.BaseCompostPerEat * (1f + Bonus(StatKey.CowRunYield)));
            ctx.animal = new SimAnimal
            {
                eatIntervalSeconds = (cow.MinEatInterval + cow.MaxEatInterval) * 0.5f,
                compostPerEat = Mathf.RoundToInt(lump * FarmUpgrades.CompostMultiplier),
            };
            return a;
        }
        return null;
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

        bool hasDog = Object.FindFirstObjectByType<FarmDog>() != null; // no singletons; scan the scene
        bool hasGoose = Object.FindFirstObjectByType<FarmGoose>() != null;

        float deer = OfflineMitigation.Stack(
            OfflineMitigation.Stack(
                OfflineMitigation.Reduction(hasFence, t.fenceDeerReduction, Bonus(StatKey.FenceEffectiveness)),
                OfflineMitigation.Reduction(hasDog,   t.dogDeerReduction,   Bonus(StatKey.DogEfficiency))),
            OfflineMitigation.Reduction(hasGoose, t.gooseDeerReduction, 0f));
        float crow = OfflineMitigation.Stack(
            OfflineMitigation.Reduction(hasScarecrow, t.scarecrowCrowReduction, Bonus(StatKey.ScarecrowEffectiveness)),
            OfflineMitigation.Reduction(hasGoose, t.gooseCrowReduction, 0f));
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
