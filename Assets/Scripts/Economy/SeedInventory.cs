using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-run seed-bag inventory. One bag = N seeds of a crop. Bags are bought lazily:
/// when a helper needs a seed and the active bag is empty, we auto-buy if money allows.
/// Seed state is per-run only (never saved) — resets on OnRunStarted.
/// </summary>
public class SeedInventory : MonoBehaviour
{
    public static SeedInventory Instance { get; private set; }

    // crop -> seeds remaining in the current bag
    private readonly Dictionary<CropData, int> _seeds = new Dictionary<CropData, int>();

    /// <summary>Fires (crop, seedsRemaining) whenever a crop's seed count changes.</summary>
    public event Action<CropData, int> OnSeedCountChanged;

    /// <summary>Fires (crop, costPaid) when a bag is auto-bought. The HUD animates the spend from that bag.</summary>
    public event Action<CropData, int> OnBagPurchased;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnRunStarted += ResetInventory;
    }

    private void OnDestroy()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnRunStarted -= ResetInventory;
    }

    private void ResetInventory()
    {
        var crops = new List<CropData>(_seeds.Keys);
        _seeds.Clear();
        foreach (var c in crops) OnSeedCountChanged?.Invoke(c, 0);
    }

    public int SeedsRemaining(CropData crop)
    {
        if (crop == null) return 0;
        return _seeds.TryGetValue(crop, out int n) ? n : 0;
    }

    /// <summary>Current money cost of one bag of this crop (run-time escalated, discounted).</summary>
    public int BagCost(CropData crop)
    {
        if (crop == null) return int.MaxValue;
        float runMinutes = RunManager.Instance != null ? RunManager.Instance.CurrentRunDuration / 60f : 0f;
        float discount = ResearchManager.Instance != null
            ? ResearchManager.Instance.GetBonus(Research.StatKey.SeedBagDiscount) : 0f;
        return SeedEconomy.BagCost(crop.seedBagBaseCost, runMinutes, discount);
    }

    /// <summary>True if this crop can be planted now (has a seed, or a bag is affordable).</summary>
    public bool CanPlant(CropData crop)
    {
        if (crop == null) return false;
        if (SeedsRemaining(crop) > 0) return true;
        return CurrencyManager.Instance != null && CurrencyManager.Instance.CanAffordMoney(BagCost(crop));
    }

    /// <summary>
    /// Consume one seed for planting. Buys a bag (debiting money + showing a -$ popup) if the
    /// current bag is empty. Returns false if no seed and no affordable bag (caller skips planting).
    /// </summary>
    public bool TryConsumeSeed(CropData crop, Vector3 worldPos)
    {
        if (crop == null) return false;

        if (SeedsRemaining(crop) <= 0)
        {
            if (!TryBuyBag(crop, worldPos)) return false;
        }

        _seeds[crop] = SeedsRemaining(crop) - 1;
        OnSeedCountChanged?.Invoke(crop, _seeds[crop]);
        return true;
    }

    /// <summary>
    /// Add seeds back to the current bag without charging (Seed Refund farm upgrade).
    /// No-op outside a run / for null crops.
    /// </summary>
    public void RefundSeed(CropData crop, int count = 1)
    {
        if (crop == null || count <= 0) return;
        if (RunManager.Instance != null && !RunManager.Instance.IsRunActive) return;
        _seeds[crop] = SeedsRemaining(crop) + count;
        OnSeedCountChanged?.Invoke(crop, _seeds[crop]);
    }

    private bool TryBuyBag(CropData crop, Vector3 worldPos)
    {
        if (CurrencyManager.Instance == null) return false;
        int cost = BagCost(crop);
        if (!CurrencyManager.Instance.SpendMoney(cost)) return false;

        float sizeBonus = ResearchManager.Instance != null
            ? ResearchManager.Instance.GetBonus(Research.StatKey.SeedBagSize) : 0f;
        int bagSize = SeedEconomy.BagSize(crop.seedBagSize, sizeBonus);

        _seeds[crop] = SeedsRemaining(crop) + bagSize;
        // The HUD owns the spend visual so it animates from the bag widget (anchors "had to buy this").
        // Fire count change first so the widget exists/updates before the purchase animation.
        OnSeedCountChanged?.Invoke(crop, _seeds[crop]);
        OnBagPurchased?.Invoke(crop, cost);
        return true;
    }
}
