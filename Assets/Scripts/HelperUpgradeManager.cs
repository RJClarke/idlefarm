using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages helper upgrades purchased with Coins
/// Tracks purchased upgrades and calculates total bonuses
/// Phase 5.5: Permanent meta-progression system
/// </summary>
public class HelperUpgradeManager : MonoBehaviour
{
    public static HelperUpgradeManager Instance { get; private set; }

    [Header("Available Upgrades")]
    [Tooltip("All helper upgrades that can be purchased")]
    [SerializeField] private List<HelperUpgrade> allUpgrades = new List<HelperUpgrade>();

    [Header("Starting Values")]
    [SerializeField] private int baseMaxHelpers = 1;
    [SerializeField] private float baseMovementSpeed = 2f;
    [SerializeField] private float baseTaskDuration = 0.5f;

    [Header("Current State (Read-Only)")]
    [SerializeField] private List<string> purchasedUpgradeIDs = new List<string>();
    [SerializeField] private int currentMaxHelpers;
    [SerializeField] private float currentMovementSpeedMultiplier;
    [SerializeField] private float currentTaskSpeedMultiplier;
    [SerializeField] private int autoSpawnCount;

    // Cached upgrade lookup
    private Dictionary<string, HelperUpgrade> upgradesByID;

    // Properties for other systems to use
    public int MaxHelpers => currentMaxHelpers;
    public float MovementSpeedMultiplier => currentMovementSpeedMultiplier;
    public float TaskSpeedMultiplier => currentTaskSpeedMultiplier;
    public int AutoSpawnHelpers => autoSpawnCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Build lookup dictionary
        BuildUpgradeLookup();
    }

    private void Start()
    {
        // Load purchased upgrades from save
        LoadUpgrades();
        
        // Calculate initial bonuses
        RecalculateBonuses();
        


    }

    /// <summary>
    /// Build lookup dictionary for upgrades
    /// </summary>
    private void BuildUpgradeLookup()
    {
        upgradesByID = new Dictionary<string, HelperUpgrade>();
        
        foreach (HelperUpgrade upgrade in allUpgrades)
        {
            if (upgrade != null && !string.IsNullOrEmpty(upgrade.upgradeID))
            {
                upgradesByID[upgrade.upgradeID] = upgrade;
            }
        }
    }

    /// <summary>
    /// Check if an upgrade has been purchased
    /// </summary>
    public bool IsUpgradePurchased(string upgradeID)
    {
        return purchasedUpgradeIDs.Contains(upgradeID);
    }

    /// <summary>
    /// Attempt to purchase an upgrade with Coins
    /// </summary>
    public bool PurchaseUpgrade(HelperUpgrade upgrade)
    {
        if (upgrade == null)
        {
            Debug.LogError("Cannot purchase null upgrade!");
            return false;
        }

        // Check if already purchased
        if (IsUpgradePurchased(upgrade.upgradeID))
        {
            Debug.LogWarning($"Upgrade {upgrade.upgradeName} already purchased!");
            return false;
        }

        // Check prerequisites
        if (!upgrade.MeetsPrerequisites(this))
        {
            Debug.LogWarning($"Prerequisites not met for {upgrade.upgradeName}!");
            return false;
        }

        // Check if player can afford it
        if (CurrencyManager.Instance == null)
        {
            Debug.LogError("CurrencyManager not found!");
            return false;
        }

        if (!CurrencyManager.Instance.CanAffordCoins(upgrade.coinCost))
        {
            return false;
        }

        // Spend coins
        if (!CurrencyManager.Instance.SpendCoins(upgrade.coinCost))
        {
            return false;
        }

        // Mark as purchased
        purchasedUpgradeIDs.Add(upgrade.upgradeID);
        
        // Recalculate bonuses
        RecalculateBonuses();
        
        // Save
        SaveUpgrades();
        
        Debug.Log($"✓ Purchased {upgrade.upgradeName} for {upgrade.coinCost} coins!");
        
        return true;
    }

    /// <summary>
    /// Recalculate all bonuses from purchased upgrades
    /// </summary>
    private void RecalculateBonuses()
    {
        // Reset to base values
        currentMaxHelpers = baseMaxHelpers;
        currentMovementSpeedMultiplier = 1.0f;
        currentTaskSpeedMultiplier = 1.0f;
        autoSpawnCount = 0;

        // Apply all purchased upgrades
        foreach (string upgradeID in purchasedUpgradeIDs)
        {
            if (upgradesByID.TryGetValue(upgradeID, out HelperUpgrade upgrade))
            {
                ApplyUpgradeBonus(upgrade);
            }
        }
    }

    /// <summary>
    /// Apply a single upgrade's bonus
    /// </summary>
    private void ApplyUpgradeBonus(HelperUpgrade upgrade)
    {
        switch (upgrade.upgradeType)
        {
            case HelperUpgradeType.MovementSpeed:
                // Multiply movement speed (1.5 = 50% faster)
                currentMovementSpeedMultiplier *= upgrade.upgradeValue;
                break;

            case HelperUpgradeType.TaskSpeed:
                // Multiply task duration (0.5 = 50% faster)
                currentTaskSpeedMultiplier *= upgrade.upgradeValue;
                break;

            case HelperUpgradeType.MaxHelpers:
                // Add to max helpers (flat increase)
                currentMaxHelpers += Mathf.RoundToInt(upgrade.upgradeValue);
                break;

            case HelperUpgradeType.AutoSpawn:
                // Add to auto-spawn count
                autoSpawnCount += Mathf.RoundToInt(upgrade.upgradeValue);
                break;
        }
    }

    /// <summary>
    /// Get all upgrades of a specific type
    /// </summary>
    public List<HelperUpgrade> GetUpgradesByType(HelperUpgradeType type)
    {
        List<HelperUpgrade> filtered = new List<HelperUpgrade>();
        
        foreach (HelperUpgrade upgrade in allUpgrades)
        {
            if (upgrade != null && upgrade.upgradeType == type)
            {
                filtered.Add(upgrade);
            }
        }
        
        return filtered;
    }

    /// <summary>
    /// Get all available (unpurchased, meets prerequisites) upgrades
    /// </summary>
    public List<HelperUpgrade> GetAvailableUpgrades()
    {
        List<HelperUpgrade> available = new List<HelperUpgrade>();
        
        foreach (HelperUpgrade upgrade in allUpgrades)
        {
            if (upgrade != null && 
                !IsUpgradePurchased(upgrade.upgradeID) && 
                upgrade.MeetsPrerequisites(this))
            {
                available.Add(upgrade);
            }
        }
        
        return available;
    }

    /// <summary>
    /// Get all purchased upgrades
    /// </summary>
    public List<HelperUpgrade> GetPurchasedUpgrades()
    {
        List<HelperUpgrade> purchased = new List<HelperUpgrade>();
        
        foreach (string upgradeID in purchasedUpgradeIDs)
        {
            if (upgradesByID.TryGetValue(upgradeID, out HelperUpgrade upgrade))
            {
                purchased.Add(upgrade);
            }
        }
        
        return purchased;
    }

    #region Save/Load

    /// <summary>
    /// Persistence is now driven by SaveManager.SaveGame / .LoadGame; this stub remains
    /// only so the existing call site in PurchaseUpgrade compiles. Save-on-pause hook
    /// flushes to disk on app background / quit.
    /// </summary>
    private void SaveUpgrades() { /* no-op — SaveManager handles persistence */ }

    /// <summary>Reset to a clean state. Called once in Awake; SaveManager populates after.</summary>
    private void LoadUpgrades() { purchasedUpgradeIDs.Clear(); }

    /// <summary>Serializable snapshot for SaveManager.</summary>
    public string[] GetPurchasedIDsForSave()
        => purchasedUpgradeIDs != null ? purchasedUpgradeIDs.ToArray() : new string[0];

    /// <summary>
    /// Replace purchased-upgrade list from the saved snapshot and re-apply all bonuses.
    /// Null-safe; unknown IDs (e.g. catalog rename) are silently skipped.
    /// </summary>
    public void LoadState(string[] ids)
    {
        purchasedUpgradeIDs.Clear();
        if (ids != null)
        {
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (purchasedUpgradeIDs.Contains(id)) continue;
                purchasedUpgradeIDs.Add(id);
            }
        }
        RecalculateBonuses();
    }

    #endregion

    #region Testing/Debug

    [ContextMenu("Show Current Bonuses")]
    private void ShowCurrentBonuses()
    {
        Debug.Log($"=== CURRENT BONUSES ===");
        Debug.Log($"Max Helpers: {currentMaxHelpers} (base: {baseMaxHelpers})");
        Debug.Log($"Movement Speed: {currentMovementSpeedMultiplier:F2}x");
        Debug.Log($"Task Speed: {currentTaskSpeedMultiplier:F2}x (duration: {baseTaskDuration * currentTaskSpeedMultiplier:F2}s)");
        Debug.Log($"Auto-Spawn: {autoSpawnCount} helpers");
        Debug.Log($"Purchased Upgrades: {purchasedUpgradeIDs.Count}");
    }

    [ContextMenu("List Available Upgrades")]
    private void ListAvailableUpgrades()
    {
        List<HelperUpgrade> available = GetAvailableUpgrades();
        Debug.Log($"=== AVAILABLE UPGRADES ({available.Count}) ===");
        
        foreach (HelperUpgrade upgrade in available)
        {
            Debug.Log($"  • {upgrade.upgradeName} - {upgrade.coinCost} coins");
        }
    }

    [ContextMenu("Reset All Upgrades")]
    private void ResetAllUpgrades()
    {
        purchasedUpgradeIDs.Clear();
        RecalculateBonuses();
        Debug.Log("Reset all upgrades");
    }

    #endregion
}