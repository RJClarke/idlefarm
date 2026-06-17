using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One itemized harvested-crop row (icon + name + count).</summary>
public struct LedgerCropRow { public Sprite sprite; public string name; public int count; }

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
