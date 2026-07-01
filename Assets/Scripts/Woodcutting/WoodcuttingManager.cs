using System;
using UnityEngine;

/// <summary>
/// Owns woodcutting meta-state: the axe level, its tuning, and the upgrade transaction.
/// Wood the resource lives on CurrencyManager; this is the domain state around it.
/// Axe level persists via GameData.axeLevel (SaveManager).
/// </summary>
public class WoodcuttingManager : MonoBehaviour
{
    public static WoodcuttingManager Instance { get; private set; }

    [Header("Axe Tuning")]
    [SerializeField] private int maxAxeLevel = 3;
    [SerializeField] private int hitsReductionPerLevel = 1;
    [Tooltip("Coin cost per next axe level, indexed by current level (0 -> level 1, ...).")]
    [SerializeField] private int[] axeCoinCosts = { 250, 750, 2000 };
    [Tooltip("Wood cost per next axe level, indexed by current level.")]
    [SerializeField] private int[] axeWoodCosts = { 20, 60, 150 };

    private int axeLevel;

    public int AxeLevel => axeLevel;
    public int MaxAxeLevel => maxAxeLevel;
    public int HitsReductionPerLevel => hitsReductionPerLevel;
    public event Action<int> OnAxeLevelChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public int NextUpgradeCoinCost()
    {
        if (axeLevel >= maxAxeLevel || axeCoinCosts.Length == 0) return int.MaxValue;
        return axeCoinCosts[Mathf.Min(axeLevel, axeCoinCosts.Length - 1)];
    }

    public int NextUpgradeWoodCost()
    {
        if (axeLevel >= maxAxeLevel || axeWoodCosts.Length == 0) return int.MaxValue;
        return axeWoodCosts[Mathf.Min(axeLevel, axeWoodCosts.Length - 1)];
    }

    public bool CanUpgradeAxe()
    {
        var cm = CurrencyManager.Instance;
        if (cm == null) return false;
        return WoodcuttingMath.CanUpgradeAxe(axeLevel, maxAxeLevel, cm.Coins, NextUpgradeCoinCost(), cm.Wood, NextUpgradeWoodCost());
    }

    /// <summary>Spends Coins + Wood and raises the axe level by one. Returns false if not allowed.</summary>
    public bool TryUpgradeAxe()
    {
        if (!CanUpgradeAxe()) return false;
        var cm = CurrencyManager.Instance;
        int coinCost = NextUpgradeCoinCost();
        int woodCost = NextUpgradeWoodCost();
        if (!cm.SpendCoins(coinCost)) return false;
        if (!cm.SpendWood(woodCost)) { cm.AddCoins(coinCost); return false; } // refund coins if wood spend fails
        axeLevel++;
        OnAxeLevelChanged?.Invoke(axeLevel);
        return true;
    }

    public void SetAxeLevel(int level)
    {
        axeLevel = Mathf.Clamp(level, 0, maxAxeLevel);
        OnAxeLevelChanged?.Invoke(axeLevel);
    }
}
