using UnityEngine;

/// <summary>
/// Pure decision logic for fishing (spec §3a). No Unity object dependencies so it is fully
/// unit-testable; FishingManager injects UnityEngine.Random values as rand01 arguments.
/// </summary>
public static class FishingMath
{
    /// <summary>
    /// Time until the next bite, spread 0.5×–1.5× around the average so bites feel organic while
    /// staying bounded (spec §3a: ~20 min average, throughput capped by real time).
    /// </summary>
    public static double RollBiteSeconds(double avgSeconds, float rand01)
        => avgSeconds * (0.5 + Mathf.Clamp01(rand01));

    /// <summary>
    /// Weighted pick of a fish tier (1-based) from relative weights, using a 0..1 roll. Higher tiers
    /// sit at the tail of the cumulative distribution, so only a high roll lands a rare fish. A
    /// null/empty weight set falls back to tier 1.
    /// </summary>
    public static int RollFishTier(float[] weights, float rand01)
    {
        if (weights == null || weights.Length == 0) return 1;
        float total = 0f;
        for (int i = 0; i < weights.Length; i++) total += Mathf.Max(0f, weights[i]);
        if (total <= 0f) return 1;

        float target = Mathf.Clamp01(rand01) * total;
        float cumulative = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += Mathf.Max(0f, weights[i]);
            if (target < cumulative) return i + 1;
        }
        return weights.Length; // rand01 == 1 → last tier
    }

    /// <summary>
    /// Reel taps to bring a cast back to shore: scales linearly with cast power so a long cast is
    /// more work than a short one (spec — reel effort scales with distance). Clamped to
    /// [minTaps, maxTaps] and never below 1, so a line is always retrievable.
    /// </summary>
    public static int ReelTapsForPower(int minTaps, int maxTaps, float power01)
    {
        int taps = Mathf.RoundToInt(Mathf.Lerp(minTaps, maxTaps, Mathf.Clamp01(power01)));
        return Mathf.Max(1, taps);
    }

    /// <summary>True when p lies within radius of center — used to test whether the bobber sits
    /// inside a whirlpool. Boundary counts as inside.</summary>
    public static bool PointInCircle(Vector2 center, float radius, Vector2 p)
        => (p - center).sqrMagnitude <= radius * radius;

    /// <summary>First pole is Coins-only (you can't fish without one — same chicken-and-egg as the axe).</summary>
    public static bool CanBuyPole(bool hasPole, int coins, int coinCost) => !hasPole && coins >= coinCost;

    /// <summary>Pole upgrade allowed: under max level and both currencies affordable.</summary>
    public static bool CanUpgradePole(int poleLevel, int maxLevel, int coins, int coinCost, int wood, int woodCost)
    {
        if (poleLevel >= maxLevel) return false;
        return coins >= coinCost && wood >= woodCost;
    }
}
