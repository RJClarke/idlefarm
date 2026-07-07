using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One jar slot in a processing building. A slot fills with units of ONE crop; once
/// unitsLoaded reaches unitsRequired the jar starts cooking (cookSecondsRemaining set)
/// and progresses only while the shared fire is lit.
/// </summary>
[Serializable]
public class CannerySlot
{
    public string cropId;                // CropData asset name (stable id)
    public string cropName;              // display name
    public int tier;                     // 1..3 → 4h/8h/12h
    public int unitsLoaded;
    public int unitsRequired;
    public double cookSecondsRemaining;  // > 0 only once fully loaded
    public int jarValue;                 // Coins on completion, fixed at jar creation
}

/// <summary>A finished jar on the ready shelf, waiting to be sold for Coins.</summary>
[Serializable]
public class ReadyJar
{
    public string cropName;
    public int value;
}

/// <summary>Mutable state for one processing building (Cannery in Phase 1).</summary>
public class CanneryState
{
    public CannerySlot[] slots = new CannerySlot[0];
    public double fuelWood;              // wood units in the furnace (burns fractionally)
    public List<ReadyJar> readyJars = new List<ReadyJar>();
}

/// <summary>
/// Pure decision + simulation logic for wood-fired processing buildings (Pantry Economy
/// spec §2, §4). No Unity object dependencies; fully unit-testable. MonoBehaviours
/// (CanneryManager, UI) route their math through here, mirroring WoodcuttingMath.
/// </summary>
public static class ProcessingMath
{
    /// <summary>Cook duration in hours by crop tier: 1→4h, 2→8h, 3→12h.</summary>
    public static int CookHoursForTier(int tier) => 4 * Mathf.Clamp(tier, 1, 3);

    /// <summary>Input units per jar = 1 per cook-hour (spec §4: jam=4, compote=8, sauce=12).</summary>
    public static int UnitsRequiredForTier(int tier) => CookHoursForTier(tier);

    /// <summary>Jar sale value in Coins: units × base crop value × tier multiplier, min 1.</summary>
    public static int JarValue(int baseUnitValue, int tier, float multiplier)
    {
        int units = UnitsRequiredForTier(tier);
        return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0, baseUnitValue) * units * Mathf.Max(0f, multiplier)));
    }

    public static bool SlotIsEmpty(CannerySlot s) => s == null || string.IsNullOrEmpty(s.cropId);

    public static bool SlotIsCooking(CannerySlot s)
        => !SlotIsEmpty(s) && s.unitsLoaded >= s.unitsRequired && s.cookSecondsRemaining > 0;

    public static int CountCooking(CanneryState st)
    {
        int n = 0;
        for (int i = 0; i < st.slots.Length; i++)
            if (SlotIsCooking(st.slots[i])) n++;
        return n;
    }

    /// <summary>
    /// Where an incoming harvested unit goes: a partial jar of the same crop first,
    /// else the first empty slot, else -1 (cannery full → harvest pays out normally).
    /// </summary>
    public static int FindIntakeSlot(CanneryState st, string cropId)
    {
        for (int i = 0; i < st.slots.Length; i++)
        {
            var s = st.slots[i];
            if (!SlotIsEmpty(s) && s.cropId == cropId && s.unitsLoaded < s.unitsRequired) return i;
        }
        for (int i = 0; i < st.slots.Length; i++)
            if (SlotIsEmpty(st.slots[i])) return i;
        return -1;
    }

    /// <summary>Whether the next slot can be bought: under the purchasable cap and both currencies afford it.</summary>
    public static bool CanBuySlot(int slotsOwned, int maxPurchasable, int coins, int coinCost, int wood, int woodCost)
    {
        if (slotsOwned >= maxPurchasable) return false;
        return coins >= coinCost && wood >= woodCost;
    }
}
