using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Central database of all available crops in the game
/// Singleton ScriptableObject - create one instance and reference it everywhere
/// Create via: Right-click in Project → Create → Farm Game → Crop Database
/// </summary>
[CreateAssetMenu(fileName = "CropDatabase", menuName = "Farm Game/Crop Database", order = 0)]
public class CropDatabase : ScriptableObject
{
    [Header("All Available Crops")]
    [Tooltip("Drag all CropData assets here")]
    public List<CropData> allCrops = new List<CropData>();

    [Header("Starting Crops (Unlocked from Beginning)")]
    [Tooltip("Crops available at game start")]
    public List<CropData> startingCrops = new List<CropData>();

    // Cache for fast lookups
    private Dictionary<string, CropData> cropsByName;

    /// <summary>
    /// Initialize the database (call this on game start)
    /// </summary>
    public void Initialize()
    {
        // Build lookup dictionary
        cropsByName = new Dictionary<string, CropData>();
        foreach (CropData crop in allCrops)
        {
            if (crop != null)
            {
                cropsByName[crop.cropName] = crop;
            }
        }

    }

    /// <summary>
    /// Get a crop by name
    /// </summary>
    public CropData GetCropByName(string cropName)
    {
        if (cropsByName == null)
        {
            Initialize();
        }

        if (cropsByName.TryGetValue(cropName, out CropData crop))
        {
            return crop;
        }

        Debug.LogWarning($"Crop '{cropName}' not found in database!");
        return null;
    }

    /// <summary>
    /// Get all crops of a specific type
    /// </summary>
    public List<CropData> GetCropsByType(CropType type)
    {
        return allCrops.Where(c => c != null && c.cropType == type).ToList();
    }

    /// <summary>
    /// Get random crop from starting crops (for testing/initial planting)
    /// </summary>
    public CropData GetRandomStartingCrop()
    {
        if (startingCrops.Count == 0)
        {
            Debug.LogWarning("No starting crops defined!");
            return null;
        }

        int randomIndex = Random.Range(0, startingCrops.Count);
        return startingCrops[randomIndex];
    }

    /// <summary>
    /// Get all crops sorted by growth time (fastest first)
    /// </summary>
    public List<CropData> GetCropsSortedBySpeed()
    {
        return allCrops.Where(c => c != null)
                      .OrderBy(c => c.TotalGrowthTime)
                      .ToList();
    }

    /// <summary>
    /// Get all crops sorted by harvest value (highest first)
    /// </summary>
    public List<CropData> GetCropsSortedByValue()
    {
        return allCrops.Where(c => c != null)
                      .OrderByDescending(c => c.harvestValue)
                      .ToList();
    }

    /// <summary>
    /// Validate database in editor
    /// </summary>
    private void OnValidate()
    {
        // Remove null entries
        allCrops.RemoveAll(c => c == null);
        startingCrops.RemoveAll(c => c == null);

        // Ensure starting crops are in all crops
        foreach (CropData startingCrop in startingCrops)
        {
            if (!allCrops.Contains(startingCrop))
            {
                Debug.LogWarning($"{startingCrop.cropName} is in starting crops but not in all crops!");
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Show database summary in console
    /// </summary>
    [ContextMenu("Show Database Summary")]
    private void ShowDatabaseSummary()
    {
        Debug.Log($"=== CROP DATABASE SUMMARY ===");
        Debug.Log($"Total Crops: {allCrops.Count}");
        Debug.Log($"Starting Crops: {startingCrops.Count}");
        Debug.Log($"\nAll Crops:");
        
        foreach (CropData crop in allCrops)
        {
            if (crop != null)
            {
                float minutes = crop.TotalGrowthTime / 60f;
                string regrowInfo = crop.canRegrow ? " (Regrows)" : "";
                Debug.Log($"  • {crop.cropName} ({crop.cropType}) - {minutes:F1} min, ${crop.harvestValue}{regrowInfo}");
            }
        }

        Debug.Log($"\nStarting Crops:");
        foreach (CropData crop in startingCrops)
        {
            if (crop != null)
            {
                Debug.Log($"  • {crop.cropName}");
            }
        }
    }

    /// <summary>
    /// Auto-populate starting crops with fastest 3 crops
    /// </summary>
    [ContextMenu("Auto-Set Starting Crops (Fastest 3)")]
    private void AutoSetStartingCrops()
    {
        var fastestCrops = allCrops.Where(c => c != null)
                                  .OrderBy(c => c.TotalGrowthTime)
                                  .Take(3)
                                  .ToList();
        
        startingCrops = fastestCrops;
        Debug.Log($"Set starting crops to: {string.Join(", ", fastestCrops.Select(c => c.cropName))}");
        
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        #endif
    }
#endif
}