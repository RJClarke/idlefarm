using System;
using UnityEngine;

/// <summary>
/// Counted stacks of collected farm output (crops by cropName, plus eggs) — the PantryManager
/// pattern: ints + change events, no item objects. Owns the run-HUD Collect/Sell mode.
/// Pure state lives in ItemInventoryCore (EconomyCore, unit-tested); this wrapper adds the
/// singleton, tuning knobs, events, and save wiring.
/// Spec: 2026-07-19-reputation-design.md §5. Caps are raised later by Renown silo addons.
/// </summary>
public class ItemInventoryManager : MonoBehaviour
{
    public static ItemInventoryManager Instance { get; private set; }

    [Header("Tuning")]
    [SerializeField] private int cropCapPerType = 500;
    [SerializeField] private int eggCap = 200;
    [Tooltip("Coins per raw crop = harvestValue × this. Keep BELOW 2.5 (lowest jar multiplier) so processed beats raw.")]
    [SerializeField] private float rawSellRate = 1.5f;
    [SerializeField] private int eggSellCoins = 25;

    private ItemInventoryCore core;
    private ItemInventoryCore Core => core ?? (core = new ItemInventoryCore(cropCapPerType, eggCap));

    public event Action OnChanged;
    public event Action<bool> OnCollectModeChanged;

    public int CropCapPerType => cropCapPerType;
    public int EggCap => eggCap;
    public int EggSellCoins => eggSellCoins;
    public int Eggs => Core.Eggs;
    public System.Collections.Generic.IReadOnlyDictionary<string, int> Crops => Core.Crops;

    public bool CollectMode
    {
        get => Core.CollectMode;
        set
        {
            if (Core.CollectMode == value) return;
            Core.CollectMode = value;
            OnCollectModeChanged?.Invoke(value);
            OnChanged?.Invoke();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public int GetCrop(string cropName) => Core.GetCrop(cropName);

    public int RawCoinValue(CropData crop)
        => crop == null ? 0 : InventoryMath.RawCropCoinValue(crop.harvestValue, rawSellRate);

    public int AddCrop(string cropName, int amount, out int overflow)
    {
        int stored = Core.AddCrop(cropName, amount, out overflow);
        if (stored > 0) OnChanged?.Invoke();
        return stored;
    }

    public bool TrySpendCrop(string cropName, int amount)
    {
        if (!Core.TrySpendCrop(cropName, amount)) return false;
        OnChanged?.Invoke();
        return true;
    }

    public int AddEggs(int amount, out int overflow)
    {
        int stored = Core.AddEggs(amount, out overflow);
        if (stored > 0) OnChanged?.Invoke();
        return stored;
    }

    public bool TrySpendEggs(int amount)
    {
        if (!Core.TrySpendEggs(amount)) return false;
        OnChanged?.Invoke();
        return true;
    }

    public void CaptureTo(GameData d)
    {
        d.cropStacks = Core.ExportCrops();
        d.eggStack = Core.Eggs;
        d.collectModeOn = Core.CollectMode;
    }

    public void LoadFrom(GameData d)
    {
        Core.Import(d.cropStacks, d.eggStack, d.collectModeOn);
        OnCollectModeChanged?.Invoke(Core.CollectMode);
        OnChanged?.Invoke();
    }
}
