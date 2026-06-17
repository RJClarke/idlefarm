using UnityEngine;

/// <summary>
/// Pure helpers that turn equipment ownership + research effectiveness bonuses into 0..1 loss
/// reductions for the offline simulator, and combine independent sources without ever exceeding 1.
/// </summary>
public static class OfflineMitigation
{
    /// <summary>
    /// Reduction from one source: 0 if absent, else baseReduction scaled by its effectiveness bonus,
    /// clamped to [0,1]. Negative bonuses are treated as 0 (never below base).
    /// </summary>
    public static float Reduction(bool present, float baseReduction, float effectivenessBonus)
        => present ? Mathf.Clamp01(baseReduction * (1f + Mathf.Max(0f, effectivenessBonus))) : 0f;

    /// <summary>Combine two independent reductions via complement product: 1 - (1-a)(1-b). Order-free, &lt;= 1.</summary>
    public static float Stack(float a, float b)
        => 1f - (1f - Mathf.Clamp01(a)) * (1f - Mathf.Clamp01(b));
}
