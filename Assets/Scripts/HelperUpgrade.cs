using UnityEngine;

/// <summary>
/// Defines a single helper upgrade that can be purchased with Coins
/// These are permanent upgrades that persist across runs
/// Create via: Right-click → Create → Farm Game → Helper Upgrade
/// </summary>
[CreateAssetMenu(fileName = "New Helper Upgrade", menuName = "Farm Game/Helper Upgrade", order = 3)]
public class HelperUpgrade : ScriptableObject
{
    [Header("Upgrade Info")]
    [Tooltip("Display name (e.g., 'Movement Speed I')")]
    public string upgradeName = "New Upgrade";
    
    [TextArea(2, 4)]
    [Tooltip("Description shown in shop UI")]
    public string description = "";
    
    [Tooltip("Unique ID for save system")]
    public string upgradeID = "upgrade_001";

    [Header("Upgrade Type")]
    public HelperUpgradeType upgradeType = HelperUpgradeType.MovementSpeed;
    
    [Header("Cost & Value")]
    [Tooltip("Cost in Coins (permanent currency)")]
    public int coinCost = 10;
    
    [Tooltip("The value this upgrade provides (multiplier or flat amount)")]
    public float upgradeValue = 1.5f;

    [Header("Prerequisites (Optional)")]
    [Tooltip("Other upgrades that must be purchased first (leave empty if none)")]
    public HelperUpgrade[] requiredUpgrades;
    
    [Tooltip("Minimum number of runs completed before available")]
    public int minRunsRequired = 0;

    [Header("Visual")]
    [Tooltip("Icon for shop UI")]
    public Sprite icon;

    /// <summary>
    /// Check if player meets prerequisites for this upgrade
    /// </summary>
    public bool MeetsPrerequisites(HelperUpgradeManager manager)
    {
        // Check required upgrades
        if (requiredUpgrades != null && requiredUpgrades.Length > 0)
        {
            foreach (HelperUpgrade required in requiredUpgrades)
            {
                if (required != null && !manager.IsUpgradePurchased(required.upgradeID))
                {
                    return false;
                }
            }
        }

        // Check minimum runs (TODO: implement run counter)
        // For now, always pass this check
        
        return true;
    }

    /// <summary>
    /// Get formatted description with value
    /// </summary>
    public string GetFormattedDescription()
    {
        string formatted = description;
        
        // Add value info based on type
        switch (upgradeType)
        {
            case HelperUpgradeType.MovementSpeed:
                formatted += $"\n<color=green>+{(upgradeValue - 1f) * 100f:F0}% movement speed</color>";
                break;
            case HelperUpgradeType.TaskSpeed:
                formatted += $"\n<color=green>-{(1f - upgradeValue) * 100f:F0}% task duration</color>";
                break;
            case HelperUpgradeType.MaxHelpers:
                formatted += $"\n<color=green>+{upgradeValue:F0} max helpers</color>";
                break;
            case HelperUpgradeType.AutoSpawn:
                formatted += $"\n<color=green>Spawn {upgradeValue:F0} helpers at run start</color>";
                break;
        }
        
        return formatted;
    }

#if UNITY_EDITOR
    [ContextMenu("Generate Unique ID")]
    private void GenerateUniqueID()
    {
        upgradeID = $"upgrade_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"Generated ID: {upgradeID}");
    }
#endif
}

/// <summary>
/// Types of helper upgrades available
/// </summary>
public enum HelperUpgradeType
{
    MovementSpeed,  // Multiplier for move speed (1.5x = 50% faster)
    TaskSpeed,      // Multiplier for task duration (0.5x = 50% faster completion)
    MaxHelpers,     // Flat increase to max helpers allowed
    AutoSpawn       // Number of helpers to spawn at run start
}