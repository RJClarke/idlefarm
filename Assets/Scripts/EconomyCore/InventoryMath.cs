using UnityEngine;

/// <summary>
/// Pure math for the item inventory (Reputation Phase 1). Raw crop pricing must stay below the
/// Cannery jar multipliers (2.5+) so processed always beats raw (spec §2).
/// </summary>
public static class InventoryMath
{
    /// <summary>Coins paid for one raw collected crop.</summary>
    public static int RawCropCoinValue(int harvestValue, float rawSellRate)
        => Mathf.Max(1, Mathf.RoundToInt(harvestValue * rawSellRate));

    /// <summary>
    /// Add <paramref name="add"/> onto <paramref name="current"/> under <paramref name="cap"/>.
    /// Returns the amount actually stored; <paramref name="overflow"/> is what didn't fit.
    /// </summary>
    public static int AddToCap(int current, int add, int cap, out int overflow)
    {
        if (add <= 0) { overflow = 0; return 0; }
        int room = Mathf.Max(0, cap - current);
        int stored = Mathf.Min(add, room);
        overflow = add - stored;
        return stored;
    }
}
