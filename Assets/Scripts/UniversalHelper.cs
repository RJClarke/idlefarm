using UnityEngine;

/// <summary>
/// Universal helper that can perform ALL tasks (harvest, water, plant, till)
/// Phase 5.2: Production helper with intelligent priority system
/// </summary>
public class UniversalHelper : Helper
{
    [Header("Universal Helper Settings")]
    [SerializeField] private Color idleColor = Color.cyan;
    [SerializeField] private Color harvestColor = Color.yellow;
    [SerializeField] private Color waterColor = Color.blue;
    [SerializeField] private Color plantColor = Color.green;
    [SerializeField] private Color tillColor = Color.gray;

    protected override void Awake()
    {
        base.Awake();
        helperName = "Universal Helper";
        helperColor = idleColor;
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
    /// Execute till task and chain to plant
    /// Returns false if chaining (don't complete), true if normal completion
    /// </summary>
    private bool ExecuteTillTask()
    {
        if (currentTask.TargetTile == null)
        {
            return true; // Complete normally
        }
        
        // Tile should already be tilled (player paid for it)
        // Mark till task as completed
        if (currentTask != null)
        {
            currentTask.IsCompleted = true;
        }

        // Immediately chain to plant task on same tile
        if (HelperManager.Instance != null)
        {
            CropData seedType = HelperManager.Instance.GetSeedForZone(currentTask.TargetTile.ZoneID);
            
            if (seedType != null)
            {
                // Create plant task and switch to it immediately
                HelperTask plantTask = HelperTask.CreatePlantTask(currentTask.TargetTile, 500);
                currentTask = plantTask; // Switch to plant task
                currentTask.IsClaimed = true;
                taskTimer = taskDuration; // Reset timer for planting
                
                return false; // DON'T complete - continue with plant task
            }
            else
            {
                Debug.LogWarning($"{helperName} no seed configured for zone {currentTask.TargetTile.ZoneID} - can't chain to plant");
            }
        }
        
        return true; // Complete normally if no chaining
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
                                spriteRenderer.color = tillColor;
                                break;
                        }
                    }
                    break;
            }
        }
    }
}