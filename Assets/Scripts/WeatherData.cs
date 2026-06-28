using UnityEngine;

/// <summary>
/// ScriptableObject that configures all weather effect values for a Thunderstorm.
/// Create via: Right-click in Project → Create → Farm Game → Weather Data
///
/// All timing and damage values are exposed as sliders for easy tuning.
/// Storm scaling (duration, intensity) increases with each storm that occurs
/// during a run, at a cadence of every N waves (default 25).
/// </summary>
[CreateAssetMenu(fileName = "ThunderstormData", menuName = "Farm Game/Weather Data", order = 6)]
public class WeatherData : ScriptableObject
{
    // ─────────────────────────────────────────────────────────────────────
    // Storm Triggering
    // ─────────────────────────────────────────────────────────────────────

    [Header("Storm Trigger (Wave-Based)")]
    [Tooltip("A storm triggers every N waves. Default 25 means storms at wave 25, 50, 75...")]
    [Range(5, 100)]
    public int stormWaveInterval = 25;

    [Tooltip("Grace period (seconds) at the start of a run before any storm can trigger.")]
    [Range(0f, 120f)]
    public float initialGracePeriod = 30f;

    // ─────────────────────────────────────────────────────────────────────
    // Storm Structure & Scaling
    // ─────────────────────────────────────────────────────────────────────

    [Header("Storm Duration")]
    [Tooltip("Base duration of the WIND phase (seconds). Wind is the outermost layer.")]
    [Range(10f, 1500f)]
    public float baseWindDuration = 60f;

    [Tooltip("How many seconds after Wind starts that Rain begins. " +
             "Rain ends the same amount of time before Wind ends.")]
    [Range(0f, 600f)]
    public float rainLeadOffset = 15f;

    [Tooltip("How many seconds after Rain starts that Lightning begins. " +
             "Lightning ends the same amount of time before Rain ends.")]
    [Range(0f, 400f)]
    public float lightningLeadOffset = 10f;

    [Tooltip("Random jitter (±seconds) applied to Rain and Lightning start/end times each storm. " +
             "Keeps storms feeling varied.")]
    [Range(0f, 15f)]
    public float timingJitter = 5f;

    [Header("Storm Scaling (per storm number)")]
    [Tooltip("How much longer each storm is than the previous one. " +
             "0.1 = +10% duration per storm. Storm 1 = 1.0x, Storm 2 = 1.1x, etc.")]
    [Range(0f, 0.5f)]
    public float durationScalePerStorm = 0.1f;

    // ─────────────────────────────────────────────────────────────────────
    // Rain Settings
    // ─────────────────────────────────────────────────────────────────────

    [Header("Rain — Moisture Restore")]
    [Tooltip("Moisture added to ALL plants per tick during rain.")]
    [Range(0.1f, 10f)]
    public float rainMoisturePerTick = 1f;

    [Tooltip("How often rain applies moisture (seconds between ticks).")]
    [Range(0.5f, 10f)]
    public float rainTickInterval = 2f;

    [Tooltip("Intensity multiplier applied to rainMoisturePerTick per storm number. " +
             "0.05 = +5% per storm.")]
    [Range(0f, 0.3f)]
    public float rainIntensityScalePerStorm = 0.05f;

    // ─────────────────────────────────────────────────────────────────────
    // Wind Settings
    // ─────────────────────────────────────────────────────────────────────

    [Header("Wind — HP Damage")]
    [Tooltip("HP removed from ALL plants per tick during wind.")]
    [Range(0.1f, 20f)]
    public float windDamagePerTick = 1f;

    [Tooltip("How often wind applies damage (seconds between ticks).")]
    [Range(1f, 30f)]
    public float windTickInterval = 5f;

    [Tooltip("Damage multiplier applied to windDamagePerTick per storm number. " +
             "0.05 = +5% per storm.")]
    [Range(0f, 0.3f)]
    public float windDamageScalePerStorm = 0.05f;

