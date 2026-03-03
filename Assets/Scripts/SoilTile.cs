using UnityEngine;

/// <summary>
/// Represents a single soil tile on the farm
/// Phase 3.1: Now visually displays moisture state via overlay
/// </summary>
public class SoilTile : MonoBehaviour
{
    [Header("Tile State")]
    [SerializeField] private TileState currentState = TileState.Untilled;
    [SerializeField] private bool isPermanentlyTilled = false;

    [Header("Zone Info")]
    [SerializeField] private int zoneID;
    [SerializeField] private int gridX;
    [SerializeField] private int gridY;

    [Header("Visual - Base Soil")]
    [SerializeField] private SpriteRenderer baseSpriteRenderer;
    [SerializeField] private Color untilledColor = new Color(0.35f, 0.25f, 0.15f, 1f); // Dark brown
    [SerializeField] private Color tilledColor = new Color(0.55f, 0.40f, 0.25f, 1f);   // Lighter brown

    [Header("Visual - Moisture Overlay")]
    [SerializeField] private SpriteRenderer moistureOverlay;
    [SerializeField] private Color wetColor = new Color(0f, 0f, 0f, 1f); // Black for wet
    [SerializeField] private float maxWetAlpha = 0.6f; // Max darkness when fully wet
    [SerializeField] private Color dryColor = new Color(1f, 1f, 1f, 1f); // White for dry
    [SerializeField] private float maxDryAlpha = 0.2f; // Max lightness when fully dry
    [SerializeField] private float transitionPoint = 20f; // Moisture % where we switch from black to white
    [SerializeField] private Color driedOutColor = new Color(0.7f, 0.65f, 0.6f, 1f); // Light grey-beige for dried out

    [Header("Moisture Display Settings")]
    [SerializeField] private bool showMoistureOverlay = true;
    [SerializeField] private bool showDriedOutState = true;

    // Reference to plant on this tile
    private GameObject currentPlant;
    private Plant plantComponent;

    // Properties
    public TileState State => currentState;
    public bool IsPermanentlyTilled => isPermanentlyTilled;
    public int ZoneID => zoneID;
    public int GridX => gridX;
    public int GridY => gridY;
    public bool IsOccupied => currentPlant != null;
    public GameObject CurrentPlant => currentPlant;
    public bool CanPlant => currentState == TileState.Tilled && !IsOccupied;

    private void Awake()
    {
        if (baseSpriteRenderer == null)
        {
            baseSpriteRenderer = GetComponent<SpriteRenderer>();
        }

        // Create moisture overlay if it doesn't exist
        if (moistureOverlay == null)
        {
            CreateMoistureOverlay();
        }
    }

    private void Update()
    {
        // Update moisture overlay based on plant's moisture
        if (plantComponent != null && showMoistureOverlay)
        {
            UpdateMoistureVisuals(plantComponent.CurrentMoisture, plantComponent.IsDriedOut);
        }
    }

    /// <summary>
    /// Initialize tile with zone and grid position
    /// </summary>
    public void Initialize(int zone, int x, int y, bool permanentlyTilled = false)
    {
        zoneID = zone;
        gridX = x;
        gridY = y;
        isPermanentlyTilled = permanentlyTilled;

        if (isPermanentlyTilled)
        {
            currentState = TileState.Tilled;
        }

        UpdateBaseVisuals();
        HideMoistureOverlay(); // No plant yet
    }

    /// <summary>
    /// Create the moisture overlay sprite renderer
    /// </summary>
    private void CreateMoistureOverlay()
    {
        GameObject overlayObj = new GameObject("MoistureOverlay");
        overlayObj.transform.SetParent(transform, false);
        overlayObj.transform.localPosition = Vector3.zero;

        moistureOverlay = overlayObj.AddComponent<SpriteRenderer>();
        
        // Moisture overlay should be ABOVE base soil but BELOW plants
        // Base soil is usually at 0, plants at 10, so we'll use 5
        moistureOverlay.sortingOrder = 5;
        
        // Use same sprite as base (or assign a different overlay sprite)
        moistureOverlay.sprite = baseSpriteRenderer.sprite;
        
        // Start invisible
        Color c = wetColor;
        c.a = 0f;
        moistureOverlay.color = c;
        
        Debug.Log($"Created moisture overlay for tile [{zoneID}]({gridX},{gridY}) at sorting order {moistureOverlay.sortingOrder}");
    }

    /// <summary>
    /// Update moisture overlay based on plant's moisture level
    /// 100% -> 20%: Black overlay fading out (dark = wet)
    /// 20% -> 0%: White overlay fading in (light = dry)
    /// </summary>
    private void UpdateMoistureVisuals(float moisturePercent, bool isDriedOut)
    {
        if (moistureOverlay == null) return;

        // Check for dried out state (Phase 3.3)
        if (isDriedOut && showDriedOutState)
        {
            // Show dried-out overlay (special color)
            Color c = driedOutColor;
            c.a = 0.4f;
            moistureOverlay.color = c;
        }
        else
        {
            // Smooth transition: Black (wet) -> Normal -> White (dry)
            
            if (moisturePercent > transitionPoint)
            {
                // WET ZONE (100% -> 20%): Black overlay fading out
                // At 100%: maxWetAlpha (0.6)
                // At 20%: 0.0
                float wetRange = 100f - transitionPoint; // 80
                float wetPercent = (moisturePercent - transitionPoint) / wetRange; // 0.0 to 1.0
                float alpha = Mathf.Lerp(0f, maxWetAlpha, wetPercent);
                
                Color c = wetColor; // Black
                c.a = alpha;
                moistureOverlay.color = c;
            }
            else
            {
                // DRY ZONE (20% -> 0%): White overlay fading in
                // At 20%: 0.0
                // At 0%: maxDryAlpha (0.2)
                float dryPercent = moisturePercent / transitionPoint; // 0.0 to 1.0
                float alpha = Mathf.Lerp(maxDryAlpha, 0f, dryPercent); // Inverted
                
                Color c = dryColor; // White
                c.a = alpha;
                moistureOverlay.color = c;
            }
            
            // Debug logging (reduced frequency)
            if (Time.frameCount % 120 == 0)
            {
                string zone = moisturePercent > transitionPoint ? "WET" : "DRY";
                Debug.Log($"Tile [{zoneID}]({gridX},{gridY}): {moisturePercent:F0}% moisture → {zone} zone, Alpha: {moistureOverlay.color.a:F2}");
            }
        }
    }

