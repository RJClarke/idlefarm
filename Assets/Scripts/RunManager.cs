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
        Debug.Log("=== TOWN MODE - Click 'Start New Run' to begin ===");
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
    /// Start a new farming run
    /// MODIFIED: Now shows seed selection popup first
    /// </summary>
    public void StartNewRun()
    {
        if (isRunActive)
        {
            Debug.LogWarning("Run is already active! End current run before starting new one.");
            return;
        }

        // Show seed selection popup
        if (SeedSelectionPopup.Instance != null)
        {
            // Subscribe to popup events
            SeedSelectionPopup.Instance.OnSeedsConfirmed += OnSeedsConfirmed;
            SeedSelectionPopup.Instance.OnCancelled += OnSeedSelectionCancelled;
            
            SeedSelectionPopup.Instance.Show();
            
            Debug.Log("🌱 Showing seed selection popup...");
        }
        else
        {
            Debug.LogWarning("SeedSelectionPopup not found! Starting run without seed selection.");
            ActuallyStartRun(null);
        }
    }

    /// <summary>
    /// Called when player confirms seed selection
    /// </summary>
    private void OnSeedsConfirmed(Dictionary<int, CropData> zoneSeeds)
    {
        // Unsubscribe from popup events
        if (SeedSelectionPopup.Instance != null)
        {
            SeedSelectionPopup.Instance.OnSeedsConfirmed -= OnSeedsConfirmed;
            SeedSelectionPopup.Instance.OnCancelled -= OnSeedSelectionCancelled;
        }

        // Pass seeds to HelperManager
        if (HelperManager.Instance != null)
        {
            HelperManager.Instance.SetZoneSeeds(zoneSeeds);
        }

        // Actually start the run
        ActuallyStartRun(zoneSeeds);
    }

    /// <summary>
    /// Called when player cancels seed selection
    /// </summary>
    private void OnSeedSelectionCancelled()
    {
        // Unsubscribe from popup events
        if (SeedSelectionPopup.Instance != null)
        {
            SeedSelectionPopup.Instance.OnSeedsConfirmed -= OnSeedsConfirmed;
            SeedSelectionPopup.Instance.OnCancelled -= OnSeedSelectionCancelled;
        }

        Debug.Log("🌱 Seed selection cancelled - run not started");
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

        // Calculate rewards
        int coinsEarned = CalculateRunRewards();

        // Award coins
        if (CurrencyManager.Instance != null && coinsEarned > 0)
        {
            CurrencyManager.Instance.AddCoins(coinsEarned);
        }

        // Notify other systems that run has ended
        OnRunEnded?.Invoke();

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

            Debug.Log($"Leftover Money: ${leftoverMoney} → {coinsFromMoney} coins (ratio: {coinsPerMoneyRatio}:1)");
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