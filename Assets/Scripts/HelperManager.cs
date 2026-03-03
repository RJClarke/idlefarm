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
    [Tooltip("How much distance affects priority (0-1, where 1 = distance matters a lot)")]
    [Range(0f, 1f)]
    [SerializeField] private float distanceWeight = 0.25f;

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
            Debug.Log($"🔄 Rebuilt plant cache: {cachedPlants.Count} plants");
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
            
            Debug.Log($"🌱 Zone seeds configured for run:");
            foreach (var kvp in currentZoneSeeds)
            {
                Debug.Log($"  Zone {kvp.Key}: {kvp.Value.cropName}");
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

        // Phase 5.5: Check max helpers limit from upgrades
        if (HelperUpgradeManager.Instance != null)
        {
            int maxHelpers = HelperUpgradeManager.Instance.MaxHelpers;
            int currentCount = GetHelperCount();
            
            if (currentCount >= maxHelpers)
            {
                Debug.LogWarning($"❌ Cannot spawn helper - at max limit ({currentCount}/{maxHelpers})");
                return null;
            }
        }

        Vector3 spawnPos = helperSpawnPoint != null ? helperSpawnPoint.position : defaultSpawnPosition;
        GameObject helperObj = Instantiate(universalHelperPrefab, spawnPos, Quaternion.identity, transform);
        
        Helper helper = helperObj.GetComponent<Helper>();
        if (helper != null)
        {
            activeHelpers.Add(helper);
            
            int current = GetHelperCount();
            int max = HelperUpgradeManager.Instance?.MaxHelpers ?? 1;
            Debug.Log($"✓ Spawned universal helper at {spawnPos} ({current}/{max})");
        }
        else
        {
            Debug.LogError("Universal Helper prefab doesn't have a Helper component!");
            Destroy(helperObj);
        }

        return helper;
    }

    /// <summary>
    /// Auto-spawn helpers at run start (if upgrade purchased)
    /// Phase 5.5: Triggered by OnRunStarted event
    /// </summary>
    private void AutoSpawnHelpers()
    {
        if (HelperUpgradeManager.Instance == null) return;

        int autoSpawnCount = HelperUpgradeManager.Instance.AutoSpawnHelpers;
        
        if (autoSpawnCount > 0)
        {
            Debug.Log($"🤖 Auto-spawning {autoSpawnCount} helpers from upgrades...");
            
            int spawned = 0;
            for (int i = 0; i < autoSpawnCount; i++)
            {
                Helper helper = SpawnUniversalHelper();
                if (helper != null)
                {
                    spawned++;
                }
            }
            
            if (spawned < autoSpawnCount)
            {
                Debug.LogWarning($"Only spawned {spawned}/{autoSpawnCount} helpers (hit max limit)");
            }
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
    /// MODIFIED: Also clear zone seeds
    /// </summary>
    private void ClearAllHelpers()
    {
        // Clear zone seed configuration
        currentZoneSeeds.Clear();
        
        Debug.Log($"🧹 Clearing {activeHelpers.Count} helpers at run end");
        
        for (int i = activeHelpers.Count - 1; i >= 0; i--)
        {
            if (activeHelpers[i] != null)
            {
                Destroy(activeHelpers[i].gameObject);
            }
        }
        
        activeHelpers.Clear();
        pendingTasks.Clear();
    }

    /// <summary>
    /// Scan farm for tasks that need to be done
    /// Phase 5.2: Full priority system for universal helpers
    /// FIXED: Now only scans unlocked zones
    /// </summary>
    private void ScanForTasks()
    {
        // Scan in priority order (though all get added to queue and sorted anyway)
        ScanForTillTasks();      // Priority 1000
        ScanForHarvestTasks();   // Priority 800-900
        ScanForWaterTasks();     // Priority 200-700
        ScanForPlantTasks();     // Priority 500
    }

    /// <summary>
    /// Scan for tiles that player has paid to till
    /// </summary>
    private void ScanForTillTasks()
    {
        // TODO: Implement when till payment system is added
        // For now, tilling is handled manually by player
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
                // Check if zone has seed configured
                if (GetSeedForZone(tile.ZoneID) != null)
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
    /// Dried out (700) > <20% (600) > <50% (400) > <75% (300) > <100% (200)
    /// Plus modifiers for urgency (driest first) and distance
    /// </summary>
    private int CalculateWaterPriority(Plant plant)
    {
        float moisture = plant.CurrentMoisture;
        int basePriority;

        if (plant.IsDriedOut)
        {
            basePriority = 700; // Water dried out crops (taking damage!)
        }
        else if (moisture < 20f)
        {
            basePriority = 600; // Water <20%
        }
        else if (moisture < 50f)
        {
            basePriority = 400; // Water <50%
        }
        else if (moisture < 75f)
        {
            basePriority = 300; // Water <75%
        }
        else if (moisture < 100f)
        {
            basePriority = 200; // Water <100% (top-off)
        }
        else
        {
            return 0; // Already at 100%, no need to water
        }

        // Urgency modifier: Driest plants get priority boost
        int urgencyBonus = Mathf.RoundToInt((100f - moisture) / 10f); // 0-10 bonus
        basePriority += urgencyBonus;

        return basePriority;
    }

    /// <summary>
    /// Calculate plant priority
    /// Base: 500 (between water tiers)
    /// Modifiers based on planting strategy
    /// </summary>
    private int CalculatePlantPriority(SoilTile tile)
    {
        int basePriority = 500; // Plant seeds (medium priority)

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
    /// Find the best task for a helper considering priority and distance
    /// Priority is the main factor, distance is 25% weight
    /// Phase 5.5: Added debug logging
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
            // Track why tasks are being skipped
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

            // Calculate score = priority - (distance * weight)
            float distance = Vector3.Distance(helperPos, task.TargetTile.transform.position);
            
            // Normalize distance (assume max distance of 20 units)
            float normalizedDistance = Mathf.Clamp01(distance / 20f);
            
            // Distance penalty scales with distance weight setting
            float distancePenalty = normalizedDistance * 100f * distanceWeight;
            
            float score = task.Priority - distancePenalty;

            if (score > bestScore)
            {
                bestScore = score;
                bestTask = task;
            }
        }

        // Debug logging when helper can't find work or assigns task
        if (bestTask == null && totalTasks > 0)
        {
            Debug.LogWarning($"⚠️ {helper.name} found NO valid tasks! Total: {totalTasks}, Claimed: {claimedTasks}, Invalid: {invalidTasks}, Valid: {validTasks}");
        }
        else if (bestTask != null && showDebugInfo)
        {
            Debug.Log($"✓ {helper.name} assigned: {bestTask.Type} (priority {bestTask.Priority}, score {bestScore:F0})");
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