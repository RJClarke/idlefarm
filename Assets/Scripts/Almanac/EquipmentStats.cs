using UnityEngine;

/// <summary>
/// The one place equipment numbers are computed. EquipmentManager's GetEffective* (the gameplay
/// path, called every frame for sprinklers) return the allocation-free float versions; the Almanac
/// reads the *Line versions, built from the same pieces, as base / Upgrades / Research / Barn skills.
/// </summary>
public static class EquipmentStats
{
    public const string FromUpgrades = "Upgrades";
    public const string FromResearch = "Research";
    public const string FromSkills = "Barn skills";

    public static int Level(string upgradeId) =>
        string.IsNullOrEmpty(upgradeId) || UpgradeManager.Instance == null ? 0 : UpgradeManager.Instance.GetCurrentLevel(upgradeId);

    private static float Res(string key) =>
        string.IsNullOrEmpty(key) || ResearchManager.Instance == null ? 0f : ResearchManager.Instance.GetBonus(key);

    private static float SkillReach(EquipmentData d) =>
        d.equipmentType == EquipmentType.Sprinkler ? FarmSkillsManager.SprinklerReachMultiplier : 1f;

    // ── Reach (area radius) ──
    public static float Reach(EquipmentData d) =>
        (d.baseAoERadius + Level(d.aoeUpgradeID) * d.aoeBonusPerLevel) * (1f + Res(d.aoeUpgradeID)) * SkillReach(d);

    public static StatLine ReachLine(EquipmentData d) =>
        new StatLine("Reach", d.baseAoERadius, StatFormat.Tiles)
            .Add(FromUpgrades, Level(d.aoeUpgradeID) * d.aoeBonusPerLevel)
            .Multiply(FromResearch, 1f + Res(d.aoeUpgradeID))
            .Multiply(FromSkills, SkillReach(d));

    // ── Cooldown ──
    public static float Cooldown(EquipmentData d)
    {
        float cd = d.baseCooldownSeconds - Level(d.cooldownUpgradeID) * d.cooldownReductionPerLevel;
        cd /= Mathf.Max(0.01f, 1f + Res(d.cooldownUpgradeID));
        return Mathf.Max(cd, d.minCooldownSeconds);
    }

    public static StatLine CooldownLine(EquipmentData d) =>
        new StatLine("Cooldown", d.baseCooldownSeconds, StatFormat.Seconds)
            .Add(FromUpgrades, -Level(d.cooldownUpgradeID) * d.cooldownReductionPerLevel)
            .Divide(FromResearch, Mathf.Max(0.01f, 1f + Res(d.cooldownUpgradeID)))
            .AtLeast(d.minCooldownSeconds);

    // ── Capacity (pests handled per use) ──
    public static int Capacity(EquipmentData d) =>
        Mathf.RoundToInt((d.baseRepelCapacity + Level(d.capacityUpgradeID) * d.capacityBonusPerLevel) * (1f + Res(d.capacityUpgradeID)));

    public static StatLine CapacityLine(EquipmentData d) =>
        new StatLine("Pests scared per use", d.baseRepelCapacity, StatFormat.Number)
            .Add(FromUpgrades, Level(d.capacityUpgradeID) * d.capacityBonusPerLevel)
            .Multiply(FromResearch, 1f + Res(d.capacityUpgradeID), roundToInt: true);

    // ── Water power (sprinklers; the Compost Bay reuses this channel as its conversion rate) ──
    public static float WaterPower(EquipmentData d) =>
        (d.baseMoisturePowerPerSecond + Level(d.waterPowerUpgradeID) * d.waterPowerBonusPerLevel) * (1f + Res(d.waterPowerUpgradeID));

    public static StatLine WaterPowerLine(EquipmentData d, string label = "Water per second") =>
        new StatLine(label, d.baseMoisturePowerPerSecond, StatFormat.Number)
            .Add(FromUpgrades, Level(d.waterPowerUpgradeID) * d.waterPowerBonusPerLevel)
            .Multiply(FromResearch, 1f + Res(d.waterPowerUpgradeID));

    public static StatLine ActiveTimeLine(EquipmentData d) =>
        new StatLine("Sprays for", d.activeDurationSeconds, StatFormat.Seconds);

    // ── Fence coverage (fence level = 1 + its area-upgrade level) ──
    public static StatLine FenceCoverageLine(EquipmentData d)
    {
        float baseCov = d.GetFenceCoverage(1);
        float cov = d.GetFenceCoverage(1 + Level(d.aoeUpgradeID));
        return new StatLine("Field covered", baseCov, StatFormat.Percent).Add(FromUpgrades, cov - baseCov);
    }
}
