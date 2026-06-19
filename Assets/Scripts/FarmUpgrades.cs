using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime accessor for the Farm Upgrades system. Static (no scene object required) so
/// consumers work the moment the catalog assets exist. Loads FarmUpgradeData from
/// Resources/FarmUpgrades and reads effective levels from UpgradeManager
/// (permanent Coins + temporary in-run Money, already persisted).
///
/// Mirrors how gameplay reads ResearchManager.GetBonus(StatKey): every getter returns 0
/// (or the neutral default) when no levels are owned, so wiring a consumer never changes
/// behaviour until the player actually buys something.
/// </summary>
public static class FarmUpgrades
{
    private static bool loaded;
    private static readonly Dictionary<string, FarmUpgradeData> byId = new Dictionary<string, FarmUpgradeData>();
    private static readonly Dictionary<string, List<FarmUpgradeData>> byEffect = new Dictionary<string, List<FarmUpgradeData>>();

    public static void EnsureLoaded()
    {
        if (loaded) return;
        Reload();
    }

    /// <summary>Force a catalog reload (used by the editor generator and tests).</summary>
    public static void Reload()
    {
        byId.Clear();
        byEffect.Clear();

        var all = Resources.LoadAll<FarmUpgradeData>("FarmUpgrades");
        foreach (var d in all)
        {
            if (d == null || string.IsNullOrEmpty(d.upgradeID)) continue;
            if (byId.ContainsKey(d.upgradeID))
            {
                Debug.LogError($"[FarmUpgrades] Duplicate ID: {d.upgradeID}");
                continue;
            }
            byId[d.upgradeID] = d;

            if (!byEffect.TryGetValue(d.effectKey, out var list))
            {
                list = new List<FarmUpgradeData>();
                byEffect[d.effectKey] = list;
            }
            list.Add(d);
        }
        loaded = true;
    }

    public static IEnumerable<FarmUpgradeData> All()
    {
        EnsureLoaded();
        return byId.Values;
    }

    public static FarmUpgradeData Get(string id)
    {
        EnsureLoaded();
        return (id != null && byId.TryGetValue(id, out var d)) ? d : null;
    }

    /// <summary>Effective (permanent + temporary) level for an upgrade ID.</summary>
    public static int Level(string id)
    {
        return UpgradeManager.Instance != null ? UpgradeManager.Instance.GetCurrentLevel(id) : 0;
    }

    /// <summary>
    /// Additive bonus for an effect key, summed across all tracks that target it (normally one).
    /// % stats return a fraction (0.24 = +24%); buffer stats return their raw unit total.
    /// </summary>
    public static float GetBonus(string effectKey)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(effectKey) || !byEffect.TryGetValue(effectKey, out var list)) return 0f;

        float total = 0f;
        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            // Zone-targeted tracks are queried via GetZoneBonus, not the global sum.
            if (d.zoneTarget != 0) continue;
            total += d.GetBonus(Level(d.upgradeID));
        }
        return total;
    }

    /// <summary>Per-zone output bonus from that zone's Zone Level track (id "zone_level_z{n}").</summary>
    public static float GetZoneBonus(int zoneID)
    {
        EnsureLoaded();
        string id = "zone_level_z" + zoneID;
        return byId.TryGetValue(id, out var d) ? d.GetBonus(Level(id)) : 0f;
    }

    // ───────── Convenience getters for consumers ─────────

    /// <summary>Multiplier on crop growth speed (1.0 = unchanged).</summary>
    public static float GrowthMultiplier => 1f + GetBonus(FarmUpgradeKey.GrowthRate);

    /// <summary>Divisor applied to moisture depletion (water lasts longer).</summary>
    public static float MoistureRetentionDivisor => 1f + GetBonus(FarmUpgradeKey.WaterRetention);

    /// <summary>Multiplier on the dry-out grace window before HP damage begins.</summary>
    public static float DryingGraceMultiplier => 1f + GetBonus(FarmUpgradeKey.DryingGrace);

    /// <summary>Divisor on dry-out HP decay (crops survive longer while parched).</summary>
    public static float SlowDecayDivisor => 1f + GetBonus(FarmUpgradeKey.SlowDecay);

    /// <summary>Divisor on rot HP decay.</summary>
    public static float RotResistanceDivisor => 1f + GetBonus(FarmUpgradeKey.RotResistance);

    /// <summary>Multiplier on moisture added per watering action.</summary>
    public static float WateringPowerMultiplier => 1f + GetBonus(FarmUpgradeKey.WateringPower);

    /// <summary>Maximum moisture a tile can hold (100 + capacity buffer points).</summary>
    public static float MaxMoisture => 100f + GetBonus(FarmUpgradeKey.WaterCapacity);

    /// <summary>Fraction of stage-1 time skipped for new crops (0-1).</summary>
    public static float HeadStartFraction => Mathf.Clamp01(GetBonus(FarmUpgradeKey.HeadStart));

    /// <summary>Divisor on incoming threat/weather damage (crops are hardier).</summary>
    public static float HardinessDivisor => 1f + GetBonus(FarmUpgradeKey.CropHardiness);

    /// <summary>Chance (0-1) for a harvest to yield double.</summary>
    public static float BountifulChance => Mathf.Clamp01(GetBonus(FarmUpgradeKey.BountifulHarvest));

    /// <summary>Chance (0-1) to refund a seed bag on harvest.</summary>
    public static float SeedRefundChance => Mathf.Clamp01(GetBonus(FarmUpgradeKey.SeedRefund));

    /// <summary>Multiplier on compost gained.</summary>
    public static float CompostMultiplier => 1f + GetBonus(FarmUpgradeKey.CompostYield);

    /// <summary>Divisor on tilling duration (faster soil prep).</summary>
    public static float SoilPrepDivisor => 1f + GetBonus(FarmUpgradeKey.SoilPrep);

    /// <summary>
    /// Combined Cash (Money) yield multiplier for a harvest in the given zone:
    /// Fertilizer A × Soil Quality × Zone Level (multiplicative, per design).
    /// </summary>
    public static float CashYieldMultiplier(int zoneID)
    {
        return (1f + GetBonus(FarmUpgradeKey.CashYield))
             * (1f + GetBonus(FarmUpgradeKey.SoilQuality))
             * (1f + GetZoneBonus(zoneID));
    }

    /// <summary>
    /// Combined Coin (banked) yield multiplier for a harvest in the given zone:
    /// Fertilizer B × Soil Quality × Zone Level (multiplicative, per design).
    /// </summary>
    public static float CoinYieldMultiplier(int zoneID)
    {
        return (1f + GetBonus(FarmUpgradeKey.CoinYield))
             * (1f + GetBonus(FarmUpgradeKey.SoilQuality))
             * (1f + GetZoneBonus(zoneID));
    }
}
