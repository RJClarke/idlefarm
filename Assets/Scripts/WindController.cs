using UnityEngine;

/// <summary>
/// Publishes the global wind shader uniforms read by the WindSway shader (_GlobalWindMul amplitude +
/// _WindTime). Wind strength now comes from the shared WeatherState (WeatherController) so sway is quiet
/// at Clear and rises with windy/stormy weather. A small Perlin gust rides on top, scaled by the wind.
///
/// Sway only animates while the game is genuinely running. In edit mode and while the editor is paused
/// we hold everything still — no wind, and _WindTime is frozen — so trees don't animate on the Game
/// screen when the game isn't active. _WindTime accrues from wall-clock but only while active, so it's
/// game-speed agnostic yet pause-aware, and resumes seamlessly (no time jump) after a pause.
///
/// [ExecuteAlways] is kept so this can actively force wind to 0 in edit mode — otherwise a stale
/// _GlobalWindMul left over from a play session would keep sprites swaying in the editor.
/// </summary>
[ExecuteAlways]
public class WindController : MonoBehaviour
{
    [Header("Gusts")]
    [Tooltip("How much the slow Perlin gusts swing the wind up and down (scaled by current wind).")]
    [SerializeField] private float gustStrength = 0.25f;
    [Tooltip("Speed of the gust noise (lower = slower, lazier gusts).")]
    [SerializeField] private float gustSpeed = 0.15f;

    [Header("Mapping")]
    [Tooltip("_GlobalWindMul at full weather wind (1.0).")]
    [SerializeField] private float maxWindMultiplier = 3f;
    [Tooltip("Fallback breeze during play before the WeatherController spins up (early frames).")]
    [SerializeField] private float editorPreviewWind = 0.15f;

    private static readonly int WindID = Shader.PropertyToID("_GlobalWindMul");
    private static readonly int TimeID = Shader.PropertyToID("_WindTime");

    // Wind clock that only advances while the game is actively playing (not edit mode, not paused).
    private float windTime;
    private float lastRealtime;

    private void OnEnable()
    {
        lastRealtime = Time.realtimeSinceStartup;
        Apply();
    }
    private void OnDisable() => Shader.SetGlobalFloat(WindID, 0f);
    private void Update() => Apply();

    private void Apply()
    {
        float now = Time.realtimeSinceStartup;
        float dt = now - lastRealtime;
        lastRealtime = now;

        // Only sway while the game is genuinely running. In edit mode, or while the editor is paused,
        // hold the trees still: freeze the wind clock and zero the amplitude so nothing animates.
        bool active = Application.isPlaying;
#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPaused) active = false;
#endif
        if (!active)
        {
            Shader.SetGlobalFloat(WindID, 0f);
            Shader.SetGlobalFloat(TimeID, windTime); // hold last frozen time so resume is seamless
            return;
        }

        windTime += dt; // wall-clock, but accrues only while active → game-speed agnostic, pause-aware
        Shader.SetGlobalFloat(TimeID, windTime);

        // Wind strength (0..1) from the shared WeatherState; small fallback until it's ready.
        float windCh = WeatherController.Instance != null
            ? WeatherController.Instance.State.wind
            : editorPreviewWind;

        float gust = (Mathf.PerlinNoise(windTime * gustSpeed, 0.37f) - 0.5f) * 2f * gustStrength * windCh;
        float wind = (windCh + gust) * maxWindMultiplier;
        Shader.SetGlobalFloat(WindID, Mathf.Max(0f, wind));
    }
}
