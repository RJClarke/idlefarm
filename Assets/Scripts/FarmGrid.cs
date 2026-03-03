using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages 4 separate zones of soil tiles
/// NOW WITH UPGRADE INTEGRATION - Grid size and zone unlocks affect layout
/// PIXEL PERFECT: Optimized for 32 PPU and 1080x1920 resolution
/// </summary>
public class FarmGrid : MonoBehaviour
{
    public static FarmGrid Instance { get; private set; }

    [Header("Grid Layout")]
    [SerializeField] private GridLayoutType layoutType = GridLayoutType.Horizontal;
    
    [Header("Grid Configuration - PIXEL PERFECT (32 PPU)")]
    [SerializeField] private int tilesPerZone = 2; // Default 2x2, upgrades increase this
    [SerializeField] private float tileSize = 1.0f; // 32 pixels per tile (pixel perfect!)
    [SerializeField] private float gapBetweenTiles = 0.0f; // No gap for seamless look
    [SerializeField] private float gapBetweenZones = 0.5f; // 16 pixel gap between zones
    [SerializeField] private float verticalOffset = 0f; // Manual vertical adjustment (0 = auto-center)

    [Header("Tile Prefab")]
    [SerializeField] private GameObject tilePrefab;

    [Header("Tilling Costs")]
    [SerializeField] private int tempTillCost = 10; // Money cost per tile (temporary)
    [SerializeField] private int permTillCost = 50; // Coin cost per tile (permanent)

    // Public accessors for other systems to check costs
    public int TempTillCost => tempTillCost;
    public int PermTillCost => permTillCost;

    // Storage for all tiles organized by zone
    private Dictionary<int, SoilTile[,]> zoneGrids = new Dictionary<int, SoilTile[,]>();
    private List<SoilTile> allTiles = new List<SoilTile>();

    // Zone parent objects for organization
    private GameObject zone1Parent, zone2Parent, zone3Parent, zone4Parent;

    // Bounds for camera adjustment
    public float TotalGridWidth { get; private set; }
    public float TotalGridHeight { get; private set; }

    // Exposes tile size for threat system grazing radius calculations
    public float TileSize => tileSize;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Apply upgrades BEFORE creating zones
        ApplyUpgrades();
        
        CreateAllZones();

