using System.Collections;
using UnityEngine;

/// <summary>
/// Always-on cosmetic atmosphere brain. Reads the global wind multiplier (WindController's
/// _GlobalWindMul) and storm state, eases a 0..1 storm "intensity", and drives the cloud-shadow
/// and wind-debris layers. Cosmetic only — never applies gameplay effects.
///
/// PLAY-MODE ONLY (intentionally not [ExecuteAlways]): ticking in edit mode spawned GameObjects
/// that accumulated across domain reloads (orphaned shadow patches, duplicate leaf emitters) and
/// made the scene drift while stopped. Tune via the WeatherData asset, then press Play to preview.
/// </summary>
public class AtmosphereController : MonoBehaviour
{
    [SerializeField] private WeatherData weatherData;

    [Tooltip("Debug only: repeatedly fire wind-gust bursts in play mode (to preview without a storm).")]
    [SerializeField] private bool debugAutoGustBurst = false;

    private static readonly int TimeID = Shader.PropertyToID("_WindTime");

    private CloudShadowLayer shadows;
    private TintDipLayer tintDip;
    private WindDebrisLayer debris;
    private StormGustLayer gusts;
    private ShadowStyle lastStyle;

    // Storm-gust timing
    private bool wasStorming;
    private float stormElapsed;
    private float gustTimer;

    private void OnEnable()
    {
        DestroyStrayChildren(); // belt-and-suspenders: clear any atmosphere objects saved into the scene
        shadows ??= new CloudShadowLayer(transform);
        tintDip ??= new TintDipLayer(transform);
        debris  ??= new WindDebrisLayer(transform);
        gusts   ??= new StormGustLayer(transform);
        Reconfigure();
    }

    /// <summary>Destroy any leftover layer-created children so we always start from a clean slate.</summary>
    private void DestroyStrayChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (c == null) continue;
            if (c.name == "CloudShadowPatch" || c.name == "WindDebris" || c.name == "TintDipCanvas" || c.name == "WindGust")
                Destroy(c.gameObject);
        }
    }

    private void OnDisable()
    {
        shadows?.Clear();
        tintDip?.Clear();
        debris?.Clear();
        gusts?.Clear();
    }

    private void Reconfigure()
    {
        if (weatherData == null) return;
        shadows.Configure(weatherData);
        tintDip.Configure(weatherData);
        debris.Configure(weatherData);
        gusts.Configure(weatherData);
        ApplyStyle();
    }

    private void ApplyStyle()
    {
        bool on = weatherData != null && weatherData.atmosphereEnabled;
        bool patches = on && weatherData.shadowStyle != ShadowStyle.TintDip;
        bool tint    = on && weatherData.shadowStyle == ShadowStyle.TintDip;
        shadows.SetActiveStyle(patches);
        tintDip.SetActive(tint);
        debris.SetActive(on);
        lastStyle = weatherData != null ? weatherData.shadowStyle : ShadowStyle.Soft;
    }

    private void Update()
    {
        if (weatherData == null) return;
        if (!weatherData.atmosphereEnabled) { OnDisable(); return; }

        // React to a style switch made live in the inspector.
        if (weatherData.shadowStyle != lastStyle) ApplyStyle();

        float wTime = Shader.GetGlobalFloat(TimeID);
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

        // Everything reads the shared, eased WeatherState (Clear -> all channels ~0).
        WeatherState s = (Application.isPlaying && WeatherController.Instance != null)
            ? WeatherController.Instance.State
            : default;

        Camera cam = Camera.main;
        if (weatherData.shadowStyle == ShadowStyle.TintDip)
            tintDip.Tick(wTime, s.cloudiness);
        else
            shadows.Tick(dt, s.cloudiness, s.wind, cam);

        debris.Tick(s.wind, cam);

        // Storm wind gusts: a burst when severity ramps up, then occasional gusts for the early window.
        bool storming = s.severity > 0.15f;
        if (storming && !wasStorming)
        {
            gusts.TriggerBurst(cam);
            stormElapsed = 0f;
            gustTimer = weatherData.windGustInterval;
        }
        else if (storming)
        {
            stormElapsed += dt;
            if (stormElapsed <= weatherData.windGustWindow)
            {
                gustTimer -= dt;
                if (gustTimer <= 0f) { gusts.SpawnOne(cam); gustTimer = weatherData.windGustInterval; }
            }
        }
        wasStorming = storming;

        gusts.Tick(dt, cam); // always tick so in-flight gusts finish + recycle
    }

    /// <summary>Spawn a gust burst now (for debugging / previewing without waiting for a storm).</summary>
    public void TriggerGustBurstNow() => gusts?.TriggerBurst(Camera.main);

    private void Start()
    {
        if (debugAutoGustBurst) StartCoroutine(DebugGustLoop());
    }

    private IEnumerator DebugGustLoop()
    {
        var wait = new WaitForSecondsRealtime(3f);
        while (true) { yield return wait; TriggerGustBurstNow(); }
    }

#if UNITY_EDITOR
    [ContextMenu("Atmosphere: Cycle Shadow Style")]
    private void CycleStyle()
    {
        if (weatherData == null) return;
        weatherData.shadowStyle = (ShadowStyle)(((int)weatherData.shadowStyle + 1) % 3);
        ApplyStyle();
        Debug.Log($"[Atmosphere] Style → {weatherData.shadowStyle}");
    }

    [ContextMenu("Atmosphere: Trigger Wind Gust Burst")]
    private void DebugGustBurst() => TriggerGustBurstNow();
#endif
}
