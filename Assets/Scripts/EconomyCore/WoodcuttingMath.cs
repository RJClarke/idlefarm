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

    /// <summary>Wood from felling at a given stage: only a portion if cut early, full yield at the last stage.</summary>
    public static int StageYield(int fullYield, int stageIndex, int stageCount)
    {
        if (stageCount <= 0) return Mathf.Max(0, fullYield);
        int stage = Mathf.Clamp(stageIndex, 0, stageCount - 1);
        return Mathf.RoundToInt(Mathf.Max(0, fullYield) * (stage + 1) / (float)stageCount);
    }

    /// <summary>Taps to fell scaled by stage — saplings fall fast, full trees take the full count (min 1).</summary>
    public static int StageHits(int fullHits, int stageIndex, int stageCount)
    {
        if (stageCount <= 0) return Mathf.Max(1, fullHits);
        int stage = Mathf.Clamp(stageIndex, 0, stageCount - 1);
        return Mathf.Max(1, Mathf.RoundToInt(fullHits * (stage + 1) / (float)stageCount));
    }
}
