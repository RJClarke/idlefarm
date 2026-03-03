using UnityEngine;

/// <summary>
/// Base class for all helper types
/// Phase 5.1: Foundation for helper automation
/// Handles states, movement, and task execution
/// UPDATED: Smooth movement implementation with upgradeable speed
/// </summary>
public abstract class Helper : MonoBehaviour
{
    public enum HelperState
    {
        Idle,           // Looking for tasks
        MovingToTask,   // Traveling to target
        PerformingTask  // Executing task
    }

    [Header("Helper Info")]
    [SerializeField] protected string helperName = "Helper";
    [SerializeField] protected HelperState currentState = HelperState.Idle;

    [Header("Movement")]
    [Tooltip("Base movement speed in units per second (slow early game, upgradeable)")]
    [SerializeField] protected float moveSpeed = 0.8f; // Slower for early game feel
    [Tooltip("Use instant teleport instead of smooth movement")]
    [SerializeField] protected bool useInstantMovement = false; // Changed to false for smooth movement
    [Tooltip("Distance threshold to consider 'arrived' at target")]
    [SerializeField] protected float arrivalThreshold = 0.05f;

    [Header("Task Execution")]
    [SerializeField] protected float taskDuration = 0.5f; // How long tasks take
    protected float taskTimer = 0f;

    [Header("Current Task (Read-Only)")]
    [SerializeField] protected HelperTask currentTask;

    [Header("Visual")]
    [SerializeField] protected SpriteRenderer spriteRenderer;
    [SerializeField] protected Color helperColor = Color.cyan;
    private Animator animator;

    [Header("Debug")]
    [SerializeField] protected bool showMovementDebug = false;

    // Properties
    public HelperState State => currentState;
    public bool IsIdle => currentState == HelperState.Idle;
    public bool HasTask => currentTask != null;

    /// <summary>
    /// Get movement speed with upgrades applied
    /// </summary>
    protected float UpgradedMoveSpeed
    {
        get
        {
            if (HelperUpgradeManager.Instance != null)
            {
                return moveSpeed * HelperUpgradeManager.Instance.MovementSpeedMultiplier;
            }
            return moveSpeed;
        }
    }

    /// <summary>
    /// Get task duration with upgrades applied (lower = faster)
    /// </summary>
    protected float UpgradedTaskDuration
    {
        get
        {
            if (HelperUpgradeManager.Instance != null)
            {
                return taskDuration * HelperUpgradeManager.Instance.TaskSpeedMultiplier;
            }
            return taskDuration;
        }
    }

    protected virtual void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // Set visual appearance
        if (spriteRenderer != null)
        {
            spriteRenderer.color = helperColor;
        }

        animator = GetComponent<Animator>();
        if (animator != null)
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    protected virtual void Start()
    {
        // Only set to Idle if we don't already have a task
        // (task might have been assigned in Awake/same frame)
        if (!HasTask)
        {
            SetState(HelperState.Idle);
        }
    }

    protected virtual void Update()
    {
        switch (currentState)
        {
            case HelperState.Idle:
                UpdateIdle();
                break;

            case HelperState.MovingToTask:
                UpdateMoving();
                break;

            case HelperState.PerformingTask:
                UpdatePerforming();
                break;
        }
    }

    /// <summary>
    /// Idle state - scan for tasks
    /// Override in child classes to implement specific task scanning
    /// </summary>
    protected virtual void UpdateIdle()
    {
        // Child classes will implement task finding logic
        // Base class just waits
    }

    /// <summary>
    /// Moving state - travel to task location
    /// NOW WITH SMOOTH MOVEMENT!
    /// </summary>
    protected virtual void UpdateMoving()
    {
        if (currentTask == null || !currentTask.IsValid())
        {
            // Task became invalid while moving
            CancelTask();
            return;
        }

        Vector3 targetPos = currentTask.TargetTile.transform.position;

        if (useInstantMovement)
        {
            // Instant teleport to target
            transform.position = targetPos;
            
            // Immediately start performing
            SetState(HelperState.PerformingTask);
            taskTimer = UpgradedTaskDuration;
        }
        else
        {
            // SMOOTH MOVEMENT - Move toward target
            Vector3 currentPos = transform.position;
            float distance = Vector3.Distance(currentPos, targetPos);

            // Check if arrived
            if (distance <= arrivalThreshold)
            {
                // Snap to exact position
                transform.position = targetPos;
                
                // Start performing task
                SetState(HelperState.PerformingTask);
                taskTimer = UpgradedTaskDuration;



            }
            else
            {
                // Move toward target
                Vector3 direction = (targetPos - currentPos).normalized;
                float moveDistance = UpgradedMoveSpeed * Time.deltaTime;
                
                // Don't overshoot
                if (moveDistance > distance)
                {
                    moveDistance = distance;
                }

                transform.position = currentPos + direction * moveDistance;



            }
        }
    }

    /// <summary>
    /// Performing state - execute the task
    /// </summary>
    protected virtual void UpdatePerforming()
    {
        if (currentTask == null)
        {
            SetState(HelperState.Idle);
            return;
        }

        // Count down task timer
        taskTimer -= Time.deltaTime;

        if (taskTimer <= 0f)
        {
            // Task duration complete - execute it
            ExecuteTask();
        }
    }

    /// <summary>
    /// Assign a task to this helper
    /// </summary>
    public virtual bool AssignTask(HelperTask task)
    {
        if (task == null || !task.IsValid())
        {
            return false;
        }

        if (HasTask)
        {
            Debug.LogWarning($"{helperName} already has a task!");
            return false;
        }

        currentTask = task;
        currentTask.IsClaimed = true;
        SetState(HelperState.MovingToTask);
        
        return true;
    }

    /// <summary>
    /// Execute the current task
    /// Override in child classes for specific task logic
    /// </summary>
    protected virtual void ExecuteTask()
    {
        if (currentTask == null)
        {
            SetState(HelperState.Idle);
            return;
        }

        // Validate task is still valid
        if (!currentTask.IsValid())
        {
            Debug.LogWarning($"{helperName} task became invalid: {currentTask}");
            CancelTask();
            return;
        }

        // Child classes implement actual execution
        // Base class just completes the task
        CompleteTask();
    }

    /// <summary>
    /// Mark current task as complete
    /// </summary>
    protected void CompleteTask()
    {
        if (currentTask != null)
        {
            currentTask.IsCompleted = true;

        }

        currentTask = null;
        SetState(HelperState.Idle);
    }

    /// <summary>
    /// Cancel current task
    /// </summary>
    protected void CancelTask()
    {
        if (currentTask != null)
        {
            currentTask.IsClaimed = false;

        }

        currentTask = null;
        SetState(HelperState.Idle);
    }

    /// <summary>
    /// Change helper state
    /// </summary>
    protected void SetState(HelperState newState)
    {
        if (currentState == newState) return;

        currentState = newState;
        OnStateChanged(newState);
    }

    /// <summary>
    /// Called when state changes
    /// Override for state-specific setup
    /// </summary>
    protected virtual void OnStateChanged(HelperState newState)
    {
        // Child classes can override for specific behavior
    }

    /// <summary>
    /// Get position for movement target
    /// </summary>
    protected Vector3 GetTargetPosition()
    {
        if (currentTask != null && currentTask.TargetTile != null)
        {
            return currentTask.TargetTile.transform.position;
        }
        return transform.position;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Debug info
    /// </summary>
    [ContextMenu("Show Helper Info")]
    protected void ShowHelperInfo()
    {
        Debug.Log($"=== {helperName} ===");
        Debug.Log($"State: {currentState}");
        Debug.Log($"Position: {transform.position}");
        Debug.Log($"Move Speed: {moveSpeed} (Upgraded: {UpgradedMoveSpeed:F2})");
        Debug.Log($"Use Instant Movement: {useInstantMovement}");
        Debug.Log($"Has Task: {HasTask}");
        if (HasTask)
        {
            Debug.Log($"Current Task: {currentTask}");
            if (currentTask.TargetTile != null)
            {
                Debug.Log($"Target Position: {currentTask.TargetTile.transform.position}");
                Debug.Log($"Distance to Target: {Vector3.Distance(transform.position, currentTask.TargetTile.transform.position):F2}");
            }
        }
    }

    [ContextMenu("Toggle Instant Movement")]
    protected void ToggleInstantMovement()
    {
        useInstantMovement = !useInstantMovement;
        Debug.Log($"{helperName} instant movement: {useInstantMovement}");
    }

    [ContextMenu("Toggle Movement Debug")]
    protected void ToggleMovementDebug()
    {
        showMovementDebug = !showMovementDebug;
        Debug.Log($"{helperName} movement debug: {showMovementDebug}");
    }
#endif
}