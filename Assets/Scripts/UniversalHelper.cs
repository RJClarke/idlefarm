using UnityEngine;

/// <summary>
/// Universal helper that can perform ALL tasks (harvest, water, plant, till)
/// Phase 5.2: Production helper with intelligent priority system
/// </summary>
public class UniversalHelper : Helper
{
    [Header("Universal Helper Settings")]
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color harvestColor = Color.yellow;
    [SerializeField] private Color waterColor = Color.blue;
    [SerializeField] private Color plantColor = Color.green;
    [SerializeField] private Color tillColor = Color.gray;

    // Per-task upgrade IDs matching UpgradeData assets
    private const string UPGRADE_PLANTING_SPEED = "helper_planting_speed";
    private const string UPGRADE_WATERING_SPEED = "helper_watering_speed";
    private const string UPGRADE_HARVESTING_SPEED = "helper_harvesting_speed";
    private const string UPGRADE_WATERING_EFFICIENCY = "helper_watering_efficiency";

    /// <summary>
    /// Get task duration with per-task-type specialization bonus applied.
    /// Stacks with the base UpgradedTaskDuration from Helper.
    /// </summary>
    private float GetSpecializedTaskDuration(HelperTask.TaskType taskType)
    {
        float duration = UpgradedTaskDuration;

        if (UpgradeManager.Instance == null) return duration;

        string upgradeID = null;
        switch (taskType)
        {
            case HelperTask.TaskType.Plant: upgradeID = UPGRADE_PLANTING_SPEED; break;
            case HelperTask.TaskType.Water: upgradeID = UPGRADE_WATERING_SPEED; break;
            case HelperTask.TaskType.Harvest: upgradeID = UPGRADE_HARVESTING_SPEED; break;
        }

        if (upgradeID != null)
        {
            int level = UpgradeManager.Instance.GetCurrentLevel(upgradeID);
            duration *= Mathf.Pow(0.8f, level); // 20% faster per level
        }

        // Research speed bonuses stack multiplicatively on top of upgrades.
        // duration is divided by (1 + bonus) so more bonus = less duration = faster.
        float researchBonus = GetResearchSpeedBonus(taskType);
        if (researchBonus > 0f)
            duration /= (1f + researchBonus);

        return duration;
    }

