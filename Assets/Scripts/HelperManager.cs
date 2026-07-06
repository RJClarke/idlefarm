using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages all helpers on the farm
/// Phase 5.1: Spawns, tracks, and coordinates helpers
/// MODIFIED: Now supports runtime zone seed configuration from popup
/// FIXED: Only works on unlocked zones, properly handles cross-zone tasks
/// </summary>
public class HelperManager : MonoBehaviour
{
    public static HelperManager Instance { get; private set; }

    [Header("Helper Prefabs")]
    [Tooltip("Universal helper that does all tasks (harvest, water, plant)")]
    [SerializeField] private GameObject universalHelperPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private Transform helperSpawnPoint;
    [SerializeField] private Vector3 defaultSpawnPosition = new Vector3(0f, 2f, 0f);

    [Header("Planting Configuration")]
    [Tooltip("Seed type for each zone (Zone 1-4) - OPTIONAL: Overridden by popup selection at runtime")]
    [SerializeField] private CropData zone1Seed;
    [SerializeField] private CropData zone2Seed;
    [SerializeField] private CropData zone3Seed;
    [SerializeField] private CropData zone4Seed;

    public enum PlantingStrategy
    {
        ClosestTile,    // Plant on nearest empty tile
        ZoneBased,      // Fill zone 1, then 2, then 3, then 4
        Random          // Random empty tile
    }
    [SerializeField] private PlantingStrategy plantingStrategy = PlantingStrategy.ClosestTile;

    [Header("Priority Tuning")]
    [Tooltip("How much distance affects task scoring (0-1). Higher = helpers stick to nearby work")]
    [Range(0f, 1f)]
    [SerializeField] private float distanceWeight = 0.6f;

    [Header("Active Helpers")]
    [SerializeField] private List<Helper> activeHelpers = new List<Helper>();

    [Header("Task Queue")]
    [SerializeField] private List<HelperTask> pendingTasks = new List<HelperTask>();
    [SerializeField] private float taskScanInterval = 1f; // How often to scan for new tasks
    private float taskScanTimer = 0f;

    [Header("Performance Optimization")]
    [Tooltip("Cache plant references to avoid FindObjectsByType every frame")]
    private List<Plant> cachedPlants = new List<Plant>();
    private float plantCacheTimer = 0f;
    [SerializeField] private float plantCacheInterval = 2f; // Rebuild plant cache every 2 seconds
    [SerializeField] private bool showDebugInfo = false;

    // MODIFIED: Runtime zone seed configuration from popup
    private Dictionary<int, CropData> currentZoneSeeds = new Dictionary<int, CropData>();

