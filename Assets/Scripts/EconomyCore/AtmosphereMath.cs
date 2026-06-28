using UnityEngine;

/// <summary>Visual style for the ambient cloud-shadow layer.</summary>
public enum ShadowStyle { Soft, Dithered, TintDip }

/// <summary>
/// Pure, stateless math for the ambient atmosphere layer (cloud shadows + wind debris).
/// No singletons, no Unity objects — unit-testable. Lives in IdleFarm.EconomyCore.
///
/// Drift/emission scale off the global wind multiplier (WindController's _GlobalWindMul)
/// and a 0..1 storm "intensity" that ThunderstormManager ramps up during wind phases.
/// windDirX is a sign (-1 = blows left, +1 = blows right) configured on WeatherData.
/// </summary>
public static class AtmosphereMath
{
    /// <summary>MoveTowards-style ease of an intensity value, clamped so it never overshoots.</summary>
    public static float EaseIntensity(float current, float target, float dt, float lerpSpeed)
        => Mathf.MoveTowards(current, target, Mathf.Max(0f, dt) * Mathf.Max(0f, lerpSpeed));

    /// <summary>Patch/cloud drift speed: base * wind, boosted by storm intensity.</summary>
    public static float DriftSpeed(float baseSpeed, float windMul, float intensity, float stormSpeedMul)
        => baseSpeed * Mathf.Max(0f, windMul) * (1f + Mathf.Clamp01(intensity) * Mathf.Max(0f, stormSpeedMul));

    /// <summary>Debris particles/second: base, boosted by storm intensity (wind already in particle vel).</summary>
    public static float EmissionRate(float baseRate, float windMul, float intensity, float stormRateMul)
        => baseRate * Mathf.Max(0f, windMul) * (1f + Mathf.Clamp01(intensity) * Mathf.Max(0f, stormRateMul));

    /// <summary>
    /// Signed horizontal velocity of a drifting patch. Clouds move in the wind direction:
    /// windDirX -1 → negative (drifts left), +1 → positive (drifts right). This is the value
    /// a patch's x position should change by per second (× dt at the call site).
    /// </summary>
    public static float PatchVelocityX(float speed, float windDirX)
        => (windDirX < 0f ? -1f : 1f) * Mathf.Max(0f, speed);

    /// <summary>World X just off the UPWIND screen edge where a new patch should spawn.</summary>
    public static float SpawnEdgeX(float camX, float camHalfWidth, float patchHalfWidth, float windDirX)
    {
        // Wind blows toward sign(windDirX); patches enter from the opposite edge.
        float upwindSign = windDirX < 0f ? 1f : -1f; // blows left => enter from right (+)
        return camX + upwindSign * (camHalfWidth + patchHalfWidth);
    }

    /// <summary>True once a patch has drifted fully past the DOWNWIND screen edge.</summary>
    public static bool IsPatchOffscreen(float patchX, float patchHalfWidth, float camX, float camHalfWidth, float windDirX)
    {
        if (windDirX < 0f) // blows left: gone when its right side is past the left edge
            return patchX + patchHalfWidth < camX - camHalfWidth;
        return patchX - patchHalfWidth > camX + camHalfWidth; // blows right
    }
}
