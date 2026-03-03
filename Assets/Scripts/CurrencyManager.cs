using UnityEngine;
using System;

/// <summary>
/// Manages two types of currency:
/// - Money: Temporary currency earned during a run (resets each run)
/// - Coins: Permanent currency that persists across runs
/// </summary>
public class CurrencyManager : MonoBehaviour
{
    // Singleton instance for easy access from anywhere
    public static CurrencyManager Instance { get; private set; }

    [Header("Current Currency Values")]
    [SerializeField] private int currentMoney = 0;  // Run-based currency
    [SerializeField] private int currentCoins = 0;  // Permanent currency

    [Header("Starting Values")]
    [SerializeField] private int startingMoney = 100; // Money at start of each run
    [SerializeField] private int startingCoins = 0;   // Only used for testing

    // Events that other scripts can subscribe to
    public event Action<int> OnMoneyChanged;
    public event Action<int> OnCoinsChanged;

    // Properties for read-only access
    public int Money => currentMoney;
    public int Coins => currentCoins;

    private void Awake()
    {
        // Singleton pattern - only one CurrencyManager should exist
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Persist between scenes
    }

    private void Start()
    {
        // Try to load saved data
        if (SaveManager.Instance != null && SaveManager.Instance.LoadGame())
        {
            // Coins are already set by LoadGame()
        }
        else
        {
            // New game - use starting values
            SetCoins(startingCoins);
        }
        
        // Always reset money for new run
        ResetMoneyForNewRun();
    }

    #region Money Management (Run-Based Currency)

    /// <summary>
    /// Add money to current run total
    /// </summary>
    public void AddMoney(int amount)
    {
        if (amount < 0)
        {
            Debug.LogWarning("Cannot add negative money. Use SpendMoney instead.");
            return;
        }

        currentMoney += amount;
        OnMoneyChanged?.Invoke(currentMoney);
    }

    /// <summary>
    /// Attempt to spend money. Returns true if successful.
    /// </summary>
    public bool SpendMoney(int amount)
    {
        if (amount < 0)
        {
            Debug.LogWarning("Cannot spend negative money.");
            return false;
        }

        if (currentMoney >= amount)
        {
            currentMoney -= amount;
            OnMoneyChanged?.Invoke(currentMoney);
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Check if player can afford something
    /// </summary>
    public bool CanAffordMoney(int amount)
    {
        return currentMoney >= amount;
    }

    /// <summary>
    /// Reset money to starting value (called at beginning of each run)
    /// </summary>
    public void ResetMoneyForNewRun()
    {
        currentMoney = startingMoney;
        OnMoneyChanged?.Invoke(currentMoney);
    }

    #endregion

    #region Coin Management (Permanent Currency)

    /// <summary>
    /// Add coins to permanent total
    /// </summary>
    public void AddCoins(int amount)
    {
        if (amount < 0)
        {
            Debug.LogWarning("Cannot add negative coins. Use SpendCoins instead.");
            return;
        }

        currentCoins += amount;
        OnCoinsChanged?.Invoke(currentCoins);
    }

    /// <summary>
    /// Attempt to spend coins. Returns true if successful.
    /// </summary>
    public bool SpendCoins(int amount)
    {
        if (amount < 0)
        {
            Debug.LogWarning("Cannot spend negative coins.");
            return false;
        }

        if (currentCoins >= amount)
        {
            currentCoins -= amount;
            OnCoinsChanged?.Invoke(currentCoins);
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Check if player can afford something with coins
    /// </summary>
    public bool CanAffordCoins(int amount)
    {
        return currentCoins >= amount;
    }

    /// <summary>
    /// Set coins directly (used when loading from save data)
    /// </summary>
    public void SetCoins(int amount)
    {
        currentCoins = amount;
        OnCoinsChanged?.Invoke(currentCoins);
    }

    #endregion

    #region Testing/Debug Methods

    // These methods are useful for testing in the Unity Editor
    [ContextMenu("Add 100 Money")]
    private void TestAddMoney()
    {
        AddMoney(100);
    }

    [ContextMenu("Add 10 Coins")]
    private void TestAddCoins()
    {
        AddCoins(10);
    }

    [ContextMenu("Reset Money")]
    private void TestResetMoney()
    {
        ResetMoneyForNewRun();
    }

    #endregion
}