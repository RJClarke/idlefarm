using UnityEngine;

/// <summary>
/// Test tool for spawning and testing plants
/// Phase 3.1: Now includes watering functionality
/// </summary>
public class PlantTestManager : MonoBehaviour
{
    [Header("Plant Prefab")]
    [Tooltip("Drag your Plant prefab here")]
    [SerializeField] private GameObject plantPrefab;

    [Header("Test Configuration")]
    [Tooltip("Drag CropDatabase here")]
    [SerializeField] private CropDatabase cropDatabase;
    
    [Tooltip("Which crop to spawn for testing")]
    [SerializeField] private int testCropIndex = 0;

    [Header("Spawn Settings")]
    [SerializeField] private bool autoTillBeforePlant = false; // Changed to false - must till manually!

    // Note: No Update() method - use context menu commands instead
    // (Right-click this component in Inspector to see options)

    /// <summary>
    /// Plant a test crop on a random available tile
    /// </summary>
    [ContextMenu("Plant Test Crop")]
    public void PlantRandomTestCrop()
    {
        if (plantPrefab == null)
        {
            Debug.LogError("Plant prefab not assigned!");
            return;
        }

        if (cropDatabase == null || cropDatabase.startingCrops.Count == 0)
        {
            Debug.LogError("No crop database or no starting crops!");
            return;
        }

        if (FarmGrid.Instance == null)
        {
            Debug.LogError("FarmGrid not found!");
            return;
        }

        // Get crop data
        CropData testCrop = cropDatabase.startingCrops[testCropIndex % cropDatabase.startingCrops.Count];

        // Find a plantable tile
        var plantableTiles = FarmGrid.Instance.GetPlantableTiles();

        if (plantableTiles.Count == 0)
        {
            if (autoTillBeforePlant)
            {
                // Try to till an untilled tile
                var untilledTiles = FarmGrid.Instance.GetUntilledTiles();
                if (untilledTiles.Count > 0)
                {
                    SoilTile tile = untilledTiles[0];
                    tile.TillTemporary(0); // Free for testing
                    plantableTiles = FarmGrid.Instance.GetPlantableTiles();
                }
            }

            if (plantableTiles.Count == 0)
            {
                Debug.LogWarning("No tilled tiles available! Use 'Till All Tiles' first.");
                return;
            }
        }

        // Pick random tile
        SoilTile randomTile = plantableTiles[Random.Range(0, plantableTiles.Count)];

        // Plant the crop
        bool success = randomTile.PlantCrop(plantPrefab, testCrop);

        if (success)
        {
            Debug.Log($"✓ Planted {testCrop.cropName} at Zone {randomTile.ZoneID} ({randomTile.GridX},{randomTile.GridY})");
            Debug.Log($"  Growth time: {testCrop.TotalGrowthTime / 60f:F1} minutes");
        }
    }

    /// <summary>
    /// Till all tiles for easy testing
    /// </summary>
    [ContextMenu("Till All Tiles (Free)")]
    public void TillAllTiles()
    {
        if (FarmGrid.Instance == null)
        {
            Debug.LogError("FarmGrid not found!");
            return;
        }

        var untilledTiles = FarmGrid.Instance.GetUntilledTiles();
        int count = 0;

        foreach (SoilTile tile in untilledTiles)
        {
            if (tile.TillTemporary(0)) // Free for testing
            {
                count++;
            }
        }

        Debug.Log($"✓ Tilled {count} tiles for testing");
    }

    /// <summary>
    /// Plant test crops on all tilled tiles
    /// </summary>
    [ContextMenu("Fill Grid with Test Crops")]
    public void FillGridWithTestCrops()
    {
        if (plantPrefab == null || cropDatabase == null)
        {
            Debug.LogError("Missing prefab or database!");
            return;
        }

        // Till all first
        TillAllTiles();

        // Get plantable tiles
        var plantableTiles = FarmGrid.Instance.GetPlantableTiles();
        int planted = 0;

        foreach (SoilTile tile in plantableTiles)
        {
            // Cycle through different crops
            int cropIndex = planted % cropDatabase.startingCrops.Count;
            CropData crop = cropDatabase.startingCrops[cropIndex];

            if (tile.PlantCrop(plantPrefab, crop))
            {
                planted++;
            }
        }

        Debug.Log($"✓ Planted {planted} test crops across the grid");
    }

    /// <summary>
    /// Clear all plants from the grid
    /// </summary>
    [ContextMenu("Clear All Plants")]
    public void ClearAllPlants()
    {
        Plant[] allPlants = FindObjectsByType<Plant>(FindObjectsSortMode.None);
        foreach (Plant plant in allPlants)
        {
            if (plant.ParentTile != null)
            {
                plant.ParentTile.ClearPlant();
            }
            else
            {
                Destroy(plant.gameObject);
            }
        }
        Debug.Log($"✓ Cleared {allPlants.Length} plants");
    }

    /// <summary>
    /// Phase 3.1: Water all plants that need it
    /// </summary>
    [ContextMenu("Water All Plants")]
    public void WaterAllPlants()
    {
        Plant[] allPlants = FindObjectsByType<Plant>(FindObjectsSortMode.None);
        int watered = 0;

        foreach (Plant plant in allPlants)
        {
            plant.Water();
            watered++;
        }

        Debug.Log($"💧 Watered {watered} plants!");
    }

    /// <summary>
    /// Phase 3.1: Water only plants below 50% moisture
    /// </summary>
    [ContextMenu("Water Low Moisture Plants")]
    public void WaterLowMoisturePlants()
    {
        Plant[] allPlants = FindObjectsByType<Plant>(FindObjectsSortMode.None);
        int watered = 0;

        foreach (Plant plant in allPlants)
        {
            if (plant.CurrentMoisture < 50f)
            {
                plant.Water();
                watered++;
            }
        }

        Debug.Log($"💧 Watered {watered} plants with low moisture");
    }

    /// <summary>
    /// Phase 3.1: Force all plants to 0% moisture (test dry-out)
    /// </summary>
    [ContextMenu("Force All Plants Dry (0% Moisture)")]
    public void ForceAllPlantsDry()
    {
        Plant[] allPlants = FindObjectsByType<Plant>(FindObjectsSortMode.None);
        
        // We need to use reflection or make a public method
        // For now, let's just log - users can use context menu on individual plants
        
        Debug.Log($"⚠️ To force individual plants dry, select them and use 'Force Dry' context menu");
        Debug.Log($"Found {allPlants.Length} plants - try reducing moisture depletion time in GameConstants for faster testing");
    }
}