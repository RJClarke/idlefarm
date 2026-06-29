using UnityEngine;

/// <summary>
/// The single brain for weather. Owns the live WeatherState and eases it toward the active profile —
/// casual moods on a random schedule (everywhere), or a storm profile when ThunderstormManager calls
/// BeginStorm. Every effect reads WeatherController.Instance.State. Play-mode only.
/// </summary>
public class WeatherController : MonoBehaviour
{
    public static WeatherController Instance { get; private set; }

    [SerializeField] private WeatherData data;

    public enum WeatherDebug { None, Clear, Cloudy, Windy, Storm1, Storm3, Storm5 }
    [Tooltip("Debug: force a weather state live (overrides the schedule). Set None for normal play.")]
    [SerializeField] private WeatherDebug debugForce = WeatherDebug.None;

    public WeatherState State => state;
    private WeatherState state;

    private bool stormActive;
    private WeatherProfile stormProfile;        // built on BeginStorm
    private CasualWeather casualMood = CasualWeather.Clear;
    private float rollTimer;
    private float eventTimer;                    // >0 while a casual mood is held

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        state.windDirection = WindDir();
        rollTimer = data != null ? Random.Range(data.casualRollInterval.x, data.casualRollInterval.y) : 120f;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private float WindDir() => (data != null && data.windDriftDirection < 0f) ? -1f : 1f;

    public void BeginStorm(float severity)
    {
        if (data == null) return;
        severity = Mathf.Clamp01(severity);
        stormActive = true;
        stormProfile = new WeatherProfile
        {
            name = "Storm",
            severity = severity,
            cloudiness = Mathf.Lerp(data.stormCloudiness.x, data.stormCloudiness.y, severity),
            wind = Mathf.Lerp(data.stormWind.x, data.stormWind.y, severity),
            precipitation = Mathf.Lerp(data.stormPrecip.x, data.stormPrecip.y, severity),
        };
    }

    public void EndStorm() => stormActive = false;

    private void Update()
    {
        if (data == null) return;
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

        if (debugForce != WeatherDebug.None) ApplyDebug();
        else if (!stormActive) TickCasualSchedule(dt);

        WeatherProfile target = stormActive ? stormProfile : ActiveCasualProfile();
        float spd = data.weatherBlendSpeed;
        state.severity      = WeatherMath.EaseChannel(state.severity,      target.severity,      dt, spd);
        state.wind          = WeatherMath.EaseChannel(state.wind,          target.wind,          dt, spd);
        state.cloudiness    = WeatherMath.EaseChannel(state.cloudiness,    target.cloudiness,    dt, spd);
        state.precipitation = WeatherMath.EaseChannel(state.precipitation, target.precipitation, dt, spd);
        state.windDirection = WindDir();
    }

    private void TickCasualSchedule(float dt)
    {
        if (eventTimer > 0f)
        {
            eventTimer -= dt;
            if (eventTimer <= 0f) casualMood = CasualWeather.Clear;
            return;
        }
        rollTimer -= dt;
        if (rollTimer <= 0f)
        {
            rollTimer = Random.Range(data.casualRollInterval.x, data.casualRollInterval.y);
            int pick = WeatherMath.RollCasual(Random.value, data.casualClearWeight, data.casualCloudyWeight, data.casualWindyWeight);
            casualMood = (CasualWeather)pick;
            if (casualMood != CasualWeather.Clear)
                eventTimer = Random.Range(data.casualEventDuration.x, data.casualEventDuration.y);
        }
    }

    private void ApplyDebug()
    {
        switch (debugForce)
        {
            case WeatherDebug.Clear:  stormActive = false; casualMood = CasualWeather.Clear;  break;
            case WeatherDebug.Cloudy: stormActive = false; casualMood = CasualWeather.Cloudy; break;
            case WeatherDebug.Windy:  stormActive = false; casualMood = CasualWeather.Windy;  break;
            case WeatherDebug.Storm1: BeginStorm(WeatherMath.StormSeverity(1, data.stormsToMaxSeverity)); break;
            case WeatherDebug.Storm3: BeginStorm(WeatherMath.StormSeverity(3, data.stormsToMaxSeverity)); break;
            case WeatherDebug.Storm5: BeginStorm(WeatherMath.StormSeverity(5, data.stormsToMaxSeverity)); break;
        }
    }

    private WeatherProfile ActiveCasualProfile()
    {
        switch (casualMood)
        {
            case CasualWeather.Cloudy: return data.profileCloudy;
            case CasualWeather.Windy:  return data.profileWindy;
            default:                   return data.profileClear;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Weather: Force Cloudy")] private void DbgCloudy() { stormActive = false; casualMood = CasualWeather.Cloudy; eventTimer = 30f; }
    [ContextMenu("Weather: Force Windy")]  private void DbgWindy()  { stormActive = false; casualMood = CasualWeather.Windy;  eventTimer = 30f; }
    [ContextMenu("Weather: Force Clear")]  private void DbgClear()  { stormActive = false; casualMood = CasualWeather.Clear;  eventTimer = 0f; }
    [ContextMenu("Weather: Force Storm 1")] private void DbgStorm1() => BeginStorm(WeatherMath.StormSeverity(1, data != null ? data.stormsToMaxSeverity : 5f));
    [ContextMenu("Weather: Force Storm 3")] private void DbgStorm3() => BeginStorm(WeatherMath.StormSeverity(3, data != null ? data.stormsToMaxSeverity : 5f));
    [ContextMenu("Weather: Force Storm 5")] private void DbgStorm5() => BeginStorm(WeatherMath.StormSeverity(5, data != null ? data.stormsToMaxSeverity : 5f));
    [ContextMenu("Weather: End Storm")]    private void DbgEndStorm() => EndStorm();
#endif
}
