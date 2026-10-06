/// <summary>
/// Pure research-curve helpers (kept out of ResearchManager so the EditMode tests can reach them).
/// </summary>
public static class ResearchMath
{
    /// <summary>
    /// "Fast start" discount on a research's cost AND time. Levels 1..<paramref name="earlyLevels"/>
    /// cost <paramref name="earlyMultiplier"/>× the normal curve; the next <paramref name="earlyLevels"/>
    /// levels ease linearly back to full price, so there is no cliff when the cheap stretch ends.
    /// earlyLevels ≤ 0 (or a multiplier ≥ 1) = no discount.
    /// </summary>
    public static float EarlyLevelMultiplier(int level, int earlyLevels, float earlyMultiplier)
    {
        if (earlyLevels <= 0 || earlyMultiplier >= 1f) return 1f;
        if (earlyMultiplier < 0f) earlyMultiplier = 0f;
        if (level <= earlyLevels) return earlyMultiplier;
        if (level >= 2 * earlyLevels) return 1f;
        float t = (level - earlyLevels) / (float)earlyLevels;
        return earlyMultiplier + (1f - earlyMultiplier) * t;
    }

    /// <summary>Per-level bonus as the picker shows it: "%" stats are fractions (0.005 → "+0.5%"),
    /// any other unit is the raw amount (0.5 with "s" → "+0.5s").</summary>
    public static string FormatBonus(float amount, string unit)
    {
        if (string.IsNullOrEmpty(unit) || unit == "%")
            return "+" + (amount * 100f).ToString("0.##") + "%";
        return "+" + amount.ToString("0.##") + unit;
    }
}
