using UnityEngine;

/// <summary>
/// Pure, stateless weather math (no Unity objects beyond Mathf) — unit-testable, lives in
/// IdleFarm.EconomyCore. Drives the WeatherController's blending + scheduling + the rain angle.
/// </summary>
public static class WeatherMath
{
    /// <summary>MoveTowards ease of one channel; never overshoots.</summary>
    public static float EaseChannel(float current, float target, float dt, float speed)
        => Mathf.MoveTowards(current, target, Mathf.Max(0f, dt) * Mathf.Max(0f, speed));

    /// <summary>Storm number -> 0..1 severity. stormsToMax storms reach full severity.</summary>
    public static float StormSeverity(int stormNumber, float stormsToMax)
        => Mathf.Clamp01(stormNumber / Mathf.Max(1f, stormsToMax));

    /// <summary>
    /// Rain fall angle from vertical (degrees). Severity is the main driver; the wind term is kept in
    /// the signature for future tuning but currently contributes 0 (wind sets DIRECTION + horizontal
    /// speed at the consumer). 0 = straight down, maxAngleDeg = near-horizontal (high-severity storms).
    /// </summary>
    public static float RainAngleDegrees(float wind, float severity, float maxAngleDeg)
        => maxAngleDeg * Mathf.Clamp01(Mathf.Clamp01(severity) + 0.2f * Mathf.Clamp01(wind) * 0f);

    /// <summary>
    /// Leaf/debris travel angle from vertical (degrees). In dry air leaves ride the breeze and lie
    /// down toward horizontal as the wind rises; once it is raining they are pulled back toward the
    /// rain's own angle (plus a small offset, since leaves catch more wind than water does) so the
    /// two layers read as one storm instead of two unrelated effects.
    /// </summary>
    public static float DebrisAngleDegrees(float wind, float precipitation, float rainAngleDeg,
                                           float calmDeg, float windyDeg, float rainMatchOffsetDeg)
    {
        float dry = Mathf.Lerp(calmDeg, windyDeg, Mathf.Clamp01(wind));
        float wet = Mathf.Clamp(rainAngleDeg + rainMatchOffsetDeg, 0f, 89f);
        return Mathf.Lerp(dry, wet, Mathf.Clamp01(precipitation));
    }

    /// <summary>
    /// Leaf/debris travel speed (world units/sec). Rises with wind, but is damped while rain is
    /// falling — gusting debris belongs to the build-up and the tail of a storm, not the downpour.
    /// </summary>
    public static float DebrisSpeed(float wind, float precipitation,
                                    float calmSpeed, float windySpeed, float rainSpeedMul)
    {
        float s = Mathf.Lerp(calmSpeed, windySpeed, Mathf.Clamp01(wind));
        return s * Mathf.Lerp(1f, Mathf.Max(0f, rainSpeedMul), Mathf.Clamp01(precipitation));
    }

    /// <summary>Pick a wind direction from a 0..1 roll. Returns -1 (blows left) or +1 (blows right).</summary>
    public static float RollWindDirection(float rng01, float leftChance)
        => Mathf.Clamp01(rng01) < Mathf.Clamp01(leftChance) ? -1f : 1f;

    /// <summary>
    /// Alpha of the darkening storm overlay. Scales with how hard it is raining, and a heavier
    /// storm (severity) drops the light further still. Clear weather is always 0.
    /// </summary>
    public static float StormDarkness(float precipitation, float severity, float maxAlpha, float severityBoost)
    {
        float p = Mathf.Clamp01(precipitation);
        return Mathf.Clamp01(p * Mathf.Max(0f, maxAlpha)
                           + p * Mathf.Clamp01(severity) * Mathf.Max(0f, severityBoost));
    }

    /// <summary>Weighted pick of a casual mood from a 0..1 roll. Returns CasualWeather as int.</summary>
    public static int RollCasual(float rng01, float clearWeight, float cloudyWeight, float windyWeight)
    {
        float c  = Mathf.Max(0f, clearWeight);
        float cl = Mathf.Max(0f, cloudyWeight);
        float w  = Mathf.Max(0f, windyWeight);
        float total = Mathf.Max(1e-5f, c + cl + w);
        float r = Mathf.Clamp01(rng01) * total;
        if (r < c) return 0;       // Clear
        if (r < c + cl) return 1;  // Cloudy
        return 2;                  // Windy
    }
}