        // Subscribe to run events
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += OnRunStarted;
        }

        // Subscribe to upgrade events to regenerate grid
        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.OnUpgradePurchased += OnUpgradePurchased;
        }

        // Notify camera controller of grid bounds
        NotifyCameraController();
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= OnRunStarted;
        }

        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.OnUpgradePurchased -= OnUpgradePurchased;
        }
    }

    /// <summary>
    /// Apply grid size from upgrades
    /// </summary>
    private void ApplyUpgrades()
    {
        if (UpgradeManager.Instance == null) return;

        int gridSizeLevel = UpgradeManager.Instance.GetPermanentLevel("grid_size");
        tilesPerZone = 2 + gridSizeLevel; // Level 0→2, Level 1→3, Level 2→4, Level 3→5
    }

    /// <summary>
    /// Called when player purchases an upgrade
    /// </summary>
    private void OnUpgradePurchased(string upgradeId)
    {
        // Only regenerate for grid-related upgrades
        if (upgradeId == "grid_size" || 
            upgradeId == "zone_unlock_2" || 
            upgradeId == "zone_unlock_3" || 
            upgradeId == "zone_unlock_4")
        {
            RegenerateGrid();
        }
    }

    /// <summary>
    /// Destroy existing grid and create new one with updated settings
    /// </summary>
    public void RegenerateGrid()
    {
        // Clear existing tiles
        foreach (SoilTile tile in allTiles)
        {
            if (tile != null)
            {
                Destroy(tile.gameObject);
            }
        }
        allTiles.Clear();
        zoneGrids.Clear();

        // Destroy zone parents
        if (zone1Parent != null) Destroy(zone1Parent);
        if (zone2Parent != null) Destroy(zone2Parent);
        if (zone3Parent != null) Destroy(zone3Parent);
        if (zone4Parent != null) Destroy(zone4Parent);

        // Apply upgrades again
        ApplyUpgrades();

        // Recreate zones
        CreateAllZones();

        // Update camera
        NotifyCameraController();

    }

    /// <summary>
    /// Create all 4 zones based on selected layout type
    /// Only shows zones that are unlocked
    /// </summary>
    private void CreateAllZones()
    {
        if (tilePrefab == null)
        {
            Debug.LogError("Tile Prefab not assigned in FarmGrid!");
            return;
        }

        // Calculate zone dimensions
        float zoneWidth = (tilesPerZone * tileSize) + ((tilesPerZone - 1) * gapBetweenTiles);
        float zoneHeight = zoneWidth; // Zones are square

        // Create parent objects for organization
        zone1Parent = new GameObject("Zone 1");
        zone2Parent = new GameObject("Zone 2");
        zone3Parent = new GameObject("Zone 3");
        zone4Parent = new GameObject("Zone 4");

        zone1Parent.transform.parent = transform;
        zone2Parent.transform.parent = transform;
        zone3Parent.transform.parent = transform;
        zone4Parent.transform.parent = transform;

        Vector3[] zonePositions;

        if (layoutType == GridLayoutType.Horizontal)
        {
            // Horizontal layout: all zones in a row
            zonePositions = CalculateHorizontalLayout(zoneWidth);
        }
        else
        {
            // 2x2 layout: zones in a square
            zonePositions = Calculate2x2Layout(zoneWidth);
        }

        // Create each zone at calculated positions
        CreateZone(1, zonePositions[0], zone1Parent.transform);
        CreateZone(2, zonePositions[1], zone2Parent.transform);
        CreateZone(3, zonePositions[2], zone3Parent.transform);
        CreateZone(4, zonePositions[3], zone4Parent.transform);

        // Hide zones that aren't unlocked
        ApplyZoneVisibility();

    }

    /// <summary>
    /// Show/hide zones based on unlock upgrades
    /// </summary>
    private void ApplyZoneVisibility()
    {
        if (UpgradeManager.Instance == null)
        {
            // No upgrade manager - show only zone 1
            if (zone2Parent != null) zone2Parent.SetActive(false);
            if (zone3Parent != null) zone3Parent.SetActive(false);
            if (zone4Parent != null) zone4Parent.SetActive(false);
            return;
        }

        // Zone 1 always visible
        if (zone1Parent != null) zone1Parent.SetActive(true);

        // Check unlock status for other zones
        bool zone2Unlocked = UpgradeManager.Instance.GetPermanentLevel("zone_unlock_2") > 0;
        bool zone3Unlocked = UpgradeManager.Instance.GetPermanentLevel("zone_unlock_3") > 0;
        bool zone4Unlocked = UpgradeManager.Instance.GetPermanentLevel("zone_unlock_4") > 0;

        if (zone2Parent != null) zone2Parent.SetActive(zone2Unlocked);
        if (zone3Parent != null) zone3Parent.SetActive(zone3Unlocked);
        if (zone4Parent != null) zone4Parent.SetActive(zone4Unlocked);
    }

    /// <summary>
    /// Calculate zone positions for horizontal layout
    /// [Zone1] [Zone2] [Zone3] [Zone4]
    /// </summary>
    private Vector3[] CalculateHorizontalLayout(float zoneWidth)
    {
        // Total width calculation
        TotalGridWidth = (zoneWidth * 4) + (gapBetweenZones * 3);
        TotalGridHeight = zoneWidth;

        // Starting X (centered)
        float startX = -(TotalGridWidth / 2f) + (zoneWidth / 2f);
        
        // Y position (with manual offset)
        float yPos = verticalOffset;

        Vector3[] positions = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            float xPos = startX + (i * (zoneWidth + gapBetweenZones));
            positions[i] = new Vector3(xPos, yPos, 0f);
        }

        return positions;
    }

    /// <summary>
    /// Calculate zone positions for 2x2 layout
    /// [Zone1] [Zone2]
    /// [Zone3] [Zone4]
    /// </summary>
    private Vector3[] Calculate2x2Layout(float zoneWidth)
    {
        // Total width and height
        TotalGridWidth = (zoneWidth * 2) + gapBetweenZones;
        TotalGridHeight = (zoneWidth * 2) + gapBetweenZones;

        float halfWidth = TotalGridWidth / 2f;
        float halfHeight = TotalGridHeight / 2f;

        // Offset for centering
        float offsetX = zoneWidth / 2f;
        float offsetY = zoneWidth / 2f;

        Vector3[] positions = new Vector3[4];
        
        // Zone 1: Top-left
        positions[0] = new Vector3(-halfWidth + offsetX, halfHeight - offsetY + verticalOffset, 0f);
        
        // Zone 2: Top-right
        positions[1] = new Vector3(halfWidth - offsetX, halfHeight - offsetY + verticalOffset, 0f);
        
        // Zone 3: Bottom-left
        positions[2] = new Vector3(-halfWidth + offsetX, -halfHeight + offsetY + verticalOffset, 0f);
        
        // Zone 4: Bottom-right
        positions[3] = new Vector3(halfWidth - offsetX, -halfHeight + offsetY + verticalOffset, 0f);

        return positions;
    }

    /// <summary>
    /// Create a single zone of tiles at the given position
    /// </summary>
    private void CreateZone(int zoneID, Vector3 zoneCenter, Transform parent)
    {
        SoilTile[,] zoneGrid = new SoilTile[tilesPerZone, tilesPerZone];

        // Calculate starting position (top-left of zone)
        float zoneWidth = (tilesPerZone * tileSize) + ((tilesPerZone - 1) * gapBetweenTiles);
        float halfZoneWidth = zoneWidth / 2f;
        
        Vector3 startPos = new Vector3(
            zoneCenter.x - halfZoneWidth + (tileSize / 2f),
            zoneCenter.y + halfZoneWidth - (tileSize / 2f),
            0f
        );

        for (int x = 0; x < tilesPerZone; x++)
        {
            for (int y = 0; y < tilesPerZone; y++)
            {
                // Calculate tile position
                float xPos = startPos.x + (x * (tileSize + gapBetweenTiles));
                float yPos = startPos.y - (y * (tileSize + gapBetweenTiles));
                Vector3 tilePos = new Vector3(xPos, yPos, 0f);

                // Instantiate tile
                GameObject tileObj = Instantiate(tilePrefab, tilePos, Quaternion.identity, parent);
                tileObj.name = $"Tile [{zoneID}] ({x},{y})";

                // Set tile scale
                tileObj.transform.localScale = new Vector3(tileSize, tileSize, 1f);

                // Initialize SoilTile component
                SoilTile tile = tileObj.GetComponent<SoilTile>();
                if (tile != null)
                {
                    tile.Initialize(zoneID, x, y);
                    zoneGrid[x, y] = tile;
                    allTiles.Add(tile);
                }
                else
                {
                    Debug.LogError("Tile prefab doesn't have SoilTile component!");
                }
            }
        }

        zoneGrids[zoneID] = zoneGrid;
    }

    /// <summary>
    /// Notify camera controller of grid bounds for auto-fit
    /// </summary>
    private void NotifyCameraController()
    {
        MobileCameraController cameraController = Camera.main?.GetComponent<MobileCameraController>();
        if (cameraController != null)
        {
            cameraController.UpdateGridBounds(TotalGridWidth, TotalGridHeight);
        }
    }

    /// <summary>
    /// Get tile at specific zone and grid coordinates
    /// </summary>
    public SoilTile GetTile(int zoneID, int x, int y)
    {
        if (!zoneGrids.ContainsKey(zoneID))
        {
            Debug.LogWarning($"Zone {zoneID} doesn't exist!");
            return null;
        }

        if (x < 0 || x >= tilesPerZone || y < 0 || y >= tilesPerZone)
        {
            return null;
        }

        return zoneGrids[zoneID][x, y];
    }

    /// <summary>
    /// Get all tiles in a specific zone
    /// </summary>
    public List<SoilTile> GetZoneTiles(int zoneID)
    {
        List<SoilTile> tiles = new List<SoilTile>();
        
        if (!zoneGrids.ContainsKey(zoneID))
        {
            return tiles;
        }

        SoilTile[,] zone = zoneGrids[zoneID];
        for (int x = 0; x < tilesPerZone; x++)
        {
            for (int y = 0; y < tilesPerZone; y++)
            {
                tiles.Add(zone[x, y]);
            }
        }

        return tiles;
    }

    /// <summary>
    /// Get all tiles that can be planted on (tilled and empty)
    /// </summary>
    public List<SoilTile> GetPlantableTiles()
    {
        List<SoilTile> plantable = new List<SoilTile>();
        foreach (SoilTile tile in allTiles)
        {
            if (tile.CanPlant)
            {
                plantable.Add(tile);
            }
        }
        return plantable;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Threat System Zone Queries (Phase 6)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all tiles in the given zone that have a living plant.
    /// Used by CrowThreat and DeerThreat to find valid targets in their locked zone.
    /// </summary>
    public List<SoilTile> GetOccupiedTilesInZone(int zoneId)
    {
        List<SoilTile> result = new List<SoilTile>();
        foreach (SoilTile tile in allTiles)
        {
            if (tile.ZoneID == zoneId && tile.IsOccupied)
                result.Add(tile);
        }
        return result;
    }

    /// <summary>
    /// Returns all occupied tiles across the entire farm.
    /// Used by ThreatWaveManager to verify zones have plants before sending animals.
    /// </summary>
    public List<SoilTile> GetOccupiedTiles()
    {
        List<SoilTile> result = new List<SoilTile>();
        foreach (SoilTile tile in allTiles)
        {
            if (tile.IsOccupied) result.Add(tile);
        }
        return result;
    }

    /// <summary>
    /// Returns the world-space center of the given zone by averaging tile positions.
    /// Used by DeerThreat and CrowThreat as the destination point when entering the farm.
    /// Falls back to Vector3.zero if the zone has no tiles.
    /// </summary>
    public Vector3 GetZoneCenter(int zoneId)
    {
        Vector3 sum   = Vector3.zero;
        int     count = 0;

        foreach (SoilTile tile in allTiles)
        {
            if (tile.ZoneID == zoneId)
            {
                sum += tile.transform.position;
                count++;
            }
        }

        return count > 0 ? sum / count : Vector3.zero;
    }

    /// <summary>
    /// Returns all zone IDs that currently have tiles (active/unlocked zones).
    /// Used by ThreatWaveManager to pick a valid zone to target each wave.
    /// </summary>
    public List<int> GetActiveZoneIds()
    {
        HashSet<int> ids = new HashSet<int>();
        foreach (SoilTile tile in allTiles)
            ids.Add(tile.ZoneID);

        List<int> result = new List<int>(ids);
        result.Sort();
        return result;
    }

    /// <summary>
    /// Returns ALL tiles in currently visible (unlocked/active) zones —
    /// tilled or untilled, occupied or empty.
    ///
    /// Used by lightning strikes, which can hit any tile in an unlocked zone
    /// regardless of whether anything is planted there.
    ///
    /// Filters by activeInHierarchy so tiles in hidden (locked) zone parents
    /// are automatically excluded.
    /// </summary>
    public List<SoilTile> GetAllUnlockedTiles()
    {
        List<SoilTile> result = new List<SoilTile>();
        foreach (SoilTile tile in allTiles)
        {
            if (tile.gameObject.activeInHierarchy)
                result.Add(tile);
        }
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Get all untilled tiles
    /// </summary>
    public List<SoilTile> GetUntilledTiles()
    {
        List<SoilTile> untilled = new List<SoilTile>();
        foreach (SoilTile tile in allTiles)
        {
            if (tile.State == TileState.Untilled)
            {
                untilled.Add(tile);
            }
        }
        return untilled;
    }

    /// <summary>
    /// Called when a new run starts - reset temporary tilling
    /// </summary>
    private void OnRunStarted()
    {
        foreach (SoilTile tile in allTiles)
        {
            tile.ResetForNewRun();
        }

    }

    /// <summary>
    /// Till all tiles temporarily (for testing)
    /// </summary>
    [ContextMenu("Till All Tiles (Temporary)")]
    private void TillAllTemporary()
    {
        int count = 0;
        foreach (SoilTile tile in allTiles)
        {
            if (tile.TillTemporary(0)) // Free for testing
            {
                count++;
            }
        }

        Debug.Log($"✓ Tilled {count} tiles temporarily (testing)");
    }

    /// <summary>
    /// Till all tiles permanently (for testing)
    /// </summary>
    [ContextMenu("Till All Tiles (Permanent)")]
    private void TillAllPermanent()
    {
        int count = 0;
        foreach (SoilTile tile in allTiles)
        {
            if (tile.TillPermanent(0)) // Free for testing
            {
                count++;
            }
        }

        Debug.Log($"✓ Tilled {count} tiles permanently (testing)");
    }
}

/// <summary>
/// Layout types for farm grid
/// </summary>
public enum GridLayoutType
{
    Horizontal, // [1][2][3][4] in a row
    Grid2x2     // [1][2]
                // [3][4] in a square
}