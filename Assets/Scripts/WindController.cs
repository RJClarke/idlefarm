using UnityEngine;

/// <summary>
/// Publishes the global wind shader uniforms read by the WindSway shader (_GlobalWindMul amplitude +
/// _WindTime). Wind strength now comes from the shared WeatherState (WeatherController) so sway is quiet
/// at Clear and rises with windy/stormy weather. A small Perlin gust rides on top, scaled by the wind.
///
/// [ExecuteAlways] so sway still previews in the editor (using editorPreviewWind, since the
/// WeatherController only runs in play mode). _WindTime uses unscaled real time (game-speed agnostic).
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
    [Tooltip("Editor-only preview breeze when no WeatherController is running.")]
    [SerializeField] private float editorPreviewWind = 0.15f;

    private static readonly int WindID = Shader.PropertyToID("_GlobalWindMul");
    private static readonly int TimeID = Shader.PropertyToID("_WindTime");

    private void OnEnable() => Apply();
    private void OnDisable() => Shader.SetGlobalFloat(WindID, 0f);
    private void Update() => Apply();

    private void Apply()
    {
        float t = Time.realtimeSinceStartup; // works in edit + play, independent of game speed
        Shader.SetGlobalFloat(TimeID, t);

        // Wind strength (0..1) from the shared WeatherState; gentle preview breeze in the editor.
        float windCh = (Application.isPlaying && WeatherController.Instance != null)
            ? WeatherController.Instance.State.wind
            : editorPreviewWind;

        float gust = (Mathf.PerlinNoise(t * gustSpeed, 0.37f) - 0.5f) * 2f * gustStrength * windCh;
        float wind = (windCh + gust) * maxWindMultiplier;
        Shader.SetGlobalFloat(WindID, Mathf.Max(0f, wind));
    }
}
