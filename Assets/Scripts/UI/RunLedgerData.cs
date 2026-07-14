using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One itemized harvested-crop row (icon + name + count).</summary>
public struct LedgerCropRow { public Sprite sprite; public string name; public int count; }

/// <summary>One per-zone "field card" for the 2x2 Fields grid: the zone's crop, what it
/// produced (with worth), its five loss causes, and stats for gear equipped on the zone.</summary>
public class LedgerZoneCard
{
    public int zoneId;
    public Sprite cropSprite; public string cropName;
    public int harvested, moneyEarned, coinsBanked;
    public int eatenByDeer, eatenByCrows, struckByLightning, driedUp, rotted;
    public int? deerRepelled, crowsRepelled, wateredBySprinkler; // null = gear absent / not simulated -> hide line
}

/// <summary>
/// Render-ready data for the ledger surfaces (Run Stats popup + both welcome-back modals).
/// Built once from either the live run (FromCurrentRun) or a simulated offline outcome (FromOffline).
/// </summary>
public class RunLedgerData
{
    public string farmTimeHms = "0s";
    public string realTimeHms = "0s";
    public bool bankrupt;
    public bool offlineTaxApplied;

    public int moneyEarned, moneySpentOnBags, coinsBanked, compostGained, resumeMoney;
    public bool hasResumeMoney; // true on the survived-offline path (shows "Money now")

    public readonly List<LedgerCropRow> harvested = new List<LedgerCropRow>();
    public int totalHarvested;

    public int eatenByDeer, eatenByCrows, struckByLightning, driedUp, rotted;
    public int deerRepelled, crowsRepelled;
    public bool hasDefense; // false offline (defense not simulated) -> hide the section

    // Per-zone field cards (2x2 grid, ordered by zoneId to mirror the farm layout).
    public readonly List<LedgerZoneCard> zoneCards = new List<LedgerZoneCard>();

    // Animals (live only; offline never simulates them).
    public bool hasDog; public int deerChasedByDog; public Sprite dogSprite;
    public bool hasCow; public int plantsEatenByCow, compostFromCow; public Sprite cowSprite;

    /// <summary>Build from the just-ended live run (RunStats + RunManager).</summary>
    public static RunLedgerData FromCurrentRun()
    {
        var d = new RunLedgerData();
        var rm = RunManager.Instance;
        var rs = RunStats.Instance;
        if (rm != null)
        {
            d.farmTimeHms = TimeFormat.Hms(rm.LastRunSurvivedSeconds);
            d.realTimeHms = TimeFormat.Hms(rm.LastRunRealSeconds);
            d.bankrupt = rm.LastRunEndedBankrupt;
        }
        if (rs != null)
        {
            d.moneyEarned = rs.MoneyEarned;
            d.coinsBanked = rs.CoinsBanked;
            d.eatenByDeer = rs.PlantsEatenByDeer;
            d.eatenByCrows = rs.PlantsEatenByCrows;
            d.struckByLightning = rs.PlantsStruckByLightning;
            d.driedUp = rs.PlantsDehydrated;
            d.rotted = rs.CropsDecayed;
            d.deerRepelled = rs.DeerRepelledByFence;
            d.crowsRepelled = rs.CrowsRepelledByScarecrow;
            d.hasDefense = true;
            foreach (var kv in rs.HarvestedByCrop)
                AddCrop(d, kv.Key, kv.Value);
            d.totalHarvested = rs.CropsHarvested;

            foreach (var kv in rs.ZoneStatsByZone)
            {
                var z = kv.Value;
                var card = new LedgerZoneCard
                {
                    zoneId = kv.Key,
                    cropSprite = z.crop != null ? (z.crop.cropSprite != null ? z.crop.cropSprite : z.crop.seedPacketSprite) : null,
                    cropName = z.crop != null ? z.crop.cropName : $"Zone {kv.Key}",
                    harvested = z.harvested, moneyEarned = z.moneyEarned, coinsBanked = z.coinsBanked,
                    eatenByDeer = z.eatenByDeer, eatenByCrows = z.eatenByCrows,
                    struckByLightning = z.struckByLightning, driedUp = z.driedUp, rotted = z.rotted,
                };
                // Equipment lines only for gear assigned to this zone (assignments persist post-run).
                var eq = EquipmentManager.Instance != null ? EquipmentManager.Instance.GetAssignment(kv.Key) : null;
                if (eq != null)
                {
                    if (eq.equipmentType == EquipmentType.Fence)     card.deerRepelled = z.deerRepelledByFence;
                    if (eq.equipmentType == EquipmentType.Scarecrow) card.crowsRepelled = z.crowsRepelledByScarecrow;
                    if (eq.equipmentType == EquipmentType.Sprinkler) card.wateredBySprinkler = z.wateredBySprinkler;
                }
                d.zoneCards.Add(card);
            }

            // Animals: show the equipped animal even at zero, plus anything that recorded counts.
            string equippedAnimal = AnimalManager.Instance != null ? AnimalManager.Instance.GetEquippedAnimalID() : null;
            d.deerChasedByDog = rs.DeerChasedByDog;
            d.hasDog = rs.DeerChasedByDog > 0 || equippedAnimal == "farm_dog";
            d.plantsEatenByCow = rs.PlantsEatenByCow;
            d.compostFromCow = rs.CompostFromCow;
            d.hasCow = rs.PlantsEatenByCow > 0 || rs.CompostFromCow > 0 || equippedAnimal == "cow";
            if (d.compostGained == 0) d.compostGained = rs.CompostFromCow; // Economy line, live path
            if (AnimalManager.Instance != null)
            {
                var dog = AnimalManager.Instance.GetAnimalData("farm_dog");
                var cow = AnimalManager.Instance.GetAnimalData("cow");
                d.dogSprite = dog != null ? dog.iconSprite : null;
                d.cowSprite = cow != null ? cow.iconSprite : null;
            }
        }
        return d;
    }

