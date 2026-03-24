using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tracks statistics during a farming run.
/// Resets when a new run starts. Read after run ends to display results.
/// </summary>
public class RunStats : MonoBehaviour
{
    public static RunStats Instance { get; private set; }

    // Counters
    public int TilesTilled { get; private set; }
    public int SeedsPlanted { get; private set; }
    public int PlantsWatered { get; private set; }
    public int CropsHarvested { get; private set; }
    public int PlantsDehydrated { get; private set; }
    public int CropsDecayed { get; private set; }
    public int PlantsEatenByDeer { get; private set; }
    public int PlantsEatenByCrows { get; private set; }
    public int DeerRepelledByFence { get; private set; }
    public int CrowsRepelledByScarecrow { get; private set; }
    public int MoneyEarned { get; private set; }
    public int CoinsSaved { get; private set; }

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
        PlantsDehydrated = 0;
        CropsDecayed = 0;
        PlantsEatenByDeer = 0;
        PlantsEatenByCrows = 0;
        DeerRepelledByFence = 0;
        CrowsRepelledByScarecrow = 0;
        MoneyEarned = 0;
        CoinsSaved = 0;
    }

    // Increment methods
    public void AddTileTilled() => TilesTilled++;
    public void AddSeedPlanted() => SeedsPlanted++;
    public void AddPlantWatered() => PlantsWatered++;
    public void AddCropHarvested() => CropsHarvested++;
    public void AddPlantDehydrated() => PlantsDehydrated++;
    public void AddCropDecayed() => CropsDecayed++;
    public void AddPlantEatenByDeer() => PlantsEatenByDeer++;
    public void AddPlantEatenByCrow() => PlantsEatenByCrows++;
    public void AddDeerRepelledByFence() => DeerRepelledByFence++;
    public void AddCrowRepelledByScarecrow() => CrowsRepelledByScarecrow++;
    public void AddMoneyEarned(int amount) => MoneyEarned += amount;
    public void SetCoinsSaved(int amount) => CoinsSaved = amount;

    /// <summary>
    /// Returns all stats as label/value pairs for display.
    /// </summary>
    public List<(string label, string value)> GetDisplayStats()
    {
        string runTime = "0:00";
        if (RunManager.Instance != null)
            runTime = RunManager.Instance.GetFormattedRunDuration();

        // null value = section header, empty string = blank spacer line
        return new List<(string, string)>
        {
            ("<color=#FFD700>OVERVIEW</color>", null),
            ("Run Time", runTime),
            ("Money Earned", "$" + MoneyEarned.ToString("N0")),
            ("Coins Saved", CoinsSaved.ToString("N0")),

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
