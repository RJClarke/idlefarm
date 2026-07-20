using System;
using System.Collections.Generic;

/// <summary>Serializable itemId → count entry (JsonUtility can't serialize Dictionary).</summary>
[Serializable]
public class ItemStackEntry
{
    public string itemId;
    public int count;
}

/// <summary>
/// Pure inventory state for collected farm output (Reputation Phase 1): crop stacks keyed by
/// cropName, the egg stack, and the Collect/Sell mode flag. Lives in EconomyCore so EditMode
/// tests can reach it; ItemInventoryManager wraps it with events, tuning knobs, and save wiring.
/// </summary>
public class ItemInventoryCore
{
    private readonly Dictionary<string, int> crops = new Dictionary<string, int>();

    public int CropCap { get; }
    public int EggCap { get; }
    public int Eggs { get; private set; }
    public bool CollectMode { get; set; }
    public IReadOnlyDictionary<string, int> Crops => crops;

    public ItemInventoryCore(int cropCap, int eggCap)
    {
        CropCap = cropCap;
        EggCap = eggCap;
    }

    public int GetCrop(string cropName)
        => !string.IsNullOrEmpty(cropName) && crops.TryGetValue(cropName, out int n) ? n : 0;

    /// <summary>Returns the amount actually stored under the cap; overflow is what didn't fit.</summary>
    public int AddCrop(string cropName, int amount, out int overflow)
    {
        overflow = 0;
        if (string.IsNullOrEmpty(cropName)) return 0;
        int stored = InventoryMath.AddToCap(GetCrop(cropName), amount, CropCap, out overflow);
        if (stored > 0) crops[cropName] = GetCrop(cropName) + stored;
        return stored;
    }

    public bool TrySpendCrop(string cropName, int amount)
    {
        if (amount <= 0 || GetCrop(cropName) < amount) return false;
        crops[cropName] -= amount;
        return true;
    }

    public int AddEggs(int amount, out int overflow)
    {
        int stored = InventoryMath.AddToCap(Eggs, amount, EggCap, out overflow);
        if (stored > 0) Eggs += stored;
        return stored;
    }

    public bool TrySpendEggs(int amount)
    {
        if (amount <= 0 || Eggs < amount) return false;
        Eggs -= amount;
        return true;
    }

    public ItemStackEntry[] ExportCrops()
    {
        var list = new List<ItemStackEntry>(crops.Count);
        foreach (var kv in crops)
            if (kv.Value > 0) list.Add(new ItemStackEntry { itemId = kv.Key, count = kv.Value });
        return list.ToArray();
    }

    /// <summary>Replaces all state from saved data. Null/legacy input → empty inventory.</summary>
    public void Import(ItemStackEntry[] cropEntries, int eggCount, bool collectMode)
    {
        crops.Clear();
        if (cropEntries != null)
            foreach (var e in cropEntries)
                if (e != null && !string.IsNullOrEmpty(e.itemId) && e.count > 0)
                    crops[e.itemId] = Math.Min(e.count, CropCap);
        Eggs = Math.Max(0, Math.Min(eggCount, EggCap));
        CollectMode = collectMode;
    }
}
