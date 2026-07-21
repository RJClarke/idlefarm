using UnityEngine;

/// <summary>
/// Pure decision logic for the Woodcutting system. No Unity object dependencies so it is
/// fully unit-testable. All MonoBehaviours (TreeNode, WoodcuttingManager, sell UI) route
/// their math through here.
/// </summary>
public static class WoodcuttingMath
{
    public enum StackMode { One, Ten, All }

    /// <summary>Taps needed to fell a tree, reduced by axe level, never below minHits.</summary>
    public static int EffectiveHitsToFell(int baseHits, int axeLevel, int reductionPerLevel, int minHits = 1)
    {
        int reduced = baseHits - Mathf.Max(0, axeLevel) * Mathf.Max(0, reductionPerLevel);
        return Mathf.Max(minHits, reduced);
    }

    /// <summary>True if the current axe can fell a tree with the given axe-level requirement.</summary>
    public static bool CanFell(int requiredAxeLevel, int axeLevel) => axeLevel >= requiredAxeLevel;

    /// <summary>0..1 regrowth progress. A zero (or negative) duration is treated as instantly full.</summary>
    public static float RegrowFraction(double elapsedSeconds, float regrowSeconds)
    {
        if (regrowSeconds <= 0f) return 1f;
        return Mathf.Clamp01((float)(elapsedSeconds / regrowSeconds));
    }

    public static bool IsRegrown(double elapsedSeconds, float regrowSeconds) => RegrowFraction(elapsedSeconds, regrowSeconds) >= 1f;

    /// <summary>How many units a stack button sells, clamped to what the player owns.</summary>
    public static int ResolveStackAmount(StackMode mode, int available)
    {
        if (available <= 0) return 0;
        switch (mode)
        {
            case StackMode.One: return Mathf.Min(1, available);
            case StackMode.Ten: return Mathf.Min(10, available);
            default: return available; // All
        }
    }

    /// <summary>Total proceeds for selling `amount` units at `pricePerUnit`. Never negative.</summary>
    public static int SellValue(int amount, int pricePerUnit) => Mathf.Max(0, amount) * Mathf.Max(0, pricePerUnit);

    /// <summary>Whether the first axe can be bought: not already owned and enough Coins. Wood-free,
    /// because you can't gather Wood until you own an axe (chicken-and-egg).</summary>
    public static bool CanBuyAxe(bool hasAxe, int coins, int coinCost) => !hasAxe && coins >= coinCost;

    /// <summary>Whether an axe upgrade is allowed: under max level and both currencies affordable.</summary>
    public static bool CanUpgradeAxe(int axeLevel, int maxLevel, int coins, int coinCost, int wood, int woodCost)
    {
        if (axeLevel >= maxLevel) return false;
        return coins >= coinCost && wood >= woodCost;
    }

    // ── Tree growth (sapling -> full over stageCount stages) ──────────────

    /// <summary>Current growth stage (0 = sapling .. stageCount-1 = full grown) for a 0..1 growth fraction.</summary>
    public static int StageIndex(float growthFraction, int stageCount)
    {
        if (stageCount <= 1) return 0;
        int idx = Mathf.FloorToInt(Mathf.Clamp01(growthFraction) * stageCount);
        return Mathf.Clamp(idx, 0, stageCount - 1);
    }

    /// <summary>Wood from felling at a given stage: nothing at stage 0 (a fresh sapling must never
    /// pay out, or instant chop-replant-chop mints unbounded wood), scaling to full yield at the
    /// last stage.</summary>
    public static int StageYield(int fullYield, int stageIndex, int stageCount)
    {
        if (stageCount <= 1) return Mathf.Max(0, fullYield);
        int stage = Mathf.Clamp(stageIndex, 0, stageCount - 1);
        return Mathf.RoundToInt(Mathf.Max(0, fullYield) * stage / (float)(stageCount - 1));
    }

    /// <summary>Taps to fell scaled by stage — saplings fall fast, full trees take the full count (min 1).</summary>
    public static int StageHits(int fullHits, int stageIndex, int stageCount)
    {
        if (stageCount <= 0) return Mathf.Max(1, fullHits);
        int stage = Mathf.Clamp(stageIndex, 0, stageCount - 1);
        return Mathf.Max(1, Mathf.RoundToInt(fullHits * (stage + 1) / (float)stageCount));
    }

    /// <summary>Fraction of a tree's wood paid on the felling blow. The remaining
    /// (1 - this) is split evenly across the earlier swings as small teasers, so the final hit is
    /// always the big reward.</summary>
    public const float DefaultFinalBlowShare = 0.6f;

    /// <summary>Wood credited on a single swing so the payout drips out as you chop instead of
    /// landing in one lump. Earlier swings each get a small even slice of the leading
    /// (1 - <paramref name="finalShare"/>) portion; the felling blow takes the rest — the bulk — so
    /// it's always the largest single reward and the chop feels lopsided toward the finish. With the
    /// 0.6 default this is 10 wood over 5 hits → 1,1,1,1,6 (i.e. 10/10/10/10/60%). Swings always sum
    /// to exactly totalYield. <paramref name="hitIndex"/> is 1-based (neededHits = the felling swing).</summary>
    public static int SwingWood(int totalYield, int hitIndex, int neededHits, float finalShare = DefaultFinalBlowShare)
    {
        totalYield = Mathf.Max(0, totalYield);
        neededHits = Mathf.Max(1, neededHits);
        hitIndex = Mathf.Clamp(hitIndex, 1, neededHits);
        if (neededHits == 1) return totalYield; // a one-hit fell gets the whole tree on that blow

        finalShare = Mathf.Clamp01(finalShare);
        // Round the leading pool to an int first (so float error can't turn an intended 1.0 into
        // 0.999… and floor it away), then split it across the earlier swings with integer division.
        int leadingPool = Mathf.RoundToInt(totalYield * (1f - finalShare));
        int perSwing = leadingPool / (neededHits - 1);
        return hitIndex < neededHits ? perSwing : totalYield - perSwing * (neededHits - 1);
    }

    // ── Wood cap (log pile visuals + hard cap) ───────────

    /// <summary>New wood total after depositing `add`, truncated so it never exceeds `cap`.
    /// A non-positive `add` leaves `current` unchanged.</summary>
    public static int ClampToCap(int current, int add, int cap)
    {
        if (add <= 0) return current;
        return Mathf.Min(current + add, cap);
    }

    /// <summary>True once wood is at or above the cap — used to refuse further chopping.</summary>
    public static bool IsAtCap(int current, int cap) => current >= cap;

    /// <summary>0..1 fill fraction for one of several equal-capacity log piles, filling in index
    /// order: pile 0 covers wood [0, capacityPerPile], pile 1 covers (capacityPerPile, 2x], etc.</summary>
    public static float PileFillFraction(int totalWood, int pileIndex, int capacityPerPile)
    {
        if (capacityPerPile <= 0) return 0f;
        int pileFloor = pileIndex * capacityPerPile;
        int fillWithinPile = Mathf.Clamp(totalWood - pileFloor, 0, capacityPerPile);
        return fillWithinPile / (float)capacityPerPile;
    }
}