    /// <summary>
    /// Hide moisture overlay (when no plant on tile)
    /// </summary>
    private void HideMoistureOverlay()
    {
        if (moistureOverlay != null)
        {
            Color c = wetColor; // Black by default
            c.a = 0f;
            moistureOverlay.color = c;
        }
    }

    /// <summary>
    /// Till this tile temporarily
    /// </summary>
    public bool TillTemporary(int cost)
    {
        if (currentState == TileState.Tilled)
        {
            return false;
        }

        if (isPermanentlyTilled)
        {
            currentState = TileState.Tilled;
            UpdateBaseVisuals();
            return true;
        }

        if (CurrencyManager.Instance != null && CurrencyManager.Instance.SpendMoney(cost))
        {
            currentState = TileState.Tilled;
            UpdateBaseVisuals();
            Debug.Log($"Tilled tile [{zoneID}]({gridX},{gridY}) for ${cost} (temporary)");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Till this tile permanently
    /// </summary>
    public bool TillPermanent(int cost)
    {
        if (isPermanentlyTilled)
        {
            return false;
        }

        if (CurrencyManager.Instance != null && CurrencyManager.Instance.SpendCoins(cost))
        {
            isPermanentlyTilled = true;
            currentState = TileState.Tilled;
            UpdateBaseVisuals();
            Debug.Log($"Permanently tilled tile [{zoneID}]({gridX},{gridY}) for {cost} coins");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Reset for new run
    /// </summary>
    public void ResetForNewRun()
    {
        if (isPermanentlyTilled)
        {
            currentState = TileState.Tilled;
        }
        else
        {
            currentState = TileState.Untilled;
        }

        if (currentPlant != null)
        {
            Destroy(currentPlant);
            currentPlant = null;
            plantComponent = null;
        }

        UpdateBaseVisuals();
        HideMoistureOverlay();
    }

    /// <summary>
    /// Plant a crop on this tile
    /// </summary>
    public bool PlantCrop(GameObject plantPrefab, CropData cropData)
    {
        if (!CanPlant)
        {
            return false;
        }

        if (plantPrefab == null || cropData == null)
        {
            Debug.LogError("Cannot plant - missing prefab or crop data!");
            return false;
        }

        GameObject newPlant = Instantiate(plantPrefab, transform.position, Quaternion.identity, transform);
        currentPlant = newPlant;

        // Get Plant component reference
        plantComponent = newPlant.GetComponent<Plant>();
        if (plantComponent != null)
        {
            plantComponent.Initialize(cropData, this);
        }
        else
        {
            Debug.LogError("Plant prefab doesn't have Plant component!");
            Destroy(newPlant);
            currentPlant = null;
            return false;
        }
        
        Debug.Log($"Planted {cropData.cropName} at [{zoneID}]({gridX},{gridY})");
        
        // Show moisture overlay at full (plant starts at 100% moisture)
        if (showMoistureOverlay)
        {
            UpdateMoistureVisuals(100f, false);
        }
        
        return true;
    }

    /// <summary>
    /// Remove plant from this tile
    /// </summary>
    public void ClearPlant()
    {
        if (currentPlant != null)
        {
            Destroy(currentPlant.gameObject);
            currentPlant = null;
            plantComponent = null;
            HideMoistureOverlay();
        }
    }

    /// <summary>
    /// Update base soil color
    /// </summary>
    private void UpdateBaseVisuals()
    {
        if (baseSpriteRenderer == null) return;

        switch (currentState)
        {
            case TileState.Untilled:
                baseSpriteRenderer.color = untilledColor;
                break;
            case TileState.Tilled:
                baseSpriteRenderer.color = tilledColor;
                break;
        }
    }

    /// <summary>
    /// Get save data
    /// </summary>
    public TileSaveData GetSaveData()
    {
        return new TileSaveData
        {
            zoneID = this.zoneID,
            gridX = this.gridX,
            gridY = this.gridY,
            isPermanentlyTilled = this.isPermanentlyTilled
        };
    }

    /// <summary>
    /// Load save data
    /// </summary>
    public void LoadSaveData(TileSaveData data)
    {
        isPermanentlyTilled = data.isPermanentlyTilled;
        if (isPermanentlyTilled)
        {
            currentState = TileState.Tilled;
            UpdateBaseVisuals();
        }
    }
}

public enum TileState
{
    Untilled,
    Tilled
}

[System.Serializable]
public class TileSaveData
{
    public int zoneID;
    public int gridX;
    public int gridY;
    public bool isPermanentlyTilled;
}