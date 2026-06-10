using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Manages the lifecycle of a farming run
/// Handles starting runs, ending runs, and tracking run state
/// MODIFIED: Now integrates with seed selection popup
/// </summary>
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [Header("Run State")]
    [SerializeField] private bool isRunActive = false;
    // Wall-clock anchored: keeps ticking through app close + resumes at "8 hours of difficulty".
    // Stored as ticks so SaveManager can round-trip it. 0 = no run.
    [SerializeField] private long runStartUtcTicks = 0;
    [SerializeField] private float currentRunDuration = 0f;

    [Header("Run Rewards")]
    [SerializeField] private int coinsPerMoneyRatio = 10; // 10 money = 1 coin
    [SerializeField] private bool giveCoinsForLeftoverMoney = true;

    // Events for other systems to respond to
    public event Action OnRunStarted;
    public event Action OnRunEnded;

    // Properties
    public bool IsRunActive => isRunActive;
    public float CurrentRunDuration => currentRunDuration;
    public long RunStartUtcTicks => runStartUtcTicks;

    // Set on EndRun for the end-of-run screen.
    public bool LastRunEndedBankrupt { get; private set; }
    public bool LastRunWasRecord { get; private set; }
    public int LastRunSurvivedSeconds { get; private set; }
    public int BestRunSeconds => PlayerPrefs.GetInt("best_run_seconds", 0);

    private void Awake()
    {
        // Singleton pattern
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
        // Start in Town Mode (between runs)
        // Player must click "Start New Run" button to begin playing
        // Town Mode - player must click "Start New Run" to begin
    }

    private void Update()
    {
        // Wall-clock anchored — works through close/reopen and ignores Time.timeScale, so
        // a run resumed after 8h offline reports 8h of difficulty.
        if (isRunActive && runStartUtcTicks > 0)
        {
            long deltaTicks = DateTime.UtcNow.Ticks - runStartUtcTicks;
            currentRunDuration = (float)(deltaTicks / (double)TimeSpan.TicksPerSecond);
        }
    }

    /// <summary>
    /// Start a new farming run using saved field configuration.
    /// Player should configure fields via the "Equip Fields" popup before starting.
    /// </summary>
    public void StartNewRun()
    {
        if (isRunActive)
        {
            Debug.LogWarning("Run is already active! End current run before starting new one.");
            return;
        }

        Dictionary<int, CropData> zoneSeeds = null;

        if (SeedSelectionPopup.Instance != null)
        {
            if (!SeedSelectionPopup.Instance.IsReadyToRun())
            {
                Debug.LogWarning("Not all zones configured! Open Equip Fields first.");
                return;
            }

            zoneSeeds = SeedSelectionPopup.Instance.LoadAndApplySavedSelections();
        }

        // Pass seeds to HelperManager
        if (HelperManager.Instance != null && zoneSeeds != null)
        {
            HelperManager.Instance.SetZoneSeeds(zoneSeeds);
        }

        ActuallyStartRun(zoneSeeds);
    }

    /// <summary>
    /// Actually start the run (called after seed selection confirmed)
    /// </summary>
    private void ActuallyStartRun(Dictionary<int, CropData> zoneSeeds)
    {
        isRunActive = true;
        runStartUtcTicks = DateTime.UtcNow.Ticks;
        currentRunDuration = 0f;

        // Reset money for new run
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.ResetMoneyForNewRun();
        }

        ApplyGameSpeedScale();

        // Notify other systems that run has started
        OnRunStarted?.Invoke();

        if (zoneSeeds != null)
        {
            Debug.Log($"=== NEW RUN STARTED with {zoneSeeds.Count} zones configured ===");
        }
        else
        {
            Debug.Log("=== NEW RUN STARTED (no seed selection) ===");
        }
    }

    /// <summary>
    /// Resume an in-progress run from a saved start timestamp. Used by SaveManager.LoadGame
    /// when the save file shows runActive=true. Does NOT reset Money (caller restores it
    /// from save), does NOT prompt for seed selection (the run was already configured).
    /// Tactical state — tiles, planted crops, helper task queues, threats in flight — is
    /// NOT restored; it resets to a fresh start of a long-running run.
    ///
    /// `lastOnlineUtcTicks`: if &gt; 0, the offline window from then → now is credited at
    /// max game speed for the purpose of difficulty advance. (Game speed = "play faster";
    /// while away we assume the player would have had max speed on the whole time.)
    /// Pass 0 to skip the offline boost (fresh new run, no save anchor available, etc.).
    /// </summary>
    public void ResumeRun(long startUtcTicks, long lastOnlineUtcTicks = 0L)
    {
        if (isRunActive)
        {
            Debug.LogWarning("ResumeRun called but a run is already active; ignoring.");
            return;
        }
        if (startUtcTicks <= 0)
        {
            Debug.LogWarning("ResumeRun called with invalid startUtcTicks; ignoring.");
            return;
        }

        isRunActive = true;
        runStartUtcTicks = startUtcTicks;

        // Offline difficulty boost: credit the offline window at max game speed.
        // We move runStartUtcTicks BACKWARD by the bonus seconds so CurrentRunDuration
        // (= now - runStartUtcTicks) reports the inflated value. ThreatWaveManager and
        // any other CurrentRunDuration consumer pick this up automatically.
        long nowTicks = DateTime.UtcNow.Ticks;
        if (lastOnlineUtcTicks > 0 && lastOnlineUtcTicks < nowTicks)
        {
            double offlineSecs = (nowTicks - lastOnlineUtcTicks) / (double)TimeSpan.TicksPerSecond;
            float maxSpeed = 1f + (ResearchManager.Instance != null
                ? ResearchManager.Instance.GetBonus(Research.StatKey.GameSpeed)
                : 0f);
            double bonusSecs = offlineSecs * Mathf.Max(0f, maxSpeed - 1f);
            if (bonusSecs > 0)
            {
                runStartUtcTicks -= (long)(bonusSecs * TimeSpan.TicksPerSecond);
                Debug.Log($"=== Offline difficulty boost: +{bonusSecs:F0}s (offline={offlineSecs:F0}s × {maxSpeed:F2}× max speed) ===");
            }
        }

        currentRunDuration = (float)((nowTicks - runStartUtcTicks) / (double)TimeSpan.TicksPerSecond);

        // Pull the saved seed selection so HelperManager has zones to work.
        Dictionary<int, CropData> zoneSeeds = null;
        if (SeedSelectionPopup.Instance != null)
            zoneSeeds = SeedSelectionPopup.Instance.LoadAndApplySavedSelections();

        if (HelperManager.Instance != null && zoneSeeds != null)
            HelperManager.Instance.SetZoneSeeds(zoneSeeds);

        ApplyGameSpeedScale();
        OnRunStarted?.Invoke(); // listeners (UpgradeManager, HelperManager, ThreatWaveManager) re-init

        Debug.Log($"=== RUN RESUMED at {currentRunDuration:F0}s of difficulty ===");
    }

    private void ApplyGameSpeedScale()
    {
        // Apply Game Speed Multiplier research bonus to Time.timeScale.
        // ResearchManager.Tick uses unscaledDeltaTime so research timers are unaffected.
        // Animal passives use UtcNow so they're also unaffected. Game Speed only touches in-run mechanics.
        float gameSpeedBonus = ResearchManager.Instance != null
            ? ResearchManager.Instance.GetBonus(Research.StatKey.GameSpeed)
            : 0f;
        Time.timeScale = 1f + gameSpeedBonus;
    }

    /// <summary>
    /// End the current run and calculate rewards
    /// </summary>
    public void EndRun(bool bankrupt = false)
    {
        if (!isRunActive)
        {
            Debug.LogWarning("No active run to end!");
            return;
        }

        isRunActive = false;
        runStartUtcTicks = 0;

        // Best-survived-time record (PlayerPrefs, matching the project's split-persistence pattern).
        int survivedSecs = Mathf.FloorToInt(currentRunDuration);
        int prevBest = PlayerPrefs.GetInt("best_run_seconds", 0);
        LastRunWasRecord = bankrupt && survivedSecs > prevBest;
        if (survivedSecs > prevBest)
        {
            PlayerPrefs.SetInt("best_run_seconds", survivedSecs);
            PlayerPrefs.Save();
        }
        LastRunSurvivedSeconds = survivedSecs;
        LastRunEndedBankrupt = bankrupt;

        // Reset Time.timeScale (Game Speed Multiplier only applies during runs)
        Time.timeScale = 1f;

        // Calculate rewards
        int coinsEarned = CalculateRunRewards();

        // Award coins
        if (CurrencyManager.Instance != null && coinsEarned > 0)
        {
            CurrencyManager.Instance.AddCoins(coinsEarned);
        }

        // Record coins saved in stats before resetting
        if (RunStats.Instance != null)
            RunStats.Instance.SetCoinsSaved(coinsEarned);

        // Notify other systems that run has ended
        OnRunEnded?.Invoke();

        // Show stats popup
        if (RunStatsPopup.Instance != null)
            RunStatsPopup.Instance.Show();

        // Save the game after run ends
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveGame();
        }

        Debug.Log($"=== RUN ENDED ===");
        Debug.Log($"Run Duration: {FormatTime(currentRunDuration)}");
        Debug.Log($"Coins Earned: {coinsEarned}");

        currentRunDuration = 0f;
    }

    /// <summary>
    /// Calculate how many coins the player earned this run
    /// </summary>
    private int CalculateRunRewards()
    {
        // Coins are now banked per-harvest during the run (see Plant.Harvest).
        // Leftover money is operational fuel and is intentionally discarded at run end.
        return 0;
    }

    /// <summary>
    /// Format time in seconds to readable format (MM:SS)
    /// </summary>
    private string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return $"{minutes:00}:{secs:00}";
    }

    /// <summary>
    /// Get formatted run duration for display
    /// </summary>
    public string GetFormattedRunDuration()
    {
        return FormatTime(currentRunDuration);
    }

    #region Testing/Debug Methods

    [ContextMenu("Start New Run")]
    private void TestStartRun()
    {
        StartNewRun();
    }

    [ContextMenu("End Current Run")]
    private void TestEndRun()
    {
        EndRun();
    }

    [ContextMenu("Add Test Money (500)")]
    private void TestAddMoney()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.AddMoney(500);
        }
    }

    #endregion
}