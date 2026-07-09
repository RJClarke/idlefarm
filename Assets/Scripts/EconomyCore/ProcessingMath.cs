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

/// <summary>A finished processed good on the ready shelf / awaiting deposit, waiting to be sold or
/// banked for Coins. `tier`/`sourceId` let a consumer (e.g. the Smokehouse) route it by kind.</summary>
[Serializable]
public class ReadyJar
{
    public string cropName;
    public int value;
    public int tier;        // 1..3 — copied from the finishing slot (0 on legacy jars)
    public string sourceId; // slot.cropId — the crop/fish that produced this good
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

    /// <summary>
    /// Purchasable slot cap after research expansions (spec §5, Phase 3). Each unlocked expansion adds
    /// `slotsPerExpansion` slots on top of the coin-purchasable base, clamped to the building's hard
    /// total. Negative inputs are treated as zero.
    /// </summary>
    public static int EffectiveSlotCap(int basePurchasable, int totalMax, int expansionsUnlocked, int slotsPerExpansion)
        => Mathf.Min(totalMax, basePurchasable + Mathf.Max(0, expansionsUnlocked) * Mathf.Max(0, slotsPerExpansion));

    /// <summary>
    /// Per-slot wood burn after a fuel-efficiency research bonus (spec §6). `efficiencyBonus` is a
    /// cumulative fraction (as returned by ResearchManager.GetBonus); `maxReduction` clamps total
    /// savings so demand never hits zero and the "keep chopping" loop survives. Only per-slot burn is
    /// reduced — base (empty-fire waste) burn is untouched.
    /// </summary>
    public static float EffectiveBurnPerSlot(float basePerSlotBurn, float efficiencyBonus, float maxReduction)
        => basePerSlotBurn * (1f - Mathf.Clamp(efficiencyBonus, 0f, Mathf.Clamp01(maxReduction)));

    // ── Firebox simulation (spec §2) ─────────────────────────────────────

    /// <summary>Wood consumed per second while the fire is lit: base + perSlot × cooking jars.</summary>
    public static double BurnRatePerSecond(int cookingSlots, float baseWoodPerHour, float perSlotWoodPerHour)
        => (Mathf.Max(0f, baseWoodPerHour) + Mathf.Max(0f, perSlotWoodPerHour) * Mathf.Max(0, cookingSlots)) / 3600.0;

    /// <summary>
    /// Advance the building by elapsed wall-clock seconds (piecewise: burn rate changes when a
    /// jar finishes). Fire is lit while fuelWood > 0 and burns even with nothing cooking (waste
    /// is intentional, spec §2). Fuel out → cooking pauses; nothing is ever ruined. Finished
    /// jars move to readyJars and free their slot immediately. Returns jars finished.
    /// </summary>
    public static int Simulate(CanneryState st, double elapsedSeconds, float baseWoodPerHour, float perSlotWoodPerHour)
    {
        int finished = 0;
        double remaining = elapsedSeconds;
        int guard = 0;
        while (remaining > 1e-9 && st.fuelWood > 1e-9 && ++guard < 100000)
        {
            int cooking = CountCooking(st);
            double rate = BurnRatePerSecond(cooking, baseWoodPerHour, perSlotWoodPerHour);
            if (rate <= 0) break; // base 0 + nothing cooking: fire idles for free

            double fuelSecs = st.fuelWood / rate;
            double nextFinish = double.MaxValue;
            for (int i = 0; i < st.slots.Length; i++)
                if (SlotIsCooking(st.slots[i]) && st.slots[i].cookSecondsRemaining < nextFinish)
                    nextFinish = st.slots[i].cookSecondsRemaining;

            double step = Math.Min(remaining, Math.Min(fuelSecs, nextFinish));
            if (step <= 0) break;

            st.fuelWood = Math.Max(0, st.fuelWood - rate * step);
            for (int i = 0; i < st.slots.Length; i++)
            {
                var s = st.slots[i];
                if (!SlotIsCooking(s)) continue;
                s.cookSecondsRemaining -= step;
                if (s.cookSecondsRemaining <= 1e-6)
                {
                    st.readyJars.Add(new ReadyJar { cropName = s.cropName, value = s.jarValue, tier = s.tier, sourceId = s.cropId });
                    finished++;
                    s.cropId = null; s.cropName = null; s.tier = 0;
                    s.unitsLoaded = 0; s.unitsRequired = 0;
                    s.cookSecondsRemaining = 0; s.jarValue = 0;
                }
            }
            remaining -= step;
        }
        return finished;
    }

    /// <summary>
    /// Extra wood (beyond current fuel) needed so every currently-COOKING jar finishes —
    /// the "Stoke to finish" amount (spec §2). Piecewise like Simulate; loading-but-not-full
    /// jars don't burn and aren't counted. Never negative.
    /// </summary>
    public static double WoodToFinishLoaded(CanneryState st, float baseWoodPerHour, float perSlotWoodPerHour)
    {
        var remain = new List<double>();
        for (int i = 0; i < st.slots.Length; i++)
            if (SlotIsCooking(st.slots[i])) remain.Add(st.slots[i].cookSecondsRemaining);
        if (remain.Count == 0) return 0.0;

        double needed = 0.0;
        while (remain.Count > 0)
        {
            remain.Sort();
            double dt = remain[0];
            needed += BurnRatePerSecond(remain.Count, baseWoodPerHour, perSlotWoodPerHour) * dt;
            for (int i = remain.Count - 1; i >= 0; i--)
            {
                remain[i] -= dt;
                if (remain[i] <= 1e-6) remain.RemoveAt(i);
            }
        }
        return Math.Max(0.0, needed - st.fuelWood);
    }
}