    private static float GetResearchSpeedBonus(HelperTask.TaskType taskType)
    {
        if (ResearchManager.Instance == null) return 0f;
        switch (taskType)
        {
            case HelperTask.TaskType.Till:    return ResearchManager.Instance.GetBonus(Research.StatKey.HelperTillSpeed);
            case HelperTask.TaskType.Water:   return ResearchManager.Instance.GetBonus(Research.StatKey.HelperWaterSpeed);
            case HelperTask.TaskType.Plant:   return ResearchManager.Instance.GetBonus(Research.StatKey.HelperPlantSpeed);
            case HelperTask.TaskType.Harvest: return ResearchManager.Instance.GetBonus(Research.StatKey.HelperHarvestSpeed);
            default: return 0f;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        helperName = "Universal Helper";
        helperColor = idleColor;
    }

    protected override float GetTaskDuration()
    {
        if (currentTask != null)
            return GetSpecializedTaskDuration(currentTask.Type);
        return base.GetTaskDuration();
    }

    protected override void UpdateIdle()
    {
        base.UpdateIdle();
        // Helper waits for HelperManager to assign tasks
        // This prevents race conditions
    }

    protected override void ExecuteTask()
    {
        if (currentTask == null || !currentTask.IsValid())
        {
            Debug.LogWarning($"{helperName} task is null or invalid, cancelling");
            CancelTask();
            return;
        }

        // Execute based on task type
        // Till task may chain to plant, so it returns whether to complete
        bool shouldComplete = true;
        
        switch (currentTask.Type)
        {
            case HelperTask.TaskType.Till:
                shouldComplete = ExecuteTillTask(); // May return false if chaining
                break;

            case HelperTask.TaskType.Harvest:
                ExecuteHarvestTask();
                break;

            case HelperTask.TaskType.Water:
                ExecuteWaterTask();
                break;

            case HelperTask.TaskType.Plant:
                ExecutePlantTask();
                break;
        }

        // Only complete task if not chaining to another one
        if (shouldComplete)
        {
            CompleteTask();
        }
    }

    /// <summary>
    /// Execute till task — till the tile, then chain to plant.
    /// Returns false if chaining (don't complete), true if normal completion.
    /// </summary>
    private bool ExecuteTillTask()
    {
        if (currentTask.TargetTile == null)
            return true;

        // Actually till the tile (free for helpers)
        SoilTile tile = currentTask.TargetTile;
        if (tile.State == TileState.Untilled)
        {
            tile.TillByHelper();
        }

        // Mark till task as completed
        currentTask.IsCompleted = true;

        // Chain to plant task on same tile
        if (HelperManager.Instance != null)
        {
            CropData seedType = HelperManager.Instance.GetSeedForZone(tile.ZoneID);

            bool canAffordSeed = SeedInventory.Instance == null || SeedInventory.Instance.CanPlant(seedType);
            if (seedType != null && tile.CanPlant && canAffordSeed)
            {
                HelperTask plantTask = HelperTask.CreatePlantTask(tile, 500);
                currentTask = plantTask;
                currentTask.IsClaimed = true;
                taskTimer = GetSpecializedTaskDuration(HelperTask.TaskType.Plant);

                // Update visual for the new task type
                if (spriteRenderer != null)
                    spriteRenderer.color = plantColor;

                return false; // Continue with plant task
            }
        }

        return true;
    }

    private void ExecuteHarvestTask()
    {
        if (currentTask.TargetPlant != null)
        {
            currentTask.TargetPlant.Harvest();
        }
    }

    private void ExecuteWaterTask()
    {
        if (currentTask.TargetPlant != null)
        {
            currentTask.TargetPlant.Water();
        }
    }

    private void ExecutePlantTask()
    {
        if (currentTask.TargetTile == null)
        {
            Debug.LogError($"{helperName} plant task has null TargetTile!");
            return;
        }
        
        if (HelperManager.Instance == null)
        {
            Debug.LogError($"{helperName} HelperManager.Instance is null!");
            return;
        }
        
        // Get seed type for this zone
        int zoneID = currentTask.TargetTile.ZoneID;
        
        CropData seedType = HelperManager.Instance.GetSeedForZone(zoneID);
        
        if (seedType == null)
        {
            Debug.LogError($"{helperName} NO SEED CONFIGURED FOR ZONE {zoneID}! Check HelperManager settings!");
            return;
        }
        
        if (seedType.plantPrefab == null)
        {
            Debug.LogError($"{helperName} seed {seedType.cropName} has NO PLANT PREFAB! Check CropData settings!");
            return;
        }
        
        // Seed economy: must consume a seed (buying a bag if needed) before planting.
        if (SeedInventory.Instance != null &&
            !SeedInventory.Instance.TryConsumeSeed(seedType, currentTask.TargetTile.transform.position))
        {
            // Out of seeds and can't afford a bag — leave the tile empty. BankruptcyWatcher handles end.
            return;
        }

        bool success = currentTask.TargetTile.PlantCrop(seedType.plantPrefab, seedType);
        
        if (!success)
        {
            Debug.LogWarning($"{helperName} ✗ PlantCrop failed for {seedType.cropName} in zone {zoneID}");
        }
    }

    protected override void OnStateChanged(HelperState newState)
    {
        base.OnStateChanged(newState);
        
        // Visual feedback based on state and task type
        if (spriteRenderer != null)
        {
            switch (newState)
            {
                case HelperState.Idle:
                    spriteRenderer.color = idleColor;
                    break;

                case HelperState.MovingToTask:
                case HelperState.PerformingTask:
                    // Color based on task type
                    if (currentTask != null)
                    {
                        switch (currentTask.Type)
                        {
                            case HelperTask.TaskType.Harvest:
                                spriteRenderer.color = harvestColor;
                                break;
                            case HelperTask.TaskType.Water:
                                spriteRenderer.color = waterColor;
                                break;
                            case HelperTask.TaskType.Plant:
                                spriteRenderer.color = plantColor;
                                break;
                            case HelperTask.TaskType.Till:
                                spriteRenderer.color = idleColor;
                                break;
                        }
                    }
                    break;
            }
        }
    }
}