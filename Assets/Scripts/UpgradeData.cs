using UnityEngine;

/// <summary>
/// ScriptableObject that defines an upgrade's properties
/// Used for grid size, zones, helper stats, etc.
/// Create via: Right-click → Create → Farm Game → Upgrade Data
/// </summary>
[CreateAssetMenu(fileName = "New Upgrade", menuName = "Farm Game/Upgrade Data", order = 10)]
public class UpgradeData : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("Unique ID for this upgrade (e.g., 'grid_size', 'helper_move_speed')")]
    public string upgradeID = "upgrade_id";
    
    [Tooltip("Display name (e.g., 'Grid Size', 'Helper Move Speed')")]
    public string displayName = "Upgrade Name";
    
    [TextArea(2, 4)]
    [Tooltip("Description shown to player")]
    public string description = "Upgrade description";

    [Header("Upgrade Type")]
    [Tooltip("What kind of upgrade is this?")]
    public UpgradeType upgradeType = UpgradeType.Stat;
    
    [Tooltip("Can this be upgraded with Money during runs? (False = Coins only, between runs)")]
    public bool allowTemporaryUpgrades = true;

    [Header("Level Settings")]
    [Tooltip("Maximum level for this upgrade")]
    [Range(1, 20)]
    public int maxLevel = 10;

    [Header("Coin Costs (Permanent - Between Runs)")]
    [Tooltip("Base Coin cost for Level 1")]
    public int baseCoinCost = 100;
    
    [Tooltip("Coin cost multiplier per level (e.g., 2.5x = exponential scaling)")]
    [Range(1.5f, 5f)]
    public float coinCostMultiplier = 2.5f;

    [Header("Money Costs (Temporary - During Runs)")]
    [Tooltip("Base Money cost for temporary Level 2 (10k default)")]
    public int baseMoneyCost = 10000;
    
    [Tooltip("Money cost multiplier per level (2.2x = Double + Tax)")]
    [Range(1.5f, 3f)]
    public float moneyCostMultiplier = 2.2f;

    [Header("Stat Upgrade Settings")]
    [Tooltip("Bonus per level (e.g., +1% per level, +5 HP per level)")]
    public float bonusPerLevel = 1f;
    
    [Tooltip("Unit for display (%, HP, tiles, etc.)")]
    public string bonusUnit = "%";
    
    [Tooltip("Show + sign before bonus? (e.g., '+5%' vs '5%')")]
    public bool showPlusSign = true;

    [Header("Visual")]
    [Tooltip("Icon/emoji for this upgrade")]
    public string icon = "⚡";

    /// <summary>
    /// Calculate Coin cost for a specific level
    /// Exponential scaling: Level 1 = base, Level 2 = base * multiplier, etc.
    /// </summary>
    public int GetCoinCost(int level)
    {
        if (level <= 0) return 0;
        
        float cost = baseCoinCost;
        for (int i = 1; i < level; i++)
        {
            cost *= coinCostMultiplier;
        }
        
        return Mathf.RoundToInt(cost);
    }

    /// <summary>
    /// Calculate Money cost for a specific temporary level
    /// Temporary levels start at 2 (after permanent base)
    /// Fixed tier system: Level 2 = 10k, Level 3 = 22k, etc.
    /// </summary>
    public int GetMoneyCost(int level)
    {
        if (level <= 1) return 0; // No Money cost for level 0 or 1
        
        float cost = baseMoneyCost;
        for (int i = 2; i < level; i++)
        {
            cost *= moneyCostMultiplier;
        }
        
        return Mathf.RoundToInt(cost);
    }

    /// <summary>
    /// Get display text for upgrade bonus at a specific level
    /// </summary>
    public string GetBonusText(int level)
    {
        if (level <= 0) return "Base";

        float totalBonus = bonusPerLevel * level;
        string sign = showPlusSign ? "+" : "";
        
        // Format based on unit
        if (bonusUnit == "%")
        {
            return $"{sign}{totalBonus:F0}{bonusUnit}";
        }
        else
        {
            return $"{sign}{totalBonus:F0} {bonusUnit}";
        }
    }

    /// <summary>
    /// Get short description of what this upgrade does at a level
    /// </summary>
    public string GetLevelDescription(int level)
    {
        if (upgradeType == UpgradeType.Stat)
        {
            return $"{displayName}: {GetBonusText(level)}";
        }
        else if (upgradeType == UpgradeType.Unlock)
        {
            return level > 0 ? $"{displayName}: Unlocked" : $"{displayName}: Locked";
        }
        else
        {
            return $"{displayName}: Level {level}";
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Show Cost Scaling")]
    private void ShowCostScaling()
    {
        Debug.Log($"=== {displayName} Cost Scaling ===");
        Debug.Log("PERMANENT (Coins):");
        for (int i = 1; i <= Mathf.Min(maxLevel, 5); i++)
        {
            int coinCost = GetCoinCost(i);
            string bonus = GetBonusText(i);
            Debug.Log($"  Level {i}: {coinCost} Coins → {bonus}");
        }
        
        if (allowTemporaryUpgrades)
        {
            Debug.Log("\nTEMPORARY (Money, during runs):");
            for (int i = 2; i <= Mathf.Min(maxLevel, 6); i++)
            {
                int moneyCost = GetMoneyCost(i);
                string bonus = GetBonusText(i);
                Debug.Log($"  Level {i}: {moneyCost} Money → {bonus}");
            }
        }
    }
#endif
}

/// <summary>
/// Types of upgrades in the game
/// </summary>
public enum UpgradeType
{
    Stat,       // Percentage or numeric bonus (speed, efficiency, etc.)
    Unlock,     // Binary unlock (zone, crop, equipment)
    Capacity    // Increases max value (helper slots, grid size)
}