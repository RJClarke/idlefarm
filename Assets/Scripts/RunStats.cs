using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Tracks statistics during a farming run.
/// Resets when a new run starts. Read after run ends to display results.
/// </summary>
public class RunStats : MonoBehaviour
{
    public static RunStats Instance { get; private set; }

    public event Action OnCropHarvested;
    public event Action OnSeedPlanted;
    public event Action OnPlantWatered;
    public event Action OnDeerRepelled;
    public event Action OnCrowRepelled;

    // Counters
    public int TilesTilled { get; private set; }
    public int SeedsPlanted { get; private set; }
    public int PlantsWatered { get; private set; }
    public int CropsHarvested { get; private set; }
    public int PlantsStruckByLightning { get; private set; }
    /// <summary>Per-crop harvested counts (live + offline). Key is CropData.</summary>
    public readonly Dictionary<CropData, int> HarvestedByCrop = new Dictionary<CropData, int>();
    public int PlantsDehydrated { get; private set; }
    public int CropsDecayed { get; private set; }
    public int PlantsEatenByDeer { get; private set; }
    public int PlantsEatenByCrows { get; private set; }
    public int DeerRepelledByFence { get; private set; }
    public int CrowsRepelledByScarecrow { get; private set; }
    public int MoneyEarned { get; private set; }
    public int CoinsSaved { get; private set; }
    public int CoinsBanked { get; private set; }
    public int DeerChasedByDog { get; private set; }
    public int PlantsEatenByCow { get; private set; }
    public int CompostFromCow { get; private set; }

    /// <summary>Per-zone breakdown for the run-stats zone cards. Keyed by FarmGrid ZoneID.</summary>
    public class ZoneStats
    {
        public CropData crop;    // set on first harvest/death recorded in the zone
        public int harvested, moneyEarned, coinsBanked;
        public int eatenByDeer, eatenByCrows, struckByLightning, driedUp, rotted;
        public int deerRepelledByFence, crowsRepelledByScarecrow, wateredBySprinkler;
    }

    private readonly SortedDictionary<int, ZoneStats> zoneStats = new SortedDictionary<int, ZoneStats>();
    public IReadOnlyDictionary<int, ZoneStats> ZoneStatsByZone => zoneStats;

    private ZoneStats Zone(int zoneId)
    {
        if (!zoneStats.TryGetValue(zoneId, out var z)) { z = new ZoneStats(); zoneStats[zoneId] = z; }
        return z;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnRunStarted += ResetStats;
    }

    private void OnDestroy()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnRunStarted -= ResetStats;
    }

    public void ResetStats()
    {
        TilesTilled = 0;
        SeedsPlanted = 0;
        PlantsWatered = 0;
        CropsHarvested = 0;
        PlantsStruckByLightning = 0;
        HarvestedByCrop.Clear();
        PlantsDehydrated = 0;
        CropsDecayed = 0;
        PlantsEatenByDeer = 0;
        PlantsEatenByCrows = 0;
        DeerRepelledByFence = 0;
        CrowsRepelledByScarecrow = 0;
        MoneyEarned = 0;
        CoinsSaved = 0;
        CoinsBanked = 0;
        DeerChasedByDog = 0;
        PlantsEatenByCow = 0;
        CompostFromCow = 0;
        zoneStats.Clear();
    }

    // Increment methods
    public void AddTileTilled() => TilesTilled++;
    public void AddSeedPlanted() { SeedsPlanted++; OnSeedPlanted?.Invoke(); }
    public void AddPlantWatered() { PlantsWatered++; OnPlantWatered?.Invoke(); }
    public void AddCropHarvested() { CropsHarvested++; OnCropHarvested?.Invoke(); }
    public void AddCropHarvested(CropData crop)
    {
        CropsHarvested++;
        if (crop != null)
        {
            HarvestedByCrop.TryGetValue(crop, out int n);
            HarvestedByCrop[crop] = n + 1;
        }
        OnCropHarvested?.Invoke();
    }
    /// <summary>One recorder for every unharvested plant death: bumps the aggregate cause counter
    /// AND the per-zone card. Cause strings match Plant.Die.</summary>
    public void AddPlantDeath(int zoneId, CropData crop, string cause)
    {
        var z = Zone(zoneId);
        if (crop != null) z.crop = crop;
        switch (cause)
        {
            case "dry-out":   PlantsDehydrated++;        z.driedUp++;           break;
            case "rot":       CropsDecayed++;            z.rotted++;            break;
            case "deer":      PlantsEatenByDeer++;       z.eatenByDeer++;       break;
            case "crow":      PlantsEatenByCrows++;      z.eatenByCrows++;      break;
            case "lightning": PlantsStruckByLightning++; z.struckByLightning++; break;
        }
    }

    /// <summary>Zone-side harvest record (aggregates are recorded separately at the same call
    /// site via AddCropHarvested/AddCoinsBanked — do not double count).</summary>
    public void AddZoneHarvest(int zoneId, CropData crop, int money, int coins)
    {
        var z = Zone(zoneId);
        if (crop != null) z.crop = crop;
        z.harvested++;
        z.moneyEarned += money;
        z.coinsBanked += coins;
    }

    public void AddSprinklerWatered(int zoneId) => Zone(zoneId).wateredBySprinkler++;
    public void AddDeerChasedByDog() => DeerChasedByDog++;

    public void AddCowEat(int compostLump)
    {
        PlantsEatenByCow++;
        CompostFromCow += compostLump;
    }

    /// <summary>
    /// Overwrite this run's stats from a simulated offline result (used when a run ended while away).
    /// `harvestedByCrop` maps the simulator's crop ids back to CropData. Coins/compost are the
    /// already-taxed grant amounts; losses/harvests are the raw simulated counts.
    /// </summary>
    public void IngestOfflineResult(
        Dictionary<CropData, int> harvestedByCrop,
        int eatenByDeer, int eatenByCrows, int struckByLightning, int driedUp, int rotted,
        int seedsPlanted, int moneyEarned, int coinsBanked)
    {
        ResetStats();
        HarvestedByCrop.Clear();
        int totalHarvested = 0;
        if (harvestedByCrop != null)
            foreach (var kv in harvestedByCrop) { HarvestedByCrop[kv.Key] = kv.Value; totalHarvested += kv.Value; }
        CropsHarvested = totalHarvested;
        PlantsEatenByDeer = eatenByDeer;
        PlantsEatenByCrows = eatenByCrows;
        PlantsStruckByLightning = struckByLightning;
        PlantsDehydrated = driedUp;
        CropsDecayed = rotted;
        SeedsPlanted = seedsPlanted;
        MoneyEarned = moneyEarned;
        CoinsBanked = coinsBanked;
    }
    public void AddDeerRepelled(int zoneId)
    {
        DeerRepelledByFence++;
        Zone(zoneId).deerRepelledByFence++;
        OnDeerRepelled?.Invoke();
    }

    public void AddCrowRepelled(int zoneId)
    {
        CrowsRepelledByScarecrow++;
        Zone(zoneId).crowsRepelledByScarecrow++;
        OnCrowRepelled?.Invoke();
    }
    public void AddMoneyEarned(int amount) => MoneyEarned += amount;
    public void SetCoinsSaved(int amount) => CoinsSaved = amount;
    public void AddCoinsBanked(int amount) => CoinsBanked += amount;

    /// <summary>
    /// Returns all stats as label/value pairs for display.
    /// </summary>
    public List<(string label, string value)> GetDisplayStats()
    {
        string totalTime = "0:00";
        string realTime = "0:00";
        if (RunManager.Instance != null)
        {
            totalTime = RunManager.Instance.GetFormattedRunDuration();   // speed-scaled
            realTime = RunManager.Instance.GetFormattedRealDuration();   // wall-clock
        }

        // null value = section header, empty string = blank spacer line
        return new List<(string, string)>
        {
            ("<color=#FFD700>OVERVIEW</color>", null),
            ("Total Run Time", totalTime),
            ("Real Time", realTime),
            ("Money Earned", "$" + MoneyEarned.ToString("N0")),
            ("Coins Banked", CoinsBanked.ToString("N0")),

            ("", ""),
            ("<color=#8BC34A>FARMING</color>", null),
            ("Tiles Tilled", TilesTilled.ToString()),
            ("Seeds Planted", SeedsPlanted.ToString()),
            ("Plants Watered", PlantsWatered.ToString()),
            ("Crops Harvested", CropsHarvested.ToString()),

            ("", ""),
            ("<color=#F44336>LOSSES</color>", null),
            ("Plants Dehydrated", PlantsDehydrated.ToString()),
            ("Crops Decayed", CropsDecayed.ToString()),
            ("Plants Eaten by Deer", PlantsEatenByDeer.ToString()),
            ("Plants Eaten by Crows", PlantsEatenByCrows.ToString()),

            ("", ""),
            ("<color=#2196F3>DEFENSE</color>", null),
            ("Deer Repelled by Fence", DeerRepelledByFence.ToString()),
            ("Crows Repelled by Scarecrow", CrowsRepelledByScarecrow.ToString()),
        };
    }
}