    /// <summary>
    /// Build a snapshot of the run in progress (for opening Run Stats mid-run). Same live RunStats
    /// ledger as <see cref="FromCurrentRun"/>, but the hero time reflects the CURRENT run's elapsed
    /// duration (not the last-ended run's), and it's never flagged bankrupt.
    /// </summary>
    public static RunLedgerData FromActiveRun()
    {
        var d = FromCurrentRun();
        var rm = RunManager.Instance;
        if (rm != null && rm.IsRunActive)
        {
            d.farmTimeHms = TimeFormat.Hms(rm.CurrentRunDuration);
            d.realTimeHms = TimeFormat.Hms(rm.RealRunDuration);
            d.bankrupt = false;
        }
        return d;
    }

    /// <summary>Build from a simulated offline outcome (already taxed payouts).</summary>
    public static RunLedgerData FromOffline(OfflineRunOutcome o, TimeSpan gap)
    {
        var d = new RunLedgerData { offlineTaxApplied = true, hasDefense = false };
        d.farmTimeHms = TimeFormat.Hms(o.result.finalFarmSeconds);
        d.realTimeHms = TimeFormat.Hms((float)gap.TotalSeconds);
        d.bankrupt = o.result.bankrupt;
        d.moneyEarned = o.result.moneyEarned;
        d.moneySpentOnBags = o.result.moneySpentOnBags;
        d.coinsBanked = o.taxedCoins;
        d.compostGained = o.compostGranted;
        d.resumeMoney = o.taxedResumeMoney;
        d.hasResumeMoney = !o.result.bankrupt;
        d.eatenByDeer = o.result.eatenByDeer;
        d.eatenByCrows = o.result.eatenByCrows;
        d.struckByLightning = o.result.struckByLightning;
        d.driedUp = o.result.driedUp;
        d.rotted = o.result.rotted;
        foreach (var kv in o.harvestedByCrop) AddCrop(d, kv.Key, kv.Value);
        d.totalHarvested = o.result.TotalHarvested;

        // Per-zone cards from the sim's zone breakdown. Equipment/animal fields stay null/false —
        // the offline sim models them as loss-reduction only, never as counted events.
        foreach (var zs in o.result.zones)
        {
            CropData crop = zs.cropId != null && o.cropById.TryGetValue(zs.cropId, out var c) ? c : null;
            d.zoneCards.Add(new LedgerZoneCard
            {
                zoneId = zs.zoneId,
                cropSprite = crop != null ? (crop.cropSprite != null ? crop.cropSprite : crop.seedPacketSprite) : null,
                cropName = crop != null ? crop.cropName : $"Zone {zs.zoneId}",
                harvested = zs.harvested, moneyEarned = zs.moneyEarned, coinsBanked = zs.coinsBanked,
                eatenByDeer = zs.eatenByDeer, eatenByCrows = zs.eatenByCrows,
                struckByLightning = zs.struckByLightning, driedUp = zs.driedUp, rotted = zs.rotted,
            });
        }
        return d;
    }

    private static void AddCrop(RunLedgerData d, CropData crop, int count)
    {
        if (crop == null || count <= 0) return;
        d.harvested.Add(new LedgerCropRow
        {
            sprite = crop.cropSprite != null ? crop.cropSprite : crop.seedPacketSprite,
            name = crop.cropName,
            count = count
        });
    }
}
