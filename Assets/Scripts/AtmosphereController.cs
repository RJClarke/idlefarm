using UnityEngine;

/// <summary>
/// Always-on cosmetic atmosphere brain. Reads the global wind multiplier (WindController's
/// _GlobalWindMul) and storm state, eases a 0..1 storm "intensity", and drives the cloud-shadow
/// and wind-debris layers. [ExecuteAlways] so it previews in the editor like WindController.
/// Cosmetic only — never applies gameplay effects.
/// </summary>
[ExecuteAlways]
public class AtmosphereController : MonoBehaviour
{
    [SerializeField] private WeatherData weatherData;

    private static readonly int WindID = Shader.PropertyToID("_GlobalWindMul");
    private static readonly int TimeID = Shader.PropertyToID("_WindTime");

    private CloudShadowLayer shadows;
    private TintDipLayer tintDip;
    private WindDebrisLayer debris;
    private float intensity;
    private ShadowStyle lastStyle;

    private void OnEnable()
    {
        shadows ??= new CloudShadowLayer(transform);
        tintDip ??= new TintDipLayer(transform);
        debris  ??= new WindDebrisLayer(transform);
        Reconfigure();
    }

    private void OnDisable()
    {
        shadows?.Clear();
        tintDip?.Clear();
        debris?.Clear();
    }

    private void Reconfigure()
    {
        if (weatherData == null) return;
        shadows.Configure(weatherData);
        tintDip.Configure(weatherData);
        debris.Configure(weatherData);
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

        float windMul = Mathf.Max(0f, Shader.GetGlobalFloat(WindID));
        float wTime   = Shader.GetGlobalFloat(TimeID);
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

        // Ease storm intensity (cosmetic) from ThunderstormManager state.
        float target = 0f;
        if (Application.isPlaying && ThunderstormManager.Instance != null &&
            (ThunderstormManager.Instance.IsWindActive || ThunderstormManager.Instance.IsStormActive))
            target = 1f;
        intensity = AtmosphereMath.EaseIntensity(intensity, target, dt, weatherData.atmosphereStormLerpSpeed);

        Camera cam = Camera.main;
        if (weatherData.shadowStyle == ShadowStyle.TintDip)
            tintDip.Tick(wTime, intensity);
        else
            shadows.Tick(dt, windMul, intensity, cam);

        debris.Tick(windMul, intensity, cam);
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

    [ContextMenu("Atmosphere: Force Storm Intensity")]
    private void ForceStormIntensity() => intensity = 1f;
#endif
}
