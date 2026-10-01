using Research;
using UnityEngine;

/// <summary>
/// The one place a crop's live numbers are computed. Gameplay (Plant) and the Farmer's Almanac both
/// read these builders, so what the book says is what happens in a run. Each returns a StatLine:
/// base value, then every bonus source in the order gameplay applies it, then the total.
///
/// Source labels are what the player sees: "Research", "Barn skills", "Farm upgrades".
/// Field-dependent values (zone level upgrades) take a zone; the Almanac shows Field 1.
/// </summary>
public static class CropStats
{
    public const string FromResearch = "Research";
    public const string FromSkills = "Barn skills";
    public const string FromUpgrades = "Farm upgrades";

    private static float R(string key) => ResearchManager.Instance != null ? ResearchManager.Instance.GetBonus(key) : 0f;
    private static float Skill(FarmSkillTrack t) => FarmSkillsManager.Instance != null ? FarmSkillsManager.Instance.GetBonus(t) : 0f;

    /// <summary>Money per harvest before the random Bountiful/Golden rolls. Plant passes the
    /// rot-adjusted value as <paramref name="startValue"/>; the Almanac uses the crop's base.</summary>
    public static StatLine Money(CropData crop, int zone = 1, int? startValue = null)
    {
        float sell = R(StatKey.CropBonusSellAmount) + R(StatKey.SoilQuality) + R(StatKey.HelperHarvestEfficiency);
        return new StatLine("Money per harvest", startValue ?? crop.harvestValue, StatFormat.Money)
            .Multiply(FromResearch, 1f + sell, roundToInt: true)
            .Multiply(FromSkills, 1f + Skill(FarmSkillTrack.Harvesting), roundToInt: true)
            .Multiply(FromUpgrades, FarmUpgrades.CashYieldMultiplier(zone), roundToInt: true);
    }

    /// <summary>Coins banked per harvest before the random Bountiful/Golden rolls (min 1 applied by Plant).</summary>
    public static StatLine Coins(CropData crop, int zone = 1)
    {
        return new StatLine("Coins per harvest", crop.coinValue, StatFormat.Coins)
            .Multiply(FromResearch, 1f + R(StatKey.CropBonusCoinAmount), roundToInt: true)
            .Multiply(FromUpgrades, FarmUpgrades.CoinYieldMultiplier(zone), roundToInt: true);
    }

    public static StatLine Health(CropData crop)
    {
        return new StatLine("Health", crop.maxHP, StatFormat.Number)
            .Multiply(FromResearch, 1f + R(StatKey.CropHp));
    }

    /// <summary>Growth speed multiplier from bonuses (moisture's own speed-up is applied by Plant).</summary>
    public static float GrowthSpeedMultiplier() =>
        (1f + R(StatKey.CropGrowthSpeed)) * (1f + Skill(FarmSkillTrack.Planting)) * FarmUpgrades.GrowthMultiplier;

    /// <summary>Seed to harvest at normal moisture: Head Start trims the seed stage, then speed bonuses divide.</summary>
    public static StatLine GrowTime(CropData crop)
    {
        return new StatLine("Grows in", crop.TotalGrowthTime, StatFormat.Seconds)
            .Add(FromUpgrades, -crop.seedSeconds * FarmUpgrades.HeadStartFraction)
            .Divide(FromResearch, 1f + R(StatKey.CropGrowthSpeed))
            .Divide(FromSkills, 1f + Skill(FarmSkillTrack.Planting))
            .Divide(FromUpgrades, FarmUpgrades.GrowthMultiplier);
    }

    /// <summary>One growth stage's length with the same bonuses as GrowTime (Head Start trims the
    /// seed stage, then speed bonuses divide), so the stages add up to "Grows in".</summary>
    public static float StageSeconds(CropData crop, GrowthStage stage)
    {
        float t = crop.GetStageTime(stage);
        if (stage == GrowthStage.Seed) t *= 1f - FarmUpgrades.HeadStartFraction;
        return t / Mathf.Max(0.01f, GrowthSpeedMultiplier());
    }

    /// <summary>Harvest-to-harvest time for a regrowing crop, with growth bonuses.</summary>
    public static float RegrowSeconds(CropData crop) => crop.RegrowTime / Mathf.Max(0.01f, GrowthSpeedMultiplier());

    public static StatLine HarvestWindow(CropData crop) =>
        new StatLine("Harvest window", crop.harvestWindowSeconds, StatFormat.Seconds);

    // Thirst divisors, shared by the per-frame float path and the Almanac StatLine.
    private static float ThirstResearchDivisor() => Mathf.Max(0.01f, 1f + R(StatKey.SoilWaterEfficiency));
    private static float ThirstUpgradeDivisor() => Mathf.Max(0.01f, FarmUpgrades.MoistureRetentionDivisor);
    private static float ThirstSkillDivisor() => Mathf.Max(0.01f, 1f + Skill(FarmSkillTrack.Watering));

    /// <summary>Thirst: multiplier on how fast the soil under this crop dries (lower = water lasts longer).</summary>
    public static StatLine Thirst(CropData crop)
    {
        return new StatLine("Water use", crop.moistureDepletionRate, StatFormat.Multiplier)
            .Divide(FromResearch, ThirstResearchDivisor())
            .Divide(FromUpgrades, ThirstUpgradeDivisor())
            .Divide(FromSkills, ThirstSkillDivisor());
    }

    /// <summary>Allocation-free Thirst total for Plant's per-frame moisture drain.</summary>
    public static float ThirstTotal(CropData crop) =>
        crop.moistureDepletionRate / ThirstResearchDivisor() / ThirstUpgradeDivisor() / ThirstSkillDivisor();

    public static StatLine SeedsPerBag(CropData crop)
    {
        int size = SeedEconomy.BagSize(crop.seedBagSize, R(StatKey.SeedBagSize));
        return new StatLine("Seeds per bag", crop.seedBagSize, StatFormat.Number).Add(FromResearch, size - crop.seedBagSize);
    }

    /// <summary>Bag price at the start of a run (it rises the longer a run goes).</summary>
    public static StatLine BagCost(CropData crop)
    {
        int cost = SeedEconomy.BagCost(crop.seedBagBaseCost, 0f, R(StatKey.SeedBagDiscount));
        return new StatLine("Seed bag price", crop.seedBagBaseCost, StatFormat.Money).Add(FromResearch, cost - crop.seedBagBaseCost);
    }
}
