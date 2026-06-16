using System.Collections.Generic;

/// <summary>
/// Plain, Unity-light data types for the offline run simulator. The simulator can NOT see
/// CropData (EconomyCore is referenced by Assembly-CSharp, not the reverse), so crops are
/// passed in as SimCrop value structs; the caller maps CropData -> SimCrop and back by `id`.
/// </summary>
public struct SimCrop
{
    public string id;                  // stable lookup key (caller uses CropData.cropName)
    public float growSeconds;          // seed -> harvestable (CropData.TotalGrowthTime)
    public float harvestWindowSeconds; // 100%-value window after maturing
    public int harvestValue;           // money per harvest
    public int coinValue;              // coins banked per harvest
    public int bagBaseCost;            // seedBagBaseCost (pre-escalation)
    public int bagSize;                // seedBagSize (pre size-bonus)
    public int tier;                   // compost tier (1 = base)
}

public struct SimZone
{
    public SimCrop crop;
    public int tileCount;
}

/// <summary>All inputs the simulator needs. Caller (Plan 2) gathers these from save + live managers.</summary>
public class OfflineSimContext
{
    public float awaySeconds;          // real wall-clock gap
    public float startFarmSeconds;     // Farm Time at save (runTotalSeconds)
    public int startMoney;             // run money balance at save
    public float maxGameSpeed = 1f;    // 1 + GameSpeed research bonus (>= 1)
    public List<SimZone> zones = new List<SimZone>();

    // Resolved 0..1 research/equipment bonuses (caller computes from live state):
    public float seedBagDiscount;      // StatKey.SeedBagDiscount
    public float seedBagSizeBonus;     // StatKey.SeedBagSize
    public float deerLossReduction;    // fence + dog + threat research
    public float crowLossReduction;    // scarecrow + threat research
    public float lightningLossReduction;
    public float dryLossReduction;     // sprinkler + watering research

    public OfflineSimTuning tuning = new OfflineSimTuning();
}

/// <summary>Untaxed result of a simulated offline run. Tax is applied separately (OfflineTax).</summary>
public class OfflineRunResult
{
    public bool bankrupt;
    public float bankruptAtFarmSeconds;
    public float finalFarmSeconds;

    public Dictionary<string, int> harvestedByCropId = new Dictionary<string, int>();
    public int eatenByDeer;
    public int eatenByCrows;
    public int struckByLightning;
    public int driedUp;
    public int rotted;

    public int seedsPlanted;
    public int moneyEarned;       // gross harvest income
    public int moneySpentOnBags;
    public int finalMoney;        // ending balance (untaxed)
    public int coinsBanked;       // untaxed
    public int compostGained;     // untaxed (never taxed)

    public int TotalHarvested
    {
        get { int n = 0; foreach (var kv in harvestedByCropId) n += kv.Value; return n; }
    }
}

/// <summary>
/// Magnitude dials for the offline simulator.
///
/// LIVE-MIRRORED fields have an authoritative source on ThreatWaveManager/WeatherData. The defaults
/// below are ONLY fallbacks for unit tests — at runtime the context-builder (Plan 2) overwrites them
/// from the live managers, so inspector balancing changes flow into the offline sim with no code change.
///
/// OFFLINE-ONLY dials have no live equivalent (the live game models individual animals/strikes; offline
/// models aggregate pressure). They are the hand-tuned playtest surface.
/// </summary>
public class OfflineSimTuning
{
    public float tickSeconds = 10f;            // farm-time step; smaller = more accurate, slower

    // --- LIVE-MIRRORED from ThreatWaveManager (defaults = test fallbacks; overwritten at runtime) ---
    public float waveIntervalSeconds = 60f;
    public int deerStartWave = 1;
    public int deerCountInterval = 20;
    public int maxDeer = 6;
    public int crowStartWave = 10;
    public int crowCountInterval = 20;
    public int maxCrows = 6;
    public float baseHunger = 500f;
    public float crowBaseHunger = 80f;
    public float hungerScalePerWave = 0.01f;

    // --- LIVE-MIRRORED from WeatherData (defaults = test fallbacks; overwritten at runtime) ---
    public int stormWaveInterval = 25;
    public float lightningStrikeInterval = 8f;

    // --- OFFLINE-ONLY dials (no live equivalent; hand-tuned by playtest) ---
    public float stormLightningPhaseSeconds = 30f;  // modeled lightning window per storm
    public float lightningPlantsPerStrike = 1f;
    public float deerPlantsPerHungerSecond = 0.00002f; // plants eaten per active deer, per sec, per hunger
    public float crowPlantsPerHungerSecond = 0.00010f;
    public float dryFractionPerSecond = 0.0008f;    // fraction of growing tiles dried per second
    public float rotFractionPerSecond = 0.02f;      // applies only to over-window tiles
    public float plantsPerSecond = 0.5f;            // farm-wide (re)planting throughput
}
