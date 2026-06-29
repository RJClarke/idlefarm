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