    // ─────────────────────────────────────────────────────────────────────
    // Lightning Settings
    // ─────────────────────────────────────────────────────────────────────

    [Header("Lightning — Random Strike")]
    [Tooltip("Minimum HP damage a lightning strike deals to a plant on the hit tile.")]
    [Range(1f, 200f)]
    public float lightningMinDamage = 80f;

    [Tooltip("Maximum HP damage a lightning strike deals to a plant on the hit tile.")]
    [Range(1f, 200f)]
    public float lightningMaxDamage = 100f;

    [Tooltip("Seconds between each lightning strike during the lightning phase.")]
    [Range(1f, 30f)]
    public float lightningStrikeInterval = 8f;

    [Tooltip("How many extra strikes are added per storm number. " +
             "0.5 = one extra strike every 2 storms.")]
    [Range(0f, 3f)]
    public float lightningStrikeScalePerStorm = 0.5f;

    [Tooltip("Scales the FIRST storm's lightning-phase duration (and thus strike count) down, " +
             "so the intro storm is gentle. 0.5 = half as much lightning as it would otherwise have.")]
    [Range(0.1f, 1f)]
    public float firstStormLightningScale = 0.5f;

    [Tooltip("Damage multiplier applied to lightning damage per storm number. " +
             "0.05 = +5% per storm.")]
    [Range(0f, 0.3f)]
    public float lightningDamageScalePerStorm = 0.05f;

    // ─────────────────────────────────────────────────────────────────────
    // Rain Visual Settings
    // ─────────────────────────────────────────────────────────────────────

    [Header("Rain Visual — Overlay")]
    [Tooltip("Target alpha of the dark screen overlay when rain is fully active.")]
    [Range(0f, 0.8f)]
    public float rainOverlayMaxAlpha = 0.35f;

    [Tooltip("Color of the screen overlay during rain.")]
    public Color rainOverlayColor = new Color(0.05f, 0.1f, 0.25f, 1f); // Dark blue

    [Tooltip("Seconds for the overlay to fade in at rain start.")]
    [Range(0.5f, 10f)]
    public float rainFadeInDuration = 3f;

    [Tooltip("Seconds for the overlay to fade out at rain end.")]
    [Range(0.5f, 10f)]
    public float rainFadeOutDuration = 4f;

    [Header("Rain Visual — Particles")]
    [Tooltip("How many raindrop particles emit per second.")]
    [Range(10f, 500f)]
    public float rainParticleRate = 150f;

    // ─────────────────────────────────────────────────────────────────────
    // Ambient Atmosphere (cosmetic — cloud shadows + wind debris)
    // ─────────────────────────────────────────────────────────────────────

    [Header("Ambient Atmosphere — Master")]
    [Tooltip("Master switch for the always-on ambient cloud-shadow + wind-debris layer.")]
    public bool atmosphereEnabled = true;

    [Tooltip("How fast the storm intensity (0..1) eases in/out for the atmosphere layer.")]
    [Range(0.1f, 5f)]
    public float atmosphereStormLerpSpeed = 0.8f;

    [Header("Ambient — Cloud Shadows")]
    [Tooltip("Soft = blurred multiply patches; Dithered = pixel-native patches; TintDip = whole-scene pulse.")]
    public ShadowStyle shadowStyle = ShadowStyle.Dithered;

    [Tooltip("Max simultaneous drifting shadow patches (Soft/Dithered modes).")]
    [Range(0, 12)]
    public int shadowMaxPatches = 3;

    [Tooltip("Min/Max world-unit width of a shadow patch.")]
    public Vector2 shadowSizeRange = new Vector2(8f, 16f);

    [Tooltip("Peak opacity of a shadow patch at ambient intensity.")]
    [Range(0f, 1f)]
    public float shadowOpacity = 0.45f;

