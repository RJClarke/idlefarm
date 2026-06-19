using System;
using UnityEngine;

/// <summary>
/// Pure, Unity-light cost/scaling math for the Farm Upgrades system. No singletons, no
/// state — so it's unit-testable. Lives in IdleFarm.EconomyCore alongside SeedEconomy.
///
/// Scaling model (per the design): a gentle geometric base curve plus milestone
/// "breakpoint" multipliers. The geometric base keeps early levels cheap and incremental;
/// the breakpoints add periodic jumps so high-cap tracks can't be maxed early.
///
///   cost(level) = baseCost * growth^(level-1) * breakpointMult^floor((level-1)/breakpointEvery)
///
/// level is 1-indexed and means "the cost to reach this level from the one below it".
/// </summary>
public static class FarmUpgradeMath
{
    /// <summary>
    /// Cost to purchase <paramref name="level"/> (1-indexed). Returns a long because high-cap
    /// tracks can exceed int range late; callers clamp to int when actually spending (a cost
    /// beyond the currency's int range is simply unaffordable, which reinforces the soft cap).
    /// </summary>
    public static long Cost(long baseCost, int level, float growthPerLevel,
                            int breakpointEvery, float breakpointMultiplier)
    {
        if (level <= 0 || baseCost <= 0) return 0;

        double g = Mathf.Max(1f, growthPerLevel);
        double cost = baseCost * Math.Pow(g, level - 1);

        if (breakpointEvery > 0 && breakpointMultiplier > 1f)
        {
            int breakpointsPassed = (level - 1) / breakpointEvery;
            cost *= Math.Pow(breakpointMultiplier, breakpointsPassed);
        }

        if (double.IsInfinity(cost) || cost >= long.MaxValue) return long.MaxValue;
        return (long)Math.Max(1.0, Math.Round(cost));
    }

    /// <summary>Clamp a (possibly huge) long cost into int range for spending against int currencies.</summary>
    public static int ClampToInt(long cost)
    {
        if (cost >= int.MaxValue) return int.MaxValue;
        if (cost <= 0) return 0;
        return (int)cost;
    }

    /// <summary>Additive bonus an effect provides at a given level (level * perLevel, floored at 0).</summary>
    public static float Bonus(int level, float bonusPerLevel)
    {
        return Mathf.Max(0, level) * bonusPerLevel;
    }
}
