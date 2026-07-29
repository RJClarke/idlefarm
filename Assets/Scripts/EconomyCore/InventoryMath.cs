using System.Collections.Generic;
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

    /// <summary>
    /// Total Coins for selling the first <paramref name="n"/> ready jars. Jars carry individual
    /// values (a tier-3 sauce is worth far more than a tier-1 jam), so a stack sale sums the jars
    /// actually consumed rather than multiplying a unit price. Selling always takes from the front
    /// of the list, so this preview and the real sale agree by construction. Clamped to the list;
    /// a non-positive n, an empty list, or null pays nothing.
    /// </summary>
    public static int JarStackValue(IReadOnlyList<int> values, int n)
    {
        if (values == null || n <= 0) return 0;
        int take = Mathf.Min(n, values.Count);
        int total = 0;
        for (int i = 0; i < take; i++) total += Mathf.Max(0, values[i]);
        return total;
    }
}
