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
    [SerializeField] private float runStartTime = 0f;
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
        // Track run duration if a run is active
        if (isRunActive)
        {
            currentRunDuration = Time.time - runStartTime;
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
        runStartTime = Time.time;
        currentRunDuration = 0f;

        // Reset money for new run
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.ResetMoneyForNewRun();
        }

        // Apply Game Speed Multiplier research bonus to Time.timeScale.
        // ResearchManager.Tick uses unscaledDeltaTime so research timers are unaffected.
        // Animal passives use UtcNow so they're also unaffected. Game Speed only touches in-run mechanics.
        float gameSpeedBonus = ResearchManager.Instance != null
            ? ResearchManager.Instance.GetBonus(Research.StatKey.GameSpeed)
            : 0f;
        Time.timeScale = 1f + gameSpeedBonus;

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
    /// End the current run and calculate rewards
    /// </summary>
    public void EndRun()
    {
        if (!isRunActive)
        {
            Debug.LogWarning("No active run to end!");
            return;
        }

        isRunActive = false;

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
        int totalCoins = 0;

        if (CurrencyManager.Instance != null && giveCoinsForLeftoverMoney)
        {
            // Convert leftover money to coins
            int leftoverMoney = CurrencyManager.Instance.Money;
            int coinsFromMoney = leftoverMoney / coinsPerMoneyRatio;
            totalCoins += coinsFromMoney;

            // Leftover money converted to coins
        }

        // In future chunks, we'll add:
        // - Bonus coins for crops harvested
        // - Bonus coins for run duration
        // - Multipliers from upgrades

        return totalCoins;
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