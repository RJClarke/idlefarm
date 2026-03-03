using UnityEngine;

/// <summary>
/// Base class for all menu panels (Market, Farm, Helpers, Equipment, Settings)
/// Handles common functionality like initialization, showing/hiding, and cleanup
/// </summary>
public abstract class MenuPanel : MonoBehaviour
{
    [Header("Menu Settings")]
    [SerializeField] protected string menuTitle = "Menu";
    [SerializeField] protected bool debugMode = false;

    // Called when panel becomes active
    protected virtual void OnEnable()
    {
        OnPanelOpened();
    }

    // Called when panel becomes inactive
    protected virtual void OnDisable()
    {
        OnPanelClosed();
    }

    /// <summary>
    /// Override this to handle panel opening logic
    /// Example: Refresh UI, load data, etc.
    /// </summary>
    protected virtual void OnPanelOpened()
    {
        // Override in child classes
    }

    /// <summary>
    /// Override this to handle panel closing logic
    /// Example: Save state, cleanup, etc.
    /// </summary>
    protected virtual void OnPanelClosed()
    {
        // Override in child classes
    }

    /// <summary>
    /// Check if player can afford something
    /// </summary>
    protected bool CanAfford(int cost, bool useCoins)
    {
        if (CurrencyManager.Instance == null) return false;

        if (useCoins)
        {
            return CurrencyManager.Instance.CanAffordCoins(cost);
        }
        else
        {
            return CurrencyManager.Instance.CanAffordMoney(cost);
        }
    }

    /// <summary>
    /// Attempt to purchase something
    /// </summary>
    protected bool TryPurchase(int cost, bool useCoins)
    {
        if (CurrencyManager.Instance == null) return false;

        if (useCoins)
        {
            return CurrencyManager.Instance.SpendCoins(cost);
        }
        else
        {
            return CurrencyManager.Instance.SpendMoney(cost);
        }
    }

    /// <summary>
    /// Check if we're currently in a run (Farm Mode)
    /// </summary>
    protected bool IsInRun()
    {
        return RunManager.Instance != null && RunManager.Instance.IsRunActive;
    }

    /// <summary>
    /// Check if we're between runs (Town Mode)
    /// </summary>
    protected bool IsBetweenRuns()
    {
        return !IsInRun();
    }
}