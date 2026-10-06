using UnityEngine;

/// <summary>
/// Represents a single soil tile on the farm
/// Phase 3.1: Now visually displays moisture state via overlay
/// </summary>
public class SoilTile : MonoBehaviour
{
    [Header("Tile State")]
    [SerializeField] private TileState currentState = TileState.Untilled;
    [Tooltip("Pre-Tilled Soil (Farm upgrade): this tile starts every run tilled. It can still go " +
             "fallow mid-run like any other tile.")]
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

    // Fallow decay: seconds (scaled run time) this tile has sat tilled but empty. FarmGrid drives it
    // during runs; at its threshold the tile reverts to untilled and must be tilled again.
    private float fallowTimer;
    private float fallowTint; // 0..1 how far the soil colour has slid back toward untilled

    // Reserved by equipment (e.g. a sprinkler sitting on the zone's center tile). A blocked tile
    // can't be tilled or planted, so it never gets worked/watered/harvested by helpers and stays
    // clear for the equipment to remain visible. Recomputed each run by FarmGrid.
    private bool isBlocked;

    // Properties
    public TileState State => currentState;
    public bool IsPermanentlyTilled => isPermanentlyTilled;
    public int ZoneID => zoneID;
    public int GridX => gridX;
    public int GridY => gridY;
    public bool IsOccupied => currentPlant != null;
    public GameObject CurrentPlant => currentPlant;
    public bool CanPlant => currentState == TileState.Tilled && !IsOccupied && !isBlocked;
    public bool IsBlocked => isBlocked;
    public float FallowTimer => fallowTimer;

    /// <summary>Reserve/unreserve this tile (e.g. for a sprinkler). Blocked tiles can't be tilled or planted.</summary>
    public void SetBlocked(bool blocked) => isBlocked = blocked;

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
        if (isBlocked) return false;
        if (currentState == TileState.Tilled)
        {
            return false;
        }

        if (CurrencyManager.Instance != null && CurrencyManager.Instance.SpendMoney(cost))
        {
            SetTilled();
            if (RunStats.Instance != null) RunStats.Instance.AddTileTilled();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Till this tile for free (used by helpers during a run).
    /// </summary>
    public bool TillByHelper()
    {
        if (isBlocked) return false;
        if (currentState == TileState.Tilled) return false;

        SetTilled();
        if (RunStats.Instance != null) RunStats.Instance.AddTileTilled();
        return true;
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
            SetTilled();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Mark/unmark this tile as Pre-Tilled (Farm upgrade). Between runs the soil flips immediately so
    /// the purchase is visible on the home farm; during a run only the flag changes (next run uses it).
    /// </summary>
    public void SetPreTilled(bool preTilled, bool applyNow)
    {
        isPermanentlyTilled = preTilled;
        if (!applyNow || IsOccupied) return;
        if (preTilled) SetTilled();
        else if (currentState == TileState.Tilled) Untill();
    }

    /// <summary>
    /// Advance fallow decay by <paramref name="dt"/> (call only while the tile is tilled and empty).
    /// The soil colour slides back toward untilled over the last <paramref name="warnFraction"/> of
    /// the window. Returns true once the timer reaches <paramref name="fallowSeconds"/>; the caller
    /// decides whether to <see cref="Untill"/> (it holds the tile while a helper is coming to plant).
    /// </summary>
    public bool TickFallow(float dt, float fallowSeconds, float warnFraction)
    {
        fallowTimer = Mathf.Min(fallowTimer + dt, fallowSeconds);
        float t = fallowSeconds <= 0f ? 1f : fallowTimer / fallowSeconds;
        float warnStart = 1f - Mathf.Clamp01(warnFraction);
        float tint = warnStart >= 1f ? 0f : Mathf.Clamp01((t - warnStart) / (1f - warnStart));
        if (!Mathf.Approximately(tint, fallowTint))
        {
            fallowTint = tint;
            UpdateBaseVisuals();
        }
        return fallowTimer >= fallowSeconds;
    }

    /// <summary>Stop fallow decay (tile got planted/worked) and restore the full tilled colour.</summary>
    public void ResetFallow()
    {
        if (fallowTimer == 0f && fallowTint == 0f) return;
        fallowTimer = 0f;
        fallowTint = 0f;
        UpdateBaseVisuals();
    }

    /// <summary>The tile went fallow (or was trampled): back to untilled, must be tilled again.</summary>
    public void Untill()
    {
        currentState = TileState.Untilled;
        fallowTimer = 0f;
        fallowTint = 0f;
        UpdateBaseVisuals();
    }

    private void SetTilled()
    {
        currentState = TileState.Tilled;
        fallowTimer = 0f;
        fallowTint = 0f;
        UpdateBaseVisuals();
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

        fallowTimer = 0f;
        fallowTint = 0f;
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
        ResetFallow();

        // Get Plant component reference
        plantComponent = newPlant.GetComponent<Plant>();
        if (plantComponent != null)
        {
            plantComponent.Initialize(cropData, this);
            if (RunStats.Instance != null) RunStats.Instance.AddSeedPlanted(zoneID, cropData);
        }
        else
        {
            Debug.LogError("Plant prefab doesn't have Plant component!");
            Destroy(newPlant);
            currentPlant = null;
            return false;
        }
        
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
                // Going fallow: slide back toward the untilled colour as the decay window runs out.
                baseSpriteRenderer.color = Color.Lerp(tilledColor, untilledColor, fallowTint);
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