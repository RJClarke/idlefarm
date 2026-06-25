using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ThunderstormManager
///
/// Triggers and runs Thunderstorms at a wave-based cadence (default: every 25 waves).
/// Each storm runs 3 overlapping phases — Wind, Rain, Lightning — with flexible timing
/// that varies slightly each storm so they feel organic.
///
/// Storm Timeline (example, base durations):
///   |←──────────────── Wind (60s) ────────────────→|
///        |←──────── Rain (30s) ────────→|
///              |←── Lightning (10s) ──→|
///
/// Lightning Visual Setup:
///   1. Import ThunderEffects.png (Sprite Mode: Multiple, PPU: 21, Point, No Compression)
///   2. Slice in Sprite Editor: Grid By Cell Count, 5 columns × 1 row (or however many frames)
///   3. Drag all sliced frames into the Lightning Frames array on this component
///   The bolt pivot is bottom-center so it naturally extends upward from the struck tile.
/// </summary>
public class ThunderstormManager : MonoBehaviour
{
    public static ThunderstormManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────────────────────────

    [Header("Weather Configuration")]
    [SerializeField] private WeatherData weatherData;

    /// <summary>Waves between storms (from WeatherData); 25 fallback if unassigned. For the offline sim.</summary>
    public int StormWaveInterval => weatherData != null ? weatherData.stormWaveInterval : 25;

    /// <summary>Seconds between lightning strikes during a storm (from WeatherData); 8 fallback. For the offline sim.</summary>
    public float LightningStrikeInterval => weatherData != null ? weatherData.lightningStrikeInterval : 8f;

    [Header("Lightning Visual")]
    [Tooltip("Drag all sliced ThunderEffects sprite frames here in order.")]
    [SerializeField] private Sprite[] lightningFrames;

    [Tooltip("Frames per second for the lightning animation.")]
    [SerializeField] private float lightningFrameRate = 14f;

    [Tooltip("How tall the lightning bolt sprite appears in world units. " +
             "Should roughly match the distance from tile to top of screen.")]
    [SerializeField] private float lightningHeightUnits = 10f;

    [Tooltip("Sorting order for the lightning overlay. Should be above everything else.")]
    [SerializeField] private int lightningSortingOrder = 6000; // above the Y-sort entity band (~1000–3000)

    [Header("Master Switch")]
    [SerializeField] private bool weatherEnabled = true;

    [Header("Debug — Read Only")]
    [SerializeField] private bool stormActive     = false;
    [SerializeField] private bool windActive      = false;
    [SerializeField] private bool rainActive      = false;
    [SerializeField] private bool lightningActive = false;
    [SerializeField] private int  currentStormNumber = 0;
    [SerializeField] private int  lastStormWave   = -1;

    // ─────────────────────────────────────────────────────────────────────
    // Runtime
    // ─────────────────────────────────────────────────────────────────────

    private Coroutine stormCoroutine;
    private Coroutine monitorCoroutine;

    // Track in-flight lightning visuals so they can be force-cleared. StopAllCoroutines()
    // (run end / new run / weather toggle) kills LightningFlash mid-animation before its
    // Destroy(bolt) + tile-color restore runs, leaving a frozen bolt on screen.
    private readonly List<GameObject> activeBolts = new List<GameObject>();
    private readonly Dictionary<SpriteRenderer, Color> flashedTiles = new Dictionary<SpriteRenderer, Color>();