    [Tooltip("Base drift speed (world units/sec) at wind multiplier 1.")]
    [Range(0f, 10f)]
    public float shadowBaseDriftSpeed = 1.2f;

    [Tooltip("Drift direction sign: -1 = clouds blow left, +1 = blow right.")]
    public float windDriftDirection = -1f;

    [Tooltip("Extra drift-speed multiplier at full storm intensity.")]
    [Range(0f, 4f)]
    public float shadowStormSpeedMul = 1.5f;

    [Tooltip("Extra opacity multiplier at full storm intensity.")]
    [Range(0f, 3f)]
    public float shadowStormOpacityMul = 1.4f;

    [Header("Ambient — Cloud Shadows (TintDip style)")]
    [Tooltip("Color the whole scene dips toward when a cloud 'passes over the sun'.")]
    public Color tintDipColor = new Color(0.15f, 0.18f, 0.28f, 1f);

    [Tooltip("Peak alpha of the tint-dip overlay at ambient intensity.")]
    [Range(0f, 0.6f)]
    public float tintDipMaxAlpha = 0.18f;

    [Tooltip("Seconds for one full darken→brighten tint-dip cycle.")]
    [Range(4f, 60f)]
    public float tintDipPeriod = 14f;

    [Header("Ambient — Wind Debris (leaves)")]
    [Tooltip("Base leaf particles/second at wind multiplier 1.")]
    [Range(0f, 30f)]
    public float debrisBaseRate = 3f;

    [Tooltip("Leaf sprites to scatter (random per particle). Leave empty for a tiny generated speck.")]
    public Sprite[] debrisSprites;

    [Tooltip("Extra emission multiplier at full storm intensity.")]
    [Range(0f, 6f)]
    public float debrisStormRateMul = 3f;

    // ─────────────────────────────────────────────────────────────────────
    // Runtime Helpers (used by ThunderstormManager)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the duration scale multiplier for the given storm number (1-indexed).
    /// Storm 1 = 1.0x, Storm 2 = 1.0 + durationScalePerStorm, etc.
    /// </summary>
    public float GetDurationScale(int stormNumber)
        => 1f + (stormNumber - 1) * durationScalePerStorm;

    /// <summary>Scaled wind duration for the given storm number.</summary>
    public float GetWindDuration(int stormNumber)
        => baseWindDuration * GetDurationScale(stormNumber);

    /// <summary>Scaled moisture per rain tick for the given storm number.</summary>
    public float GetRainMoisture(int stormNumber)
        => rainMoisturePerTick * (1f + (stormNumber - 1) * rainIntensityScalePerStorm);

    /// <summary>Scaled wind damage per tick for the given storm number.</summary>
    public float GetWindDamage(int stormNumber)
        => windDamagePerTick * (1f + (stormNumber - 1) * windDamageScalePerStorm);

    /// <summary>Scaled lightning damage for the given storm number.</summary>
    public float GetLightningDamage(int stormNumber)
    {
        float scale = 1f + (stormNumber - 1) * lightningDamageScalePerStorm;
        return Random.Range(lightningMinDamage, lightningMaxDamage) * scale;
    }

    /// <summary>
    /// Total number of lightning strikes to deliver during the lightning phase.
    /// Minimum 1. Increases by lightningStrikeScalePerStorm per storm.
    /// </summary>
    public int GetLightningStrikeCount(int stormNumber, float lightningPhaseDuration)
    {
        // Base: one strike per strikeInterval, plus scaling bonus
        int baseStrikes = Mathf.Max(1, Mathf.FloorToInt(lightningPhaseDuration / lightningStrikeInterval));
        float bonus     = (stormNumber - 1) * lightningStrikeScalePerStorm;
        return Mathf.Max(1, Mathf.RoundToInt(baseStrikes + bonus));
    }

    private void OnValidate()
    {
        lightningMinDamage = Mathf.Min(lightningMinDamage, lightningMaxDamage);
    }
}