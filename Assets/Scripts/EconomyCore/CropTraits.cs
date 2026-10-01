using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plain-language reading of a crop's numbers for the Almanac: derived tags (never hand-set, so
/// they can't disagree with the data) and words for how much a pest wants the crop.
/// Thresholds are the spec's (docs/superpowers/specs/2026-09-28-farmers-almanac-design.md).
/// </summary>
public static class CropTraits
{
    public const float QuickGrowSeconds = 150f;
    public const float SlowGrowSeconds = 330f;
    public const int SturdyHp = 90;
    public const int FragileHp = 70;
    public const float ThirstyAt = 1.2f;
    public const float DroughtHardyAt = 0.85f;
    public const float LovedAt = 1.25f;
    public const float AvoidedAt = 0.6f;

    /// <summary>Seconds from a regrowing crop's harvest to its next one. A harvested regrower
    /// restarts at the sapling stage (it never goes back to seed); an authored regrowSeconds is
    /// the whole wait, otherwise the sapling stage's own length. 0 for crops that don't regrow.</summary>
    public static float RegrowSeconds(bool canRegrow, float regrowSeconds, float saplingSeconds)
    {
        if (!canRegrow) return 0f;
        return regrowSeconds > 0f ? regrowSeconds : saplingSeconds;
    }

    public static string AppetiteWord(float appetite)
    {
        if (appetite <= 0.3f) return "won't touch it";
        if (appetite <= 0.6f) return "avoids it";
        if (appetite <= 0.9f) return "nibbles";
        if (appetite <= 1.1f) return "normal";
        if (appetite <= 1.4f) return "likes it";
        return "loves it";
    }

    /// <summary>Bar fill for an appetite multiplier (0-2 maps to 0-1; normal = half full).</summary>
    public static float AppetiteFill(float appetite) => Mathf.Clamp01(appetite / 2f);
}