    // Home screen cosmetic helpers
    private List<GameObject> homeScreenHelpers = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Ensure lists are initialized
        if (activeHelpers == null) activeHelpers = new List<Helper>();
        if (pendingTasks == null) pendingTasks = new List<HelperTask>();
    }

    private void Start()
    {
        // Subscribe to run events for auto-spawn
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += AutoSpawnHelpers;
            RunManager.Instance.OnRunEnded += ClearAllHelpers;

            // Resume-on-reopen fires OnRunStarted during load — possibly before this Start ran,
            // so we'd miss the event and never spawn. If a run is already active, catch up now.
            if (RunManager.Instance.IsRunActive) AutoSpawnHelpers();
        }

        // Refresh home screen helpers when a new slot is unlocked
        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.OnUpgradePurchased += OnUpgradePurchased;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from run events
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= AutoSpawnHelpers;
            RunManager.Instance.OnRunEnded -= ClearAllHelpers;
        }

        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.OnUpgradePurchased -= OnUpgradePurchased;
        }
    }

    private void OnUpgradePurchased(string upgradeID)
    {
        if (upgradeID != "helper_slot_unlock") return;

        bool inRun = RunManager.Instance != null && RunManager.Instance.IsRunActive;
        if (inRun)
        {
            // Rent purchased mid-run — spawn the extra working helper immediately.
            SpawnUniversalHelper();
        }
        else
        {
            // Permanent slot unlocked on home screen — add a cosmetic helper.
            SpawnOneHomeHelper();
        }
    }

    /// <summary>
    /// Hard cap for helper slots is 4. During a run, rent stacks on permanent (capped at 4).
    /// Outside a run, only the permanent level counts (stale temp from prior run is ignored).
    /// </summary>
    public const int MAX_HELPER_SLOTS = 4;

    public int GetMaxHelperSlots()
    {
        if (UpgradeManager.Instance == null) return 1;
        bool inRun = RunManager.Instance != null && RunManager.Instance.IsRunActive;
        int level = inRun
            ? UpgradeManager.Instance.GetCurrentLevel("helper_slot_unlock")
            : UpgradeManager.Instance.GetPermanentLevel("helper_slot_unlock");
        return Mathf.Min(MAX_HELPER_SLOTS, level + 1);
    }

    /// <summary>
    /// Spawn a single cosmetic home screen helper (additive, doesn't disturb existing ones).
    /// </summary>
    private void SpawnOneHomeHelper()
    {
        if (universalHelperPrefab == null) return;

        Vector3 spawnPos = helperSpawnPoint != null ? helperSpawnPoint.position : defaultSpawnPosition;
        spawnPos.x += Random.Range(-1f, 1f);
        spawnPos.y += Random.Range(-0.5f, 0.5f);

        GameObject helperObj = Instantiate(universalHelperPrefab, spawnPos, Quaternion.identity, transform);
        helperObj.name = $"HomeHelper_{homeScreenHelpers.Count + 1}";
        helperObj.AddComponent<HomeHelperWander>();

        Helper helper = helperObj.GetComponent<Helper>();
        if (helper != null)
            helper.enabled = false;

        homeScreenHelpers.Add(helperObj);
    }

    private void Update()
    {
        // Update plant cache periodically (PERFORMANCE FIX)
        plantCacheTimer += Time.deltaTime;
        if (plantCacheTimer >= plantCacheInterval)
        {
            plantCacheTimer = 0f;
            RebuildPlantCache();
        }
        
        // Scan for new tasks periodically
        taskScanTimer += Time.deltaTime;
        if (taskScanTimer >= taskScanInterval)
        {
            taskScanTimer = 0f;
            ScanForTasks();
        }

        // Clean up completed/invalid tasks (LESS FREQUENTLY - PERFORMANCE FIX)
        if (Time.frameCount % 30 == 0) // Only every 30 frames (~0.5 seconds)
        {
            CleanupTasks();
        }

        // Clean up null helpers (destroyed but not removed from list)
        CleanupHelpers();

        // Assign tasks to idle helpers
        AssignTasksToHelpers();
    }
    
    /// <summary>
    /// Rebuild cached plant list (PERFORMANCE FIX)
    /// Called every 2 seconds instead of every frame
    /// </summary>
    private void RebuildPlantCache()
    {
        cachedPlants.Clear();
        Plant[] allPlants = FindObjectsByType<Plant>(FindObjectsSortMode.None);
        cachedPlants.AddRange(allPlants);
        
        if (showDebugInfo && cachedPlants.Count > 0)
        {

        }
    }

    /// <summary>
    /// Remove null helpers from active list
    /// </summary>
    private void CleanupHelpers()
    {
        if (activeHelpers != null)
        {
            activeHelpers.RemoveAll(helper => helper == null);
        }
    }

    /// <summary>
    /// FIXED: Check if a zone is unlocked
    /// Zone 1 is always unlocked, others require upgrades
    /// </summary>
    private bool IsZoneUnlocked(int zoneID)
    {
        if (zoneID == 1) return true; // Zone 1 always unlocked

        if (UpgradeManager.Instance == null) return false;

        return UpgradeManager.Instance.GetPermanentLevel($"zone_unlock_{zoneID}") > 0;
    }

    /// <summary>
    /// MODIFIED: Set zone seeds for this run (called from RunManager after popup confirmation)
    /// </summary>
    public void SetZoneSeeds(Dictionary<int, CropData> zoneSeeds)
    {
        currentZoneSeeds.Clear();
        
        if (zoneSeeds != null)
        {
            foreach (var kvp in zoneSeeds)
            {
                currentZoneSeeds[kvp.Key] = kvp.Value;
            }
            


        }
    }

    /// <summary>
    /// Spawn a universal helper (does all tasks)
    /// </summary>
    public Helper SpawnUniversalHelper()
    {
        if (universalHelperPrefab == null)
        {
            Debug.LogError("Universal Helper prefab not assigned in HelperManager!");
            return null;
        }

        // Cap from the single source of truth (UpgradeManager). Hard ceiling = 4.
        {
            int maxHelpers = GetMaxHelperSlots();
            int currentCount = GetHelperCount();

            if (currentCount >= maxHelpers)
            {
                Debug.LogWarning($"❌ Cannot spawn helper - at max limit ({currentCount}/{maxHelpers})");
                return null;
            }
        }

        Vector3 spawnPos = helperSpawnPoint != null ? helperSpawnPoint.position : defaultSpawnPosition;
        GameObject helperObj = Instantiate(universalHelperPrefab, spawnPos, Quaternion.identity, transform);
        helperObj.AddComponent<CropAgitator>(); // rustles crops the helper brushes past

        Helper helper = helperObj.GetComponent<Helper>();
        if (helper != null)
        {
            activeHelpers.Add(helper);
            
            int current = GetHelperCount();
            int max = HelperUpgradeManager.Instance?.MaxHelpers ?? 1;

        }
        else
        {
            Debug.LogError("Universal Helper prefab doesn't have a Helper component!");
            Destroy(helperObj);
        }

        return helper;
    }

    /// <summary>
    /// Auto-spawn helpers at run start based on unlocked helper slots.
    /// Level 0 = 1 helper, Level 1 = 2 helpers, etc.
    /// </summary>
    private void AutoSpawnHelpers()
    {
        // Remove cosmetic home screen helpers
        HideHomeScreenHelpers();

        // Run starts at the PERMANENT slot level (capped at 4). Any temp rent from a prior
        // run is ignored, so the next run starts with the unlocked count, not the rented one.
        int unlockedSlots = 1;
        if (UpgradeManager.Instance != null)
        {
            int slotLevel = UpgradeManager.Instance.GetPermanentLevel("helper_slot_unlock");
            unlockedSlots = Mathf.Min(MAX_HELPER_SLOTS, slotLevel + 1);
        }

        if (unlockedSlots > 0)
        {
            int spawned = 0;
            for (int i = 0; i < unlockedSlots; i++)
            {
                Helper helper = SpawnUniversalHelper();
                if (helper != null)
                    spawned++;
            }

            Debug.Log($"[Helpers] Auto-spawned {spawned}/{unlockedSlots} helpers");
        }
    }

    /// <summary>
    /// Remove a helper
    /// </summary>
    public void RemoveHelper(Helper helper)
    {
        if (activeHelpers.Contains(helper))
        {
            activeHelpers.Remove(helper);
            Destroy(helper.gameObject);
        }
    }

    /// <summary>
    /// Remove all helpers (called at run end)
    /// MODIFIED: Also clear zone seeds, restore home screen helpers
    /// </summary>
    private void ClearAllHelpers()
    {
        // Clear zone seed configuration
        currentZoneSeeds.Clear();

        for (int i = activeHelpers.Count - 1; i >= 0; i--)
        {
            if (activeHelpers[i] != null)
            {
                Destroy(activeHelpers[i].gameObject);
            }
        }

        activeHelpers.Clear();
        pendingTasks.Clear();

        // Restore cosmetic helpers on home screen
        ShowHomeScreenHelpers();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Home Screen Cosmetic Helpers
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawn cosmetic helpers on the home screen that wander around.
    /// Count matches the number of unlocked helper slots.
    /// </summary>
    public void ShowHomeScreenHelpers()
    {
        HideHomeScreenHelpers();

        if (universalHelperPrefab == null) return;
        if (RunManager.Instance != null && RunManager.Instance.IsRunActive) return;

        int slots = 1;
        if (UpgradeManager.Instance != null)
            slots = Mathf.Min(MAX_HELPER_SLOTS, UpgradeManager.Instance.GetPermanentLevel("helper_slot_unlock") + 1);

        for (int i = 0; i < slots; i++)
        {
            Vector3 spawnPos = helperSpawnPoint != null ? helperSpawnPoint.position : defaultSpawnPosition;
            // Offset each helper slightly so they don't stack
            spawnPos.x += Random.Range(-1f, 1f);
            spawnPos.y += Random.Range(-0.5f, 0.5f);

            GameObject helperObj = Instantiate(universalHelperPrefab, spawnPos, Quaternion.identity, transform);
            helperObj.name = $"HomeHelper_{i + 1}";

            // Add the wander component for cosmetic movement
            HomeHelperWander wander = helperObj.AddComponent<HomeHelperWander>();

            // Disable the Helper component so it doesn't try to do tasks
            Helper helper = helperObj.GetComponent<Helper>();
            if (helper != null)
                helper.enabled = false;

            homeScreenHelpers.Add(helperObj);
        }
    }

    /// <summary>
    /// Destroy all cosmetic home screen helpers.
    /// </summary>
    public void HideHomeScreenHelpers()
    {
        foreach (GameObject go in homeScreenHelpers)
        {
            if (go != null) Destroy(go);
        }
        homeScreenHelpers.Clear();
    }

    /// <summary>
    /// Scan farm for tasks that need to be done
    /// Phase 5.2: Full priority system for universal helpers
    /// FIXED: Now only scans unlocked zones
    /// </summary>
    private void ScanForTasks()
    {
        // Scan in priority order (though all get added to queue and sorted anyway)
        ScanForHarvestTasks();   // Priority 800-900 (+urgency)
        ScanForWaterTasks();     // Priority 300-900 (+urgency)
        ScanForPlantTasks();     // Priority ~400
        ScanForTillTasks();      // Priority 350 — lowest ("expand" only when nothing needs tending)
    }

    /// <summary>
    /// Scan for untilled tiles in unlocked zones.
    /// Priority 350 — LOW. Tilling is "expansion"; saving existing crops (water/harvest) and
    /// planting tilled tiles all come first ("tend before expand"). At run start there are no crops
    /// yet, so tilling still happens (it's the only available work). This stops helpers from pinning
    /// themselves to tilling new ground while planted crops die of thirst.
    /// </summary>
    private void ScanForTillTasks()
    {
        if (FarmGrid.Instance == null) return;

        List<SoilTile> untilledTiles = FarmGrid.Instance.GetUntilledTiles();

        foreach (SoilTile tile in untilledTiles)
        {
            if (!IsZoneUnlocked(tile.ZoneID)) continue;

            // Only till zones we can actually farm: a seed must be configured AND currently
            // affordable. Otherwise helpers waste their time tilling zones that can't be planted
            // (starving watering/harvesting of the zones you CAN afford). Watering/harvesting of
            // already-planted crops is unaffected — those scans don't gate on affordability.
            if (!ZoneIsWorkable(tile.ZoneID)) continue;

            bool alreadyHasTask = pendingTasks.Exists(task =>
                task.Type == HelperTask.TaskType.Till &&
                task.TargetTile == tile
            );

            if (!alreadyHasTask)
            {
                HelperTask task = HelperTask.CreateTillTask(tile, 350);
                AddTask(task);
            }
        }
    }

    /// <summary>
    /// Scan for plants that need harvesting
    /// Priority: Rotting (900) > Normal harvest window (800)
    /// FIXED: Only scans plants in unlocked zones
    /// </summary>
    private void ScanForHarvestTasks()
    {
        // PERFORMANCE FIX: Use cached plant list instead of FindObjectsByType
        foreach (Plant plant in cachedPlants)
        {
            if (plant == null) continue; // Skip destroyed plants
            
            // FIXED: Skip plants in locked zones
            SoilTile tile = plant.GetComponentInParent<SoilTile>();
            if (tile != null && !IsZoneUnlocked(tile.ZoneID))
            {
                continue; // Skip this plant - it's in a locked zone
            }
            
            if (plant.IsHarvestable)
            {
                // Check if we already have a task for this plant
                bool alreadyHasTask = pendingTasks.Exists(task => 
                    task.Type == HelperTask.TaskType.Harvest && 
                    task.TargetPlant == plant
                );

                if (!alreadyHasTask)
                {
                    int priority = CalculateHarvestPriority(plant);
                    HelperTask task = HelperTask.CreateHarvestTask(plant, priority);
                    AddTask(task);
                }
            }
        }
    }

    /// <summary>
    /// Scan for plants that need watering
    /// Priority tiers:
    /// - Dried out (700)
    /// - <20% moisture (600)
    /// - <50% moisture (400)
    /// - <75% moisture (300)
    /// - <100% moisture (200)
    /// FIXED: Only scans plants in unlocked zones
    /// </summary>
    private void ScanForWaterTasks()
    {
        // PERFORMANCE FIX: Use cached plant list instead of FindObjectsByType
        foreach (Plant plant in cachedPlants)
        {
            if (plant == null) continue; // Skip destroyed plants
            
            // FIXED: Skip plants in locked zones
            SoilTile tile = plant.GetComponentInParent<SoilTile>();
            if (tile != null && !IsZoneUnlocked(tile.ZoneID))
            {
                continue; // Skip this plant - it's in a locked zone
            }
            
            // Check if already has water task
            bool alreadyHasTask = pendingTasks.Exists(task => 
                task.Type == HelperTask.TaskType.Water && 
                task.TargetPlant == plant
            );

            if (!alreadyHasTask)
            {
                int priority = CalculateWaterPriority(plant);
                if (priority > 0) // Only create task if plant needs water
                {
                    HelperTask task = HelperTask.CreateWaterTask(plant, priority);
                    AddTask(task);
                }
            }
        }
    }

    /// <summary>
    /// Scan for empty tilled tiles that need planting
    /// Priority: 500 (medium - between watering tiers)
    /// FIXED: Only scans tiles in unlocked zones
    /// </summary>
    private void ScanForPlantTasks()
    {
        if (FarmGrid.Instance == null) return;

        List<SoilTile> plantableTiles = FarmGrid.Instance.GetPlantableTiles();

        foreach (SoilTile tile in plantableTiles)
        {
            // FIXED: Skip tiles in locked zones
            if (!IsZoneUnlocked(tile.ZoneID))
            {
                continue; // Skip this tile - it's in a locked zone
            }
            
            // Check if already has plant task
            bool alreadyHasTask = pendingTasks.Exists(task => 
                task.Type == HelperTask.TaskType.Plant && 
                task.TargetTile == tile
            );

            if (!alreadyHasTask)
            {
                // Only plant where there's a configured seed we can afford (so helpers don't walk
                // to tiles they can't plant and churn).
                if (ZoneIsWorkable(tile.ZoneID))
                {
                    int priority = CalculatePlantPriority(tile);
                    HelperTask task = HelperTask.CreatePlantTask(tile, priority);
                    AddTask(task);
                }
            }
        }
    }

    /// <summary>
    /// Calculate harvest priority
    /// Rotting (900) > Normal (800)
    /// Plus modifiers for urgency and distance
    /// </summary>
    private int CalculateHarvestPriority(Plant plant)
    {
        int basePriority;

        if (plant.IsRotting)
        {
            basePriority = 900; // Harvest rotting crops (highest harvest priority)
            
            // Urgency modifier: Lower HP = higher priority
            float hpPercent = plant.CurrentHP / plant.CropData.maxHP;
            int urgencyBonus = Mathf.RoundToInt((1f - hpPercent) * 50f); // 0-50 bonus
            basePriority += urgencyBonus;
        }
        else if (plant.IsInHarvestWindow)
        {
            basePriority = 800; // Harvest normal window
            
            // Urgency modifier: Less time = higher priority
            float timeLeft = plant.HarvestWindowTimer;
            float windowDuration = plant.CropData.harvestWindowSeconds;
            float timePercent = timeLeft / windowDuration;
            int urgencyBonus = Mathf.RoundToInt((1f - timePercent) * 50f); // 0-50 bonus
            basePriority += urgencyBonus;
        }
        else
        {
            basePriority = 800; // Shouldn't happen, but default to normal
        }

        // Distance modifier (25% weight)
        // Closer plants get small priority boost
        // This will be applied when helper evaluates tasks

        return basePriority;
    }

    /// <summary>
    /// Calculate water priority
    /// Dried out (900) > <20% (800) > <50% (700) > <75% (600) > top-off <100% (300)
    /// Plus urgency modifier (driest first, up to +30). Meaningful tiers sit well above Plant (400)
    /// so a thirsty crop is watered before a new seed is planted even after distance penalties;
    /// top-off stays below Plant so a healthy, watered farm still gets planted out. Water tasks also
    /// skip the crowding penalty in task scoring so a needy crop is never passed over.
    /// </summary>
    private int CalculateWaterPriority(Plant plant)
    {
        float moisture = plant.CurrentMoisture;
        int basePriority;

        if (plant.IsDriedOut)
        {
            basePriority = 900; // Water dried out crops (taking damage!)
        }
        else if (moisture < 20f)
        {
            basePriority = 800; // Water <20% — critical
        }
        else if (moisture < 50f)
        {
            basePriority = 700; // Water <50% — beats planting
        }
        else if (moisture < 75f)
        {
            basePriority = 600; // Water <75% — beats planting even when penalized
        }
        else if (moisture < 100f)
        {
            basePriority = 300; // Water <100% (top-off — lower than planting)
        }
        else
        {
            return 0; // Already at 100%, no need to water
        }

        // Urgency modifier: Driest plants get priority boost (0-30)
        int urgencyBonus = Mathf.RoundToInt((100f - moisture) / 100f * 30f);
        basePriority += urgencyBonus;

        return basePriority;
    }

    /// <summary>
    /// Calculate plant priority
    /// Base: 400 — lower than all meaningful water tiers, higher than top-off (300)
    /// Planting should only happen when nothing nearby needs watering/harvesting
    /// </summary>
    private int CalculatePlantPriority(SoilTile tile)
    {
        int basePriority = 400; // Plant seeds (low-medium priority)

        // Strategy modifier
        switch (plantingStrategy)
        {
            case PlantingStrategy.ZoneBased:
                // Prioritize lower zones (fill zone 1, then 2, etc.)
                basePriority += (5 - tile.ZoneID) * 10; // Zone 1 = +40, Zone 4 = +10
                break;

            case PlantingStrategy.ClosestTile:
                // Distance will be primary factor (applied when helper evaluates)
                break;

            case PlantingStrategy.Random:
                // Add random offset to spread planting
                basePriority += Random.Range(-20, 20);
                break;
        }

        return basePriority;
    }

    /// <summary>
    /// A zone is "workable" (worth tilling/planting) when it has a configured seed the player can
    /// currently afford (or already has seeds for). Watering/harvesting existing crops is NOT gated
    /// on this — only new till/plant work is, so helpers stop expanding into zones you can't afford
    /// but keep tending what's already growing.
    /// </summary>
    private bool ZoneIsWorkable(int zoneID)
    {
        CropData seed = GetSeedForZone(zoneID);
        if (seed == null) return false;
        if (SeedInventory.Instance == null) return true; // no economy in play → allow
        return SeedInventory.Instance.CanPlant(seed);
    }

    /// <summary>Distinct crops configured across all zones this run (for the seed HUD).</summary>
    public List<CropData> GetConfiguredCrops()
    {
        var result = new List<CropData>();
        foreach (var kv in currentZoneSeeds)
            if (kv.Value != null && !result.Contains(kv.Value)) result.Add(kv.Value);
        return result;
    }

    /// <summary>
    /// Get seed type configured for a zone
    /// MODIFIED: Check runtime dictionary first, fall back to inspector fields
    /// </summary>
    public CropData GetSeedForZone(int zoneID)
    {
        // Check runtime configuration first (from popup)
        if (currentZoneSeeds.ContainsKey(zoneID))
        {
            return currentZoneSeeds[zoneID];
        }
        
        // Fall back to inspector configuration (for backwards compatibility/testing)
        switch (zoneID)
        {
            case 1: return zone1Seed;
            case 2: return zone2Seed;
            case 3: return zone3Seed;
            case 4: return zone4Seed;
            default: return null;
        }
    }

    /// <summary>
    /// Clean up completed or invalid tasks
    /// </summary>
    private void CleanupTasks()
    {
        pendingTasks.RemoveAll(task => task == null || task.IsCompleted || !task.IsValid());
    }

    /// <summary>
    /// Try to assign pending tasks to idle helpers
    /// Phase 5.2: Includes distance weighting (25%)
    /// </summary>
    private void AssignTasksToHelpers()
    {
        if (pendingTasks.Count == 0) return;

        // PERFORMANCE FIX: Limit how many helpers we check per frame
        int checkedHelpers = 0;
        const int MAX_HELPERS_PER_FRAME = 3;

        foreach (Helper helper in activeHelpers)
        {
            // Skip null helpers (shouldn't happen but safe)
            if (helper == null) continue;
            
            if (helper.IsIdle && !helper.HasTask && pendingTasks.Count > 0)
            {
                // Find best task for this helper (priority + distance)
                HelperTask bestTask = FindBestTaskForHelper(helper);
                
                if (bestTask != null && !bestTask.IsClaimed && bestTask.IsValid())
                {
                    helper.AssignTask(bestTask);
                }
                
                checkedHelpers++;
                if (checkedHelpers >= MAX_HELPERS_PER_FRAME) break; // Stop after 3 helpers
            }
        }
    }

    /// <summary>
    /// Find the best task for a helper considering priority, distance, and crowding.
    /// Score = priority - distancePenalty - crowdingPenalty
    /// Crowding: if another active helper is closer to the task, penalize it so
    /// helpers spread out instead of all traveling to the same zone.
    /// </summary>
    private HelperTask FindBestTaskForHelper(Helper helper)
    {
        HelperTask bestTask = null;
        float bestScore = float.MinValue;

        Vector3 helperPos = helper.transform.position;

        int totalTasks = pendingTasks.Count;
        int claimedTasks = 0;
        int invalidTasks = 0;
        int validTasks = 0;

        foreach (HelperTask task in pendingTasks)
        {
            if (task.IsClaimed)
            {
                claimedTasks++;
                continue;
            }

            if (!task.IsValid())
            {
                invalidTasks++;
                continue;
            }

            validTasks++;

            Vector3 taskPos = task.TargetTile.transform.position;
            float distance = Vector3.Distance(helperPos, taskPos);

            // Distance penalty: quadratic falloff with a TIGHT "zone of influence" so helpers
            // strongly prefer local work. At /12 normalization + 450 base × weight(0.6): ~270 max
            // penalty at 12+ units, ~67 at 6 units. This stops a helper crossing the map for a
            // high-priority HARVEST while a crop crisps right next to it — a far task must be far
            // more urgent than local work to pull a helper over.
            float normalizedDistance = Mathf.Clamp01(distance / 12f);
            float distancePenalty = normalizedDistance * normalizedDistance * 450f * distanceWeight;

            // Crowding penalty: if ANY busy helper is already closer to this task, back off HARD —
            // don't pull a helper across the map for a crop a nearer helper can (and will) handle
            // back-to-back. The closer helper wins it during assignment.
            float crowdingPenalty = 0f;
            foreach (Helper other in activeHelpers)
            {
                if (other == null || other == helper || other.IsIdle) continue;

                float otherDist = Vector3.Distance(other.transform.position, taskPos);
                if (otherDist < distance)
                {
                    // Someone else is closer — heavy penalty so this helper doesn't cross the map
                    // to compete for a task a nearer helper will handle.
                    crowdingPenalty = 200f;
                    break;
                }
            }

            float score = task.Priority - distancePenalty - crowdingPenalty;

            if (score > bestScore)
            {
                bestScore = score;
                bestTask = task;
            }
        }

        if (bestTask == null && totalTasks > 0)
        {
            Debug.LogWarning($"⚠️ {helper.name} found NO valid tasks! Total: {totalTasks}, Claimed: {claimedTasks}, Invalid: {invalidTasks}, Valid: {validTasks}");
        }

        return bestTask;
    }

    /// <summary>
    /// Add a task to the queue
    /// </summary>
    public void AddTask(HelperTask task)
    {
        if (task != null && task.IsValid() && !pendingTasks.Contains(task))
        {
            pendingTasks.Add(task);
        }
    }

    /// <summary>
    /// Get count of active helpers
    /// </summary>
    public int GetHelperCount()
    {
        if (activeHelpers == null) return 0;
        // Clean nulls first
        activeHelpers.RemoveAll(h => h == null);
        return activeHelpers.Count;
    }

    /// <summary>
    /// Get count of idle helpers
    /// </summary>
    public int GetIdleHelperCount()
    {
        if (activeHelpers == null) return 0;
        
        int count = 0;
        foreach (Helper helper in activeHelpers)
        {
            if (helper != null && helper.IsIdle) count++;
        }
        return count;
    }

    /// <summary>
    /// Get count of pending tasks
    /// </summary>
    public int GetPendingTaskCount()
    {
        return pendingTasks.Count;
    }

#if UNITY_EDITOR
    [ContextMenu("Spawn Universal Helper")]
    private void TestSpawnUniversalHelper()
    {
        SpawnUniversalHelper();
    }

    [ContextMenu("Show Manager Info")]
    private void ShowManagerInfo()
    {
        Debug.Log("=== HELPER MANAGER ===");
        Debug.Log($"Active Helpers: {activeHelpers.Count}");
        Debug.Log($"Idle Helpers: {GetIdleHelperCount()}");
        Debug.Log($"Pending Tasks: {pendingTasks.Count}");
        
        // Show task breakdown
        int harvestTasks = pendingTasks.FindAll(t => t.Type == HelperTask.TaskType.Harvest).Count;
        int waterTasks = pendingTasks.FindAll(t => t.Type == HelperTask.TaskType.Water).Count;
        int plantTasks = pendingTasks.FindAll(t => t.Type == HelperTask.TaskType.Plant).Count;
        int tillTasks = pendingTasks.FindAll(t => t.Type == HelperTask.TaskType.Till).Count;
        
        Debug.Log($"  Harvest: {harvestTasks}, Water: {waterTasks}, Plant: {plantTasks}, Till: {tillTasks}");
        
        foreach (Helper helper in activeHelpers)
        {
            if (helper != null)
            {
                Debug.Log($"  - {helper.name}: {helper.State}");
            }
        }
    }

    [ContextMenu("Clear All Helpers")]
    private void TestClearHelpers()
    {
        for (int i = activeHelpers.Count - 1; i >= 0; i--)
        {
            RemoveHelper(activeHelpers[i]);
        }
        Debug.Log("Cleared all helpers");
    }

    [ContextMenu("Show Performance Stats")]
    private void ShowPerformanceStats()
    {
        Debug.Log("=== HELPER MANAGER PERFORMANCE ===");
        Debug.Log($"Active Helpers: {activeHelpers.Count}");
        Debug.Log($"Pending Tasks: {pendingTasks.Count}");
        Debug.Log($"Cached Plants: {cachedPlants.Count}");
        
        // Count tasks by type
        int harvest = 0, water = 0, plant = 0, till = 0;
        foreach (HelperTask task in pendingTasks)
        {
            if (task.Type == HelperTask.TaskType.Harvest) harvest++;
            else if (task.Type == HelperTask.TaskType.Water) water++;
            else if (task.Type == HelperTask.TaskType.Plant) plant++;
            else if (task.Type == HelperTask.TaskType.Till) till++;
        }
        
        Debug.Log($"Task breakdown: H:{harvest} W:{water} P:{plant} T:{till}");
        
        if (pendingTasks.Count > 50)
        {
            Debug.LogWarning("⚠️ HIGH TASK COUNT! This may cause lag. Consider adjusting task creation thresholds.");
        }
        
        if (cachedPlants.Count > 200)
        {
            Debug.LogWarning("⚠️ HIGH PLANT COUNT! Consider optimizing update loops.");
        }
    }
#endif
}