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
    public void AddPlantStruckByLightning() => PlantsStruckByLightning++;

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
    public void AddPlantDehydrated() => PlantsDehydrated++;
    public void AddCropDecayed() => CropsDecayed++;
    public void AddPlantEatenByDeer() => PlantsEatenByDeer++;
    public void AddPlantEatenByCrow() => PlantsEatenByCrows++;
    public void AddDeerRepelledByFence() { DeerRepelledByFence++; OnDeerRepelled?.Invoke(); }
    public void AddCrowRepelledByScarecrow() { CrowsRepelledByScarecrow++; OnCrowRepelled?.Invoke(); }
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
