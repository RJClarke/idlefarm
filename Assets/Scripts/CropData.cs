using UnityEngine;

/// <summary>
/// ScriptableObject that defines all properties for a single crop type
/// Create instances via: Right-click in Project â†’ Create â†’ Farm Game â†’ Crop Data
/// </summary>
[CreateAssetMenu(fileName = "New Crop", menuName = "Farm Game/Crop Data", order = 1)]
public class CropData : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("Display name of the crop (e.g., 'Carrot', 'Tomato')")]
    public string cropName = "New Crop";
    
    [Tooltip("Category/classification of this crop")]
    public CropType cropType = CropType.Root;
    
    [TextArea(2, 4)]
    [Tooltip("Optional description for UI tooltips")]
    public string description = "";

    [Header("Growth Timers (seconds)")]
    [Tooltip("Time to grow from Seed â†’ Sprout")]
    public float seedSeconds = 40f;
    
    [Tooltip("Time to grow from Sprout â†’ Sapling")]
    public float sproutSeconds = 60f;
    
    [Tooltip("Time to grow from Sapling â†’ Harvestable")]
    public float saplingSeconds = 200f;

    [Header("Health & Durability")]
    [Tooltip("Maximum HP - shared across all growth stages. Determines how long crop survives threats and rot.")]
    [Range(50, 120)]
    public int maxHP = 80;

    [Header("Harvest Mechanics")]
    [Tooltip("Time window (seconds) to harvest at 100% value after becoming Harvestable")]
    [Range(20, 80)]
    public float harvestWindowSeconds = 50f;
    
    [Tooltip("Money reward when harvested during harvest window (100% value)")]
    public int harvestValue = 10;

    [Tooltip("Crop tier for compost yield (default 1). Higher tier = more compost when this crop dies.")]
    public int tier = 1;

    [Header("Regrowth")]
    [Tooltip("Does this crop regrow after harvest, or is it removed?")]
    public bool canRegrow = false;
    
    [Tooltip("Time (seconds) to regrow from Seed â†’ Harvestable again (only if canRegrow = true)")]
    public float regrowSeconds = 0f;

    [Header("Visuals")]
    [Tooltip("Plant prefab to instantiate when planting this crop")]
    public GameObject plantPrefab;
    
    [Tooltip("Sprite shown during Seed stage")]
    public Sprite seedSprite;
    
    [Tooltip("Sprite shown during Sprout stage")]
    public Sprite sproutSprite;
    
    [Tooltip("Sprite shown during Sapling stage")]
    public Sprite saplingSprite;
    
    [Tooltip("Sprite shown during Harvestable stage")]
    public Sprite harvestableSprite;

    [Tooltip("Sprite shown after harvest (for regrowable crops)")]
    public Sprite harvestedSprite; // Empty plant, picked tomatoes, etc.
    
    [Tooltip("Sprite shown during Dried Out state (optional)")]
    public Sprite driedOutSprite;
    
    [Tooltip("Sprite shown during Rot state (optional)")]
    public Sprite rottingSprite;
    
    [Header("Store/UI Visuals")]
    [Tooltip("Seed packet sprite shown in unlock store/market")]
    public Sprite seedPacketSprite;
    
    [Tooltip("Crop item sprite (just the crop itself, not the plant - e.g., an ear of corn, a blueberry). For future use in inventory/UI.")]
    public Sprite cropSprite;

    [Header("Advanced Properties (Future Use)")]
    [Tooltip("Moisture depletion rate modifier (1.0 = normal, 0.5 = slower, 2.0 = faster)")]
    [Range(0.5f, 2f)]
    public float moistureDepletionRate = 1f;
    
    [Tooltip("Base threat resistance (higher = less damage from threats)")]
    [Range(0f, 2f)]
    public float threatResistance = 1f;

    /// <summary>
    /// Calculate total growth time from Seed â†’ Harvestable
    /// </summary>
    public float TotalGrowthTime => seedSeconds + sproutSeconds + saplingSeconds;

    /// <summary>
    /// Calculate approximate time to death during rot (based on max HP and rot decay rate)
    /// NOTE: After harvest window expires, plant enters Rot state where:
    /// - HP decays at global rate (see GameConstants.rotDecayRate, default 4 HP/sec)
    /// - Can still harvest at 25% value to save the plant
    /// - Plant dies when HP reaches 0
    /// </summary>
    public float ApproximateRotSurvivalTime
    {
        get
        {
            if (GameConstants.Instance != null)
            {
                return maxHP / GameConstants.Instance.rotDecayRate;
            }
            return maxHP / 4f; // Fallback if GameConstants not loaded
        }
    }

    /// <summary>
    /// Get time for a specific growth stage
    /// </summary>
    public float GetStageTime(GrowthStage stage)
    {
        switch (stage)
        {
            case GrowthStage.Seed:
                return seedSeconds;
            case GrowthStage.Sprout:
                return sproutSeconds;
            case GrowthStage.Sapling:
                return saplingSeconds;
            case GrowthStage.Harvestable:
                return 0f; // No timer for harvestable - uses harvest window instead
            default:
                return 0f;
        }
    }

    /// <summary>
    /// Get sprite for a specific growth stage
    /// </summary>
    public Sprite GetStageSprite(GrowthStage stage)
    {
        switch (stage)
        {
            case GrowthStage.Seed:
                return seedSprite;
            case GrowthStage.Sprout:
                return sproutSprite;
            case GrowthStage.Sapling:
                return saplingSprite;
            case GrowthStage.Harvestable:
                return harvestableSprite;
            default:
                return null;
        }
    }

    /// <summary>
    /// Validate crop data in editor
    /// </summary>
    private void OnValidate()
    {
        // Ensure positive values
        seedSeconds = Mathf.Max(1f, seedSeconds);
        sproutSeconds = Mathf.Max(1f, sproutSeconds);
        saplingSeconds = Mathf.Max(1f, saplingSeconds);
        harvestWindowSeconds = Mathf.Max(10f, harvestWindowSeconds);
        
        // If can't regrow, regrow time should be 0
        if (!canRegrow)
        {
            regrowSeconds = 0f;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Display helpful info in inspector
    /// </summary>
    [ContextMenu("Show Growth Summary")]
    private void ShowGrowthSummary()
    {
        float totalMinutes = TotalGrowthTime / 60f;
        float rotSurvival = ApproximateRotSurvivalTime;
        
        Debug.Log($"=== {cropName} Growth Summary ===");
        Debug.Log($"Type: {cropType}");
        Debug.Log($"Total Growth Time: {totalMinutes:F1} minutes ({TotalGrowthTime:F0} seconds)");
        Debug.Log($"  Seed â†’ Sprout: {seedSeconds}s");
        Debug.Log($"  Sprout â†’ Sapling: {sproutSeconds}s");
        Debug.Log($"  Sapling â†’ Harvestable: {saplingSeconds}s");
        Debug.Log($"");
        Debug.Log($"Harvest Window: {harvestWindowSeconds}s (100% value = ${harvestValue})");
        Debug.Log($"After Window: Enters Rot State");
        Debug.Log($"  â€¢ HP decays at {(GameConstants.Instance != null ? GameConstants.Instance.rotDecayRate : 4f)} HP/sec");
        Debug.Log($"  â€¢ Survives ~{rotSurvival:F1}s in rot ({maxHP} HP Ã· decay rate)");
        Debug.Log($"  â€¢ Can harvest at 25% value (${Mathf.RoundToInt(harvestValue * 0.25f)}) to save plant");
        Debug.Log($"  â€¢ Plant dies if HP reaches 0");
        Debug.Log($"");
        Debug.Log($"Max HP: {maxHP}");
        Debug.Log($"Can Regrow: {(canRegrow ? $"Yes ({regrowSeconds}s)" : "No")}");
    }
#endif
}