    // ─────────────────────────────────────────────────────────────────────
    // Unity
    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += OnRunStarted;
            RunManager.Instance.OnRunEnded   += OnRunEnded;
        }

        if (RainOverlayUI.Instance != null && weatherData != null)
            RainOverlayUI.Instance.Initialize(weatherData);
    }

    private void OnDestroy()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= OnRunStarted;
            RunManager.Instance.OnRunEnded   -= OnRunEnded;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Run Events
    // ─────────────────────────────────────────────────────────────────────

    private void OnRunStarted()
    {
        if (!weatherEnabled || weatherData == null) return;

        currentStormNumber = 0;
        lastStormWave      = -1;
        stormActive        = false;

        StopAllCoroutines();
        ClearLightningVisuals();

        if (RainOverlayUI.Instance != null)
        {
            RainOverlayUI.Instance.Initialize(weatherData);
            RainOverlayUI.Instance.ForceHide();
        }

        monitorCoroutine = StartCoroutine(WaveMonitorLoop());
    }

    private void OnRunEnded()
    {
        StopAllCoroutines();
        EndAllEffectsImmediately();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Wave Monitor
    // ─────────────────────────────────────────────────────────────────────

    private IEnumerator WaveMonitorLoop()
    {
        yield return new WaitForSeconds(weatherData.initialGracePeriod);

        while (true)
        {
            yield return new WaitForSeconds(5f);

            if (RunManager.Instance == null || !RunManager.Instance.IsRunActive) yield break;
            if (stormActive) continue;

            int currentWave     = GetCurrentWave();
            int stormAtThisWave = currentWave / weatherData.stormWaveInterval;

            if (stormAtThisWave > currentStormNumber && stormAtThisWave > 0)
            {
                currentStormNumber = stormAtThisWave;
                lastStormWave      = currentWave;
                stormCoroutine     = StartCoroutine(RunThunderstorm(currentStormNumber));
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Storm Sequence
    // ─────────────────────────────────────────────────────────────────────

    private IEnumerator RunThunderstorm(int stormNumber)
    {
        stormActive = true;

        float windDuration = weatherData.GetWindDuration(stormNumber);

        float rainOffset      = weatherData.rainLeadOffset      + Random.Range(-weatherData.timingJitter, weatherData.timingJitter);
        float lightningOffset = weatherData.lightningLeadOffset + Random.Range(-weatherData.timingJitter * 0.5f, weatherData.timingJitter * 0.5f);

        rainOffset      = Mathf.Max(0f, rainOffset);
        lightningOffset = Mathf.Max(0f, lightningOffset);

        float rainDuration      = Mathf.Max(5f, windDuration - (rainOffset * 2f));
        float lightningDuration = Mathf.Max(3f, rainDuration  - (lightningOffset * 2f));

        // First storm is a gentle intro: shrink the lightning phase (→ fewer, less frequent strikes).
        if (stormNumber <= 1)
            lightningDuration = Mathf.Max(3f, lightningDuration * weatherData.firstStormLightningScale);

        Coroutine windCo      = StartCoroutine(WindPhase(windDuration, stormNumber));
        Coroutine rainCo      = StartCoroutine(DelayedRainPhase(rainOffset, rainDuration, stormNumber));
        Coroutine lightningCo = StartCoroutine(DelayedLightningPhase(rainOffset + lightningOffset, lightningDuration, stormNumber));

        yield return windCo;
        yield return rainCo;
        yield return lightningCo;

        stormActive = false;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Wind Phase
    // ─────────────────────────────────────────────────────────────────────

    private IEnumerator WindPhase(float duration, int stormNumber)
    {
        windActive = true;
        float damage   = weatherData.GetWindDamage(stormNumber);
        float elapsed  = 0f;
        float nextTick = weatherData.windTickInterval;

        while (elapsed < duration)
        {
            float dt  = Time.deltaTime;
            elapsed  += dt;
            nextTick -= dt;

            if (nextTick <= 0f)
            {
                nextTick = weatherData.windTickInterval;
                ApplyWindDamage(damage);
            }

            yield return null;
        }

        windActive = false;
    }

    private void ApplyWindDamage(float damage)
    {
        if (FarmGrid.Instance == null) return;

        float effective = damage * (1f - ResearchReduction());

        foreach (SoilTile tile in FarmGrid.Instance.GetOccupiedTiles())
        {
            if (tile.CurrentPlant == null) continue;
            Plant plant = tile.CurrentPlant.GetComponent<Plant>();
            if (plant != null)
                plant.TakeDamage(effective);
        }
    }

    private static float ResearchReduction()
    {
        if (ResearchManager.Instance == null) return 0f;
        return Mathf.Clamp01(ResearchManager.Instance.GetBonus(Research.StatKey.StormDamageReduction));
    }

    // ─────────────────────────────────────────────────────────────────────
    // Rain Phase
    // ─────────────────────────────────────────────────────────────────────

    private IEnumerator DelayedRainPhase(float delay, float duration, int stormNumber)
    {
        yield return new WaitForSeconds(delay);
        yield return StartCoroutine(RainPhase(duration, stormNumber));
    }

    private IEnumerator RainPhase(float duration, int stormNumber)
    {
        rainActive = true;
        float moisture = weatherData.GetRainMoisture(stormNumber);
        float elapsed  = 0f;
        float nextTick = weatherData.rainTickInterval;

        if (RainOverlayUI.Instance != null)
            RainOverlayUI.Instance.FadeIn();

        while (elapsed < duration)
        {
            float dt  = Time.deltaTime;
            elapsed  += dt;
            nextTick -= dt;

            if (nextTick <= 0f)
            {
                nextTick = weatherData.rainTickInterval;
                ApplyRainMoisture(moisture);
            }

            yield return null;
        }

        if (RainOverlayUI.Instance != null)
            RainOverlayUI.Instance.FadeOut();

        rainActive = false;
    }

    private void ApplyRainMoisture(float amount)
    {
        if (FarmGrid.Instance == null) return;

        float effective = amount;
        if (ResearchManager.Instance != null)
            effective *= 1f + ResearchManager.Instance.GetBonus(Research.StatKey.RainWatering);

        foreach (SoilTile tile in FarmGrid.Instance.GetOccupiedTiles())
        {
            if (tile.CurrentPlant == null) continue;
            Plant plant = tile.CurrentPlant.GetComponent<Plant>();
            plant?.ApplyRain(effective);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Lightning Phase
    // ─────────────────────────────────────────────────────────────────────

    private IEnumerator DelayedLightningPhase(float delay, float duration, int stormNumber)
    {
        yield return new WaitForSeconds(delay);
        yield return StartCoroutine(LightningPhase(duration, stormNumber));
    }

    private IEnumerator LightningPhase(float duration, int stormNumber)
    {
        lightningActive = true;

        int   totalStrikes = weatherData.GetLightningStrikeCount(stormNumber, duration);
        float interval     = duration / Mathf.Max(1, totalStrikes);

        for (int i = 0; i < totalStrikes; i++)
        {
            yield return new WaitForSeconds(interval);

            if (RunManager.Instance == null || !RunManager.Instance.IsRunActive) break;

            StrikeLightning(stormNumber);
        }

        lightningActive = false;
    }

    private void StrikeLightning(int stormNumber)
    {
        if (FarmGrid.Instance == null) return;

        List<SoilTile> candidates = FarmGrid.Instance.GetAllUnlockedTiles();
        if (candidates.Count == 0) return;

        SoilTile target = candidates[Random.Range(0, candidates.Count)];

        StartCoroutine(LightningFlash(target.transform.position, target));

        if (target.IsOccupied && target.CurrentPlant != null)
        {
            Plant plant  = target.CurrentPlant.GetComponent<Plant>();
            float damage = weatherData.GetLightningDamage(stormNumber);
            damage *= 1f - ResearchReduction();

            if (plant != null)
            {
                plant.TakeDamage(damage, "lightning");
                Debug.Log($"[Lightning] ⚡ Strike on {plant.CropData?.cropName} ({plant.CurrentStage}) — {damage:F0} HP!");
            }
        }
    }

    /// <summary>
    /// Plays the lightning bolt animation above the struck tile, then fades the tile flash out.
    ///
    /// Positioning logic:
    ///   - Sprite pivot is bottom-center, placed at the tile's world position
    ///   - The bolt graphic extends naturally upward toward the top of screen
    ///   - lightningHeightUnits controls the vertical scale in world space
    ///
    /// If no lightningFrames are assigned, falls back to the original yellow square placeholder.
    /// </summary>
    private IEnumerator LightningFlash(Vector3 worldPos, SoilTile tile)
    {
        // ── Tile color flash ──────────────────────────────────────────────
        SpriteRenderer tileSR       = tile != null ? tile.GetComponent<SpriteRenderer>() : null;
        Color          originalColor = tileSR != null ? tileSR.color : Color.white;

        if (tileSR != null)
            tileSR.color = new Color(1f, 1f, 0.5f, 1f);

        // ── Spawn bolt GameObject ─────────────────────────────────────────
        GameObject bolt = new GameObject("LightningBolt");
        SpriteRenderer sr = bolt.AddComponent<SpriteRenderer>();
        sr.sortingOrder = lightningSortingOrder;

        // Register for force-cleanup in case a coroutine stop interrupts us mid-animation.
        activeBolts.Add(bolt);
        if (tileSR != null && !flashedTiles.ContainsKey(tileSR))
            flashedTiles[tileSR] = originalColor;

        if (lightningFrames != null && lightningFrames.Length > 0)
        {
            // ── Real sprite animation ─────────────────────────────────────
            // Position: bottom of sprite sits at tile position, bolt extends upward
            // We achieve bottom-pivot positioning by offsetting Y by half the world height
            bolt.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
            bolt.transform.localScale = Vector3.one;

            float frameDuration = 1f / lightningFrameRate;
            float animDuration  = frameDuration * lightningFrames.Length;

            // Play each frame
            for (int i = 0; i < lightningFrames.Length; i++)
            {
                sr.sprite = lightningFrames[i];

                // Scale sprite to fill lightningHeightUnits in world space
                // We do this after setting the sprite so we know its native size
                if (sr.sprite != null)
                {
                    float nativeHeight = sr.sprite.bounds.size.y;
                    if (nativeHeight > 0f)
                    {
                        float scaleY = lightningHeightUnits / nativeHeight;
                        bolt.transform.localScale = new Vector3(scaleY, scaleY, 1f); // uniform scale to keep aspect
                    }
                }

                yield return new WaitForSeconds(frameDuration);
            }

            // Quick fade out after animation completes
            float fadeDuration = 0.15f;
            float elapsed      = 0f;
            Color startColor   = new Color(1f, 1f, 1f, 1f);

            while (elapsed < fadeDuration)
            {
                elapsed     += Time.deltaTime;
                float t      = elapsed / fadeDuration;
                sr.color     = Color.Lerp(startColor, Color.clear, t);

                if (tileSR != null)
                    tileSR.color = Color.Lerp(new Color(1f, 1f, 0.5f, 1f), originalColor, t);

                yield return null;
            }
        }
        else
        {
            // ── Placeholder fallback (original yellow square) ─────────────
            bolt.transform.position = worldPos;

            const int size   = 8;
            Texture2D tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[]   pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(1f, 1f, 0.3f, 1f);
            tex.SetPixels(pixels);
            tex.Apply();

            float ppu = size / 0.6f;
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, ppu);

            float duration = 0.3f;
            float elapsed  = 0f;

            while (elapsed < duration)
            {
                elapsed     += Time.deltaTime;
                float t      = elapsed / duration;
                Color c      = sr.color;
                c.a          = Mathf.Lerp(1f, 0f, t);
                sr.color     = c;

                if (tileSR != null)
                    tileSR.color = Color.Lerp(new Color(1f, 1f, 0.5f, 1f), originalColor, t);

                yield return null;
            }
        }

        // Restore tile color exactly and clean up
        if (tileSR != null)
        {
            tileSR.color = originalColor;
            flashedTiles.Remove(tileSR);
        }

        activeBolts.Remove(bolt);
        Destroy(bolt);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    private int GetCurrentWave()
    {
        if (ThreatWaveManager.Instance != null)
            return ThreatWaveManager.Instance.CurrentWave;

        if (RunManager.Instance == null) return 1;
        return Mathf.Max(1, Mathf.FloorToInt(RunManager.Instance.CurrentRunDuration / 60f) + 1);
    }

    private void EndAllEffectsImmediately()
    {
        stormActive     = false;
        windActive      = false;
        rainActive      = false;
        lightningActive = false;

        ClearLightningVisuals();

        if (RainOverlayUI.Instance != null)
            RainOverlayUI.Instance.ForceHide();
    }

    /// <summary>Destroy any frozen lightning bolts and restore any tiles still mid-flash.</summary>
    private void ClearLightningVisuals()
    {
        foreach (GameObject b in activeBolts)
            if (b != null) Destroy(b);
        activeBolts.Clear();

        foreach (var kv in flashedTiles)
            if (kv.Key != null) kv.Key.color = kv.Value;
        flashedTiles.Clear();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────

    public bool IsStormActive     => stormActive;
    public bool IsWindActive      => windActive;
    public bool IsRainActive      => rainActive;
    public bool IsLightningActive => lightningActive;

    public void SetWeatherEnabled(bool enabled)
    {
        weatherEnabled = enabled;
        if (!enabled) { StopAllCoroutines(); EndAllEffectsImmediately(); }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Editor Debug
    // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("Debug: Force Thunderstorm (Storm 1)")]
    private void DebugStorm1()
    {
        if (weatherData == null) { Debug.LogWarning("No WeatherData assigned!"); return; }
        if (stormActive) { Debug.LogWarning("Storm already active!"); return; }
        StartCoroutine(RunThunderstorm(1));
    }

    [ContextMenu("Debug: Force Thunderstorm (Storm 5)")]
    private void DebugStorm5()
    {
        if (weatherData == null) { Debug.LogWarning("No WeatherData assigned!"); return; }
        if (stormActive) { Debug.LogWarning("Storm already active!"); return; }
        StartCoroutine(RunThunderstorm(5));
    }

    [ContextMenu("Debug: Force Single Strike")]
    private void DebugSingleStrike() => StrikeLightning(1);

    [ContextMenu("Debug: End Storm Immediately")]
    private void DebugEndStorm() => EndAllEffectsImmediately();
#endif
}