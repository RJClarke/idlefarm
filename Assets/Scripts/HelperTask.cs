using UnityEngine;

/// <summary>
/// Represents a task that a helper can perform
/// Phase 5.1: Task system for helper automation
/// Phase 5.2: Improved validation to check if task conditions still apply
/// </summary>
public class HelperTask
{
    public enum TaskType
    {
        Harvest,
        Water,
        Plant,
        Till
    }

    // Task details
    public TaskType Type { get; private set; }
    public SoilTile TargetTile { get; private set; }
    public Plant TargetPlant { get; private set; }
    public int Priority { get; set; } // Higher = more urgent
    public bool IsCompleted { get; set; }
    public bool IsClaimed { get; set; } // Prevent multiple helpers from taking same task

    /// <summary>
    /// Create a harvest task
    /// </summary>
    public static HelperTask CreateHarvestTask(Plant plant, int priority = 0)
    {
        return new HelperTask
        {
            Type = TaskType.Harvest,
            TargetPlant = plant,
            TargetTile = plant.ParentTile,
            Priority = priority,
            IsCompleted = false,
            IsClaimed = false
        };
    }

    /// <summary>
    /// Create a water task
    /// </summary>
    public static HelperTask CreateWaterTask(Plant plant, int priority = 0)
    {
        return new HelperTask
        {
            Type = TaskType.Water,
            TargetPlant = plant,
            TargetTile = plant.ParentTile,
            Priority = priority,
            IsCompleted = false,
            IsClaimed = false
        };
    }

    /// <summary>
    /// Create a plant task
    /// </summary>
    public static HelperTask CreatePlantTask(SoilTile tile, int priority = 0)
    {
        return new HelperTask
        {
            Type = TaskType.Plant,
            TargetTile = tile,
            TargetPlant = null,
            Priority = priority,
            IsCompleted = false,
            IsClaimed = false
        };
    }

    /// <summary>
    /// Create a till task
    /// </summary>
    public static HelperTask CreateTillTask(SoilTile tile, int priority = 0)
    {
        return new HelperTask
        {
            Type = TaskType.Till,
            TargetTile = tile,
            TargetPlant = null,
            Priority = priority,
            IsCompleted = false,
            IsClaimed = false
        };
    }

    /// <summary>
    /// Check if task is still valid AND still needed
    /// Phase 5.2 FIX: Now checks if task conditions still apply!
    /// </summary>
    public bool IsValid()
    {
        // Task must have a target tile
        if (TargetTile == null)
        {
            return false;
        }

        switch (Type)
        {
            case TaskType.Harvest:
                // Need valid plant that is still harvestable
                if (TargetPlant == null || TargetPlant.gameObject == null)
                    return false;
                
                // Plant must still be harvestable (not already harvested)
                return TargetPlant.IsHarvestable;

            case TaskType.Water:
                // Need valid plant that still needs water
                if (TargetPlant == null || TargetPlant.gameObject == null)
                    return false;
                
                // Plant must still need water (not already at 100%)
                // We'll be generous and say any plant <95% can still use water
                return TargetPlant.CurrentMoisture < 95f;

            case TaskType.Plant:
                // Tile must be empty and tilled
                return TargetTile.CanPlant;

            case TaskType.Till:
                // Tile must be untilled
                return TargetTile.State == TileState.Untilled;

            default:
                return false;
        }
    }

    /// <summary>
    /// Get description for debugging
    /// </summary>
    public override string ToString()
    {
        string target = TargetPlant != null ? TargetPlant.CropData.cropName : "Tile";
        string status = IsClaimed ? " [CLAIMED]" : "";
        status += IsCompleted ? " [COMPLETED]" : "";
        return $"{Type} task on {target} (Priority: {Priority}){status}";
    }
}