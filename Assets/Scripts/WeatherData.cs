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
    public float atmosphereStormLerpSpeed = 0.87f;

    [Header("Ambient — Cloud Shadows")]
    [Tooltip("Soft = blurred multiply patches; Dithered = pixel-native patches; TintDip = whole-scene pulse.")]
    public ShadowStyle shadowStyle = ShadowStyle.Dithered;

    [Tooltip("Max simultaneous drifting shadow patches (Soft/Dithered modes).")]
    [Range(0, 12)]
    public int shadowMaxPatches = 12;

    [Tooltip("Min/Max world-unit width of a shadow patch.")]
    public Vector2 shadowSizeRange = new Vector2(32f, 80f);

    [Tooltip("Peak opacity of a shadow patch at ambient intensity.")]
    [Range(0f, 1f)]
    public float shadowOpacity = 0.169f;

    [Tooltip("Base drift speed (world units/sec) at wind multiplier 1.")]
    [Range(0f, 10f)]
    public float shadowBaseDriftSpeed = 1.2f;

    [Tooltip("Drift direction sign: -1 = clouds blow left, +1 = blow right.")]
    public float windDriftDirection = -1f;

    [Tooltip("Extra drift-speed multiplier at full storm intensity.")]
    [Range(0f, 4f)]
    public float shadowStormSpeedMul = 0.65f;

    [Tooltip("Extra opacity multiplier at full storm intensity.")]
    [Range(0f, 3f)]
    public float shadowStormOpacityMul = 1.69f;

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

    [Header("Ambient — Storm Wind Gusts (streaks at storm start)")]
    [Tooltip("Wind-streak sprites that sweep across at the start of a storm (e.g. the wide sub-sprites of 5 Wind/Wind1-4).")]
    public Sprite[] windStreakSprites;

    [Tooltip("How many gust streaks burst across when a storm begins.")]
    [Range(0, 12)]
    public int windGustBurstCount = 4;

    [Tooltip("Seconds between the occasional follow-up gusts during the early storm window.")]
    [Range(0.5f, 15f)]
    public float windGustInterval = 3.5f;

    [Tooltip("Only spawn follow-up gusts for this many seconds after a storm starts (the 'beginning').")]
    [Range(0f, 60f)]
    public float windGustWindow = 12f;

    [Tooltip("Gust travel speed (world units/sec) across the screen.")]
    [Range(2f, 80f)]
    public float windGustSpeed = 26f;

    [Tooltip("Min/Max scale multiplier applied to a gust sprite.")]
    public Vector2 windGustScaleRange = new Vector2(3f, 6f);

    [Tooltip("Seconds a gust lives (also its fade duration).")]
    [Range(0.3f, 6f)]
    public float windGustLife = 2.2f;

    [Tooltip("Peak opacity of a gust streak.")]
    [Range(0f, 1f)]
    public float windGustAlpha = 0.55f;

    // ─────────────────────────────────────────────────────────────────────
    // Weather Profiles + Scheduler (unified casual + storm system)
    // ─────────────────────────────────────────────────────────────────────

    [Header("Weather — Blend")]
    [Tooltip("How fast the live weather eases toward the active profile (per second).")]
    [Range(0.05f, 3f)] public float weatherBlendSpeed = 0.4f;

    [Header("Weather — Casual Profiles")]
    public WeatherProfile profileClear  = new WeatherProfile { name = "Clear",  severity = 0f,    wind = 0.02f, cloudiness = 0.05f, precipitation = 0f };
    public WeatherProfile profileCloudy = new WeatherProfile { name = "Cloudy", severity = 0f,    wind = 0.12f, cloudiness = 0.7f,  precipitation = 0f };
    public WeatherProfile profileWindy  = new WeatherProfile { name = "Windy",  severity = 0.08f, wind = 0.65f, cloudiness = 0.45f, precipitation = 0f };

    [Header("Weather — Casual Scheduler (runs everywhere)")]
    [Tooltip("Random seconds between casual-weather rolls.")]
    public Vector2 casualRollInterval = new Vector2(120f, 240f);
    [Tooltip("Random seconds a casual mood lasts before easing back to Clear.")]
    public Vector2 casualEventDuration = new Vector2(20f, 40f);
    [Tooltip("Weight of rolling Clear (skip) in the casual roll.")]
    public float casualClearWeight = 2.5f;
    [Tooltip("Weight of rolling Cloudy in the casual roll.")]
    public float casualCloudyWeight = 2f;
    [Tooltip("Weight of rolling Windy in the casual roll.")]
    public float casualWindyWeight = 1.5f;

    [Header("Weather — Storm Scaling")]
    [Tooltip("Storm number that reaches full severity (1.0).")]
    [Range(1f, 10f)] public float stormsToMaxSeverity = 5f;
    [Tooltip("Storm cloudiness lerped by severity (min ~Storm 1 .. max at full severity).")]
    public Vector2 stormCloudiness = new Vector2(0.6f, 1f);
    [Tooltip("Storm wind lerped by severity.")]
    public Vector2 stormWind = new Vector2(0.35f, 1f);
    [Tooltip("Storm precipitation lerped by severity.")]
    public Vector2 stormPrecip = new Vector2(0.4f, 1f);

    [Header("Weather — Rain")]
    [Tooltip("Max rain fall angle from vertical (deg) at full severity. Near-horizontal late storms.")]
    [Range(10f, 85f)] public float rainMaxAngleDeg = 75f;

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