using UnityEngine;

/// <summary>
/// ScriptableObject that defines a one-time unlock (crop, equipment, etc.)
/// Create via: Right-click → Create → Farm Game → Unlock Data
/// ONE SCRIPT FOR ALL UNLOCKS - just create different assets!
/// </summary>
[CreateAssetMenu(fileName = "New Unlock", menuName = "Farm Game/Unlock Data", order = 11)]
public class UnlockData : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("Unique ID for save system (e.g., 'scarecrow_unlock', 'corn_unlock')")]
    public string unlockID = "unlock_id";
    
    [Tooltip("Display name shown to player")]
    public string displayName = "Item Name";
    
    [Tooltip("Icon/emoji for this unlock")]
    public string icon = "🌾";

    [Header("Unlock Type")]
    [Tooltip("What category does this belong to?")]
    public UnlockCategory category = UnlockCategory.Equipment;
    
    [Header("Linked Data")]
    [Tooltip("For crops: Drag the CropData here to automatically use its seed packet sprite")]
    public CropData cropData;
    
    [Tooltip("For equipment: Drag an icon sprite here to display on unlock button (store icon, not in-game sprite)")]
    public Sprite equipmentSprite;

    [Header("Cost")]
    [Tooltip("Coin cost to unlock (permanent currency)")]
    public int coinCost = 500;

    [Header("Descriptions")]
    [TextArea(2, 4)]
    [Tooltip("Description shown when locked")]
    public string lockedDescription = "Unlock this to use it on your farm.";
    
    [Tooltip("Short message shown when unlocked")]
    public string unlockedMessage = "Unlocked! Now available for use.";

    [Header("Prerequisites (Optional)")]
    [Tooltip("Other unlocks required before this is available")]
    public UnlockData[] requiredUnlocks;

    /// <summary>
    /// Check if prerequisites are met
    /// </summary>
    public bool MeetsPrerequisites()
    {
        if (UpgradeManager.Instance == null) return false;

        if (requiredUnlocks != null && requiredUnlocks.Length > 0)
        {
            foreach (UnlockData required in requiredUnlocks)
            {
                if (required != null)
                {
                    // Check if required unlock has been purchased (level > 0)
                    if (UpgradeManager.Instance.GetPermanentLevel(required.unlockID) == 0)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Get list of missing prerequisites
    /// </summary>
    public string GetMissingPrerequisites()
    {
        if (requiredUnlocks == null || requiredUnlocks.Length == 0)
            return "";

        string missing = "";
        foreach (UnlockData required in requiredUnlocks)
        {
            if (required != null && UpgradeManager.Instance.GetPermanentLevel(required.unlockID) == 0)
            {
                if (missing.Length > 0) missing += ", ";
                missing += required.displayName;
            }
        }

        return missing;
    }

#if UNITY_EDITOR
    [ContextMenu("Generate Unique ID")]
    private void GenerateUniqueID()
    {
        // Auto-generate ID from name
        unlockID = name.ToLower().Replace(" ", "_");
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"Generated ID: {unlockID}");
    }

    [ContextMenu("Show Unlock Info")]
    private void ShowUnlockInfo()
    {
        Debug.Log($"=== {displayName} ===");
        Debug.Log($"ID: {unlockID}");
        Debug.Log($"Category: {category}");
        Debug.Log($"Cost: {coinCost} Coins");
        Debug.Log($"Icon: {icon}");
        
        if (requiredUnlocks != null && requiredUnlocks.Length > 0)
        {
            Debug.Log($"Prerequisites: {requiredUnlocks.Length}");
            foreach (var req in requiredUnlocks)
            {
                if (req != null)
                    Debug.Log($"  - {req.displayName}");
            }
        }
        else
        {
            Debug.Log("Prerequisites: None");
        }
    }
#endif
}

/// <summary>
/// Categories for organizing unlocks
/// </summary>
public enum UnlockCategory
{
    Crop,           // Corn, Watermelon, Onions, etc.
    Equipment,      // Scarecrow, Fence, Sprinkler, etc.
    Zone,           // Zone unlocks (though these might use UpgradeData)
    Special         // Future: Greenhouse, Auto-seller, etc.
}