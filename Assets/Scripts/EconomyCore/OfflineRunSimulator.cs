using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deterministic, cause-attributed offline run simulator. Pure: no singletons, no scene access,
/// no RNG. Replays the away-window in fixed farm-time ticks and returns an untaxed OfflineRunResult.
/// Tax is applied by the caller via OfflineTax. See spec 2026-06-16.
/// </summary>
public static class OfflineRunSimulator
{
    // ---- deterministic wave/storm context (mirror ThreatWaveManager / WeatherData) ----

    public static int WaveAt(float farmSeconds, OfflineSimTuning t)
        => Mathf.Max(1, Mathf.FloorToInt(farmSeconds / t.waveIntervalSeconds) + 1);

    public static int DeerCount(int wave, OfflineSimTuning t)
    {
        if (wave < t.deerStartWave) return 0;
        int c = Mathf.FloorToInt((float)(wave - t.deerStartWave) / t.deerCountInterval) + 1;
        return Mathf.Min(c, t.maxDeer);
    }

    public static int CrowCount(int wave, OfflineSimTuning t)
    {
        if (wave < t.crowStartWave) return 0;
        int c = Mathf.FloorToInt((float)(wave - t.crowStartWave) / t.crowCountInterval) + 1;
        return Mathf.Min(c, t.maxCrows);
    }

    public static float HungerMult(int wave, OfflineSimTuning t)
        => 1f + (wave - 1) * t.hungerScalePerWave;

    /// <summary>
    /// A storm fires when the run crosses each multiple of stormWaveInterval waves. We model its
    /// lightning phase as a fixed window starting at that wave boundary's farm-time.
    /// </summary>
    public static bool LightningActiveAt(float farmSeconds, OfflineSimTuning t)
    {
        int wave = WaveAt(farmSeconds, t);
        int stormNumber = wave / t.stormWaveInterval;
        if (stormNumber <= 0) return false;
        float stormStart = stormNumber * t.stormWaveInterval * t.waveIntervalSeconds;
        return farmSeconds >= stormStart && farmSeconds < stormStart + t.stormLightningPhaseSeconds;
    }

    // ---- main entry (implemented in Task 5) ----
    public static OfflineRunResult Simulate(OfflineSimContext ctx)
    {
        return new OfflineRunResult { finalFarmSeconds = ctx.startFarmSeconds };
    }
}
