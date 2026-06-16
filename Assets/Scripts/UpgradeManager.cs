using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Manages all upgrade levels (permanent and temporary)
/// Handles dual-progression system: Coins for permanent, Money for temporary
/// </summary>
public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    private Dictionary<string, int> permanentLevels = new Dictionary<string, int>();
    private Dictionary<string, int> temporaryLevels = new Dictionary<string, int>();

    [Header("Money Scaling Settings")]
    [Tooltip("Money cost multiplier (2.2 = Double + Tax)")]
    [SerializeField] private float moneyScalingMultiplier = 2.2f;

    // Event that fires when any upgrade is purchased (permanent or temporary)
    public event Action<string> OnUpgradePurchased;

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
        // Subscribe to run events
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += OnRunStarted;
        }
    }

    private void OnDestroy()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= OnRunStarted;
        }
    }

    // ───────── Save / Load ─────────

    /// <summary>Serializable snapshot of permanent upgrade levels for SaveManager.</summary>
    public UpgradeLevelEntry[] GetPermanentLevelsForSave()
    {
        var arr = new UpgradeLevelEntry[permanentLevels.Count];
        int i = 0;
        foreach (var kvp in permanentLevels)
            arr[i++] = new UpgradeLevelEntry { upgradeID = kvp.Key, level = kvp.Value };
        return arr;
    }

    /// <summary>Replace in-memory permanent levels with the saved snapshot. Null-safe.</summary>
    public void LoadPermanentLevels(UpgradeLevelEntry[] entries)
    {
        permanentLevels.Clear();
        if (entries == null) return;
        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrEmpty(e.upgradeID) || e.level <= 0) continue;
            permanentLevels[e.upgradeID] = e.level;
        }
    }

    /// <summary>Snapshot of temporary (in-run) upgrade levels — used when saving an active run.</summary>
    public UpgradeLevelEntry[] GetTemporaryLevelsForSave()
    {
        var arr = new UpgradeLevelEntry[temporaryLevels.Count];
        int i = 0;
        foreach (var kvp in temporaryLevels)
            arr[i++] = new UpgradeLevelEntry { upgradeID = kvp.Key, level = kvp.Value };
        return arr;
    }

    /// <summary>
    /// Restore temporary (in-run) upgrade levels. Must be called AFTER RunManager.ResumeRun,
    /// because OnRunStarted clears temporaryLevels in OnRunStarted handler.
    /// </summary>
    public void LoadTemporaryLevels(UpgradeLevelEntry[] entries)
    {
        temporaryLevels.Clear();
        if (entries == null) return;
        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrEmpty(e.upgradeID) || e.level <= 0) continue;
            temporaryLevels[e.upgradeID] = e.level;
        }
    }

    /// <summary>
    /// Reset temporary levels at run start
    /// </summary>
    private void OnRunStarted()
    {
        temporaryLevels.Clear();
    }

    /// <summary>
    /// Get permanent level for an upgrade
    /// </summary>
    public int GetPermanentLevel(string upgradeID)
    {
        if (permanentLevels.TryGetValue(upgradeID, out int level))
        {
            return level;
        }
        return 0; // Default level 0
    }

    /// <summary>
    /// Get current effective level (permanent + temporary)
    /// </summary>
    public int GetCurrentLevel(string upgradeID)
    {
        int permanent = GetPermanentLevel(upgradeID);
        
        if (temporaryLevels.TryGetValue(upgradeID, out int temp))
        {
            return Mathf.Max(permanent, temp); // Temp should always be >= permanent
        }
        
        return permanent;
    }

    /// <summary>
    /// Get temporary bonus levels (how many levels above permanent)
    /// </summary>
    public int GetTemporaryBonus(string upgradeID)
    {
        return GetCurrentLevel(upgradeID) - GetPermanentLevel(upgradeID);
    }

    /// <summary>
    /// Purchase permanent upgrade with Coins (between runs)
    /// </summary>
    public bool PurchasePermanentUpgrade(string upgradeID, int coinCost, int maxLevel = int.MaxValue)
    {
        if (RunManager.Instance != null && RunManager.Instance.IsRunActive)
        {
            Debug.LogWarning("Cannot purchase permanent upgrades during a run!");
            return false;
        }

        // Never exceed the upgrade's max level (callers pass data.maxLevel). Guards against any
        // over-purchase path leaving a level above the cap (e.g. the old grid_size 4/3 bug).
        if (GetPermanentLevel(upgradeID) >= maxLevel)
        {
            Debug.LogWarning($"Upgrade '{upgradeID}' already at max level ({maxLevel}); purchase rejected.");
            return false;
        }

        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.SpendCoins(coinCost))
        {
            return false;
        }

        int currentLevel = GetPermanentLevel(upgradeID);
        permanentLevels[upgradeID] = currentLevel + 1;

        Debug.Log($"✅ Purchased permanent upgrade: {upgradeID} → Level {permanentLevels[upgradeID]} (Cost: {coinCost} Coins)");
        
        // Notify listeners that upgrade was purchased
        OnUpgradePurchased?.Invoke(upgradeID);
        
        return true;
    }

    /// <summary>
    /// Purchase temporary upgrade with Money (during run)
    /// </summary>
    public bool PurchaseTemporaryUpgrade(string upgradeID, int moneyCost)
    {
        if (RunManager.Instance == null || !RunManager.Instance.IsRunActive)
        {
            Debug.LogWarning("Cannot purchase temporary upgrades outside of a run!");
            return false;
        }

        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.SpendMoney(moneyCost))
        {
            return false;
        }

        int currentLevel = GetCurrentLevel(upgradeID);
        temporaryLevels[upgradeID] = currentLevel + 1;

        Debug.Log($"⚡ Purchased temporary upgrade: {upgradeID} → Level {temporaryLevels[upgradeID]} (Cost: ${moneyCost} Money)");
        
        // Notify listeners that upgrade was purchased
        OnUpgradePurchased?.Invoke(upgradeID);
        
        return true;
    }

    /// <summary>
    /// Calculate Money cost for next temporary level
    /// Uses fixed tier system: Level 2 always costs 10k, Level 3 always costs 22k, etc.
    /// </summary>
    public int CalculateMoneyCost(int nextLevel)
    {
        if (nextLevel <= 1) return 0;

        // Base cost for level 2
        int baseCost = 10000;

        // Each level multiplies by scaling factor
        float cost = baseCost;
        for (int i = 2; i < nextLevel; i++)
        {
            cost *= moneyScalingMultiplier;
        }

        return Mathf.RoundToInt(cost);
    }

    /// <summary>
    /// Format Money cost for display (10k, 22k, 48k, etc.)
    /// </summary>
    public string FormatMoneyCost(int cost)
    {
        if (cost >= 1000000)
        {
            return $"{(cost / 1000000f):F1}M";
        }
        else if (cost >= 1000)
        {
            return $"{(cost / 1000f):F0}k";
        }
        else
        {
            return cost.ToString();
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Show All Upgrades")]
    private void ShowAllUpgrades()
    {
        Debug.Log("=== UPGRADE MANAGER ===");
        Debug.Log($"Permanent Upgrades: {permanentLevels.Count}");
        foreach (var kvp in permanentLevels)
        {
            Debug.Log($"  {kvp.Key}: Level {kvp.Value} (Permanent)");
        }

        Debug.Log($"Temporary Upgrades: {temporaryLevels.Count}");
        foreach (var kvp in temporaryLevels)
        {
            Debug.Log($"  {kvp.Key}: Level {kvp.Value} (This Run)");
        }
    }

    [ContextMenu("Test Money Scaling")]
    private void TestMoneyScaling()
    {
        Debug.Log("=== MONEY COST SCALING (2.2x multiplier) ===");
        for (int i = 2; i <= 10; i++)
        {
            int cost = CalculateMoneyCost(i);
            Debug.Log($"Level {i}: ${cost:N0} ({FormatMoneyCost(cost)})");
        }
    }
#endif
}