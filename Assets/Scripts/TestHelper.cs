using UnityEngine;

/// <summary>
/// Test helper for Phase 5.1
/// Can perform basic tasks to test the helper system
/// Will be replaced by specific helper types in Phase 5.2+
/// </summary>
public class TestHelper : Helper
{
    protected override void Awake()
    {
        base.Awake();
        helperName = "Test Helper";
        helperColor = Color.cyan;
    }

    protected override void UpdateIdle()
    {
        base.UpdateIdle();

        // Helpers no longer scan for tasks themselves
        // HelperManager will assign tasks to idle helpers
        // This prevents race conditions where multiple helpers
        // create separate task objects for the same plant
    }

    protected override void ExecuteTask()
    {
        if (currentTask == null || !currentTask.IsValid())
        {
            CancelTask();
            return;
        }

        // Execute based on task type
        switch (currentTask.Type)
        {
            case HelperTask.TaskType.Harvest:
                if (currentTask.TargetPlant != null)
                {
                    currentTask.TargetPlant.Harvest();
                }
                break;

            case HelperTask.TaskType.Water:
                if (currentTask.TargetPlant != null)
                {
                    currentTask.TargetPlant.Water();
                }
                break;

            case HelperTask.TaskType.Plant:
                // TODO: Implement in Phase 5.4
                break;

            case HelperTask.TaskType.Till:
                // TODO: Implement tilling
                break;
        }

        CompleteTask();
    }

    protected override void OnStateChanged(HelperState newState)
    {
        base.OnStateChanged(newState);

        // Visual feedback for state changes
        if (spriteRenderer != null)
        {
            switch (newState)
            {
                case HelperState.Idle:
                    spriteRenderer.color = Color.cyan; // Cyan when idle
                    break;
                case HelperState.MovingToTask:
                    spriteRenderer.color = Color.yellow; // Yellow when moving
                    break;
                case HelperState.PerformingTask:
                    spriteRenderer.color = Color.green; // Green when working
                    break;
            }
        }
    }
}