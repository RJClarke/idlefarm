using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Handles all rain visuals:
///   1. A full-screen Canvas Image that fades to a dark blue overlay
///   2. A Unity Particle System that simulates falling raindrops
///
/// Tuning without code changes:
///   - All particle settings are exposed in the Inspector
///   - Right-click the component → "Rebuild Rain Particles" to apply changes live
///   - Right-click → "Test: Force FadeIn" / "Test: Force FadeOut" to preview
///
/// Rain Angle note:
///   Angle is applied via VelocityOverLifetime (X component) not transform rotation.
///   Positive = blows right, Negative = blows left.
///   A value of -8 to -15 gives a natural wind-blown look.
///
/// World unit reference (1080x1920 @ 32 PPU):
///   Screen height = 60 world units  (top = +30, bottom = -30 from center)
///   Screen width  = 33.75 world units
///   Emitter Y     = 35 units above camera center (just above screen top)
/// </summary>
public class RainOverlayUI : MonoBehaviour
{
    public static RainOverlayUI Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────
    // Inspector — Tunable Rain Settings
    // ─────────────────────────────────────────────────────────────────────

    [Header("Rain Particle Tuning")]
    [Tooltip("How long each raindrop lives (seconds).")]
    [SerializeField] private float rainMinLifetime = 5f;
    [SerializeField] private float rainMaxLifetime = 7f;

    [Tooltip("Downward fall speed in world units per second.")]
    [SerializeField] private float rainMinSpeed = 25f;
    [SerializeField] private float rainMaxSpeed = 40f;

    [Tooltip("Raindrop width in world units.")]
    [SerializeField] private float rainMinSize = 0.02f;
    [SerializeField] private float rainMaxSize = 0.05f;

    [Tooltip("Stretch multiplier — higher = longer streaks.")]
    [SerializeField] private float rainLengthScale = 1.5f;

    [Tooltip("Horizontal wind speed added to particles. Negative = blows left, Positive = blows right.")]
    [SerializeField] private float rainWindX = -8f;

    [Tooltip("How far above camera center the emitter sits.")]
    [SerializeField] private float rainEmitterY = 35f;

    [Tooltip("Gravity pull on particles.")]
    [SerializeField] private float rainGravity = 1f;

    [Tooltip("Raindrop color and opacity.")]
    [SerializeField] private Color rainColor = new Color(0.6f, 0.75f, 1f, 0.6f);

    // ─────────────────────────────────────────────────────────────────────
    // Runtime Components
    // ─────────────────────────────────────────────────────────────────────

    private Canvas         overlayCanvas;
    private Image          overlayImage;
    private ParticleSystem rainParticles;

    private WeatherData    data;
    private Coroutine      fadeCoroutine;

    // ─────────────────────────────────────────────────────────────────────
    // Unity
    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        BuildOverlayCanvas();
        BuildRainParticles();

        SetOverlayAlpha(0f);
        rainParticles.Stop();
    }

    private void Update()
    {
        if (rainParticles == null || Camera.main == null) return;
        Vector3 camPos = Camera.main.transform.position;
        float screenW = Camera.main.orthographicSize * Camera.main.aspect * 2f;

        if (Application.isPlaying && WeatherController.Instance != null && data != null)
        {
            WeatherState s = WeatherController.Instance.State;
            float precip = Mathf.Clamp01(s.precipitation);

            var emission = rainParticles.emission;
            emission.rateOverTime = data.rainParticleRate * precip;
            SetOverlayAlpha(data.rainOverlayMaxAlpha * precip);
            if (precip > 0.02f) { if (!rainParticles.isPlaying) rainParticles.Play(); }
            else if (rainParticles.isPlaying) rainParticles.Stop();

            // Straight diagonal streak: angle from vertical scales with severity, direction = the wind.
            float angleDeg = WeatherMath.RainAngleDegrees(s.wind, s.severity, data.rainMaxAngleDeg);
            float dir = s.windDirection < 0f ? -1f : 1f;
            float fall = Mathf.Lerp(data.rainFallSpeed.x, data.rainFallSpeed.y, Mathf.Clamp01(s.severity));
            float rad = angleDeg * Mathf.Deg2Rad;
            float vxRain = dir * fall * Mathf.Sin(rad);
            float vyRain = -fall * Mathf.Cos(rad);

            var main = rainParticles.main; main.gravityModifier = 0f; // no parabola
            var vel = rainParticles.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(vxRain);
            vel.y = new ParticleSystem.MinMaxCurve(vyRain);

            // Emit above the camera, offset upwind + widened so the angled rain still covers the screen.
            float avgLife = (rainMinLifetime + rainMaxLifetime) * 0.5f;
            float drift = Mathf.Abs(vxRain) * avgLife;
            rainParticles.transform.position = new Vector3(camPos.x - dir * drift * 0.5f, camPos.y + rainEmitterY, -1f);
            var shape = rainParticles.shape;
            shape.scale = new Vector3(screenW + drift + 4f, 0.1f, 1f);
        }
        else
        {
            rainParticles.transform.position = new Vector3(camPos.x, camPos.y + rainEmitterY, -1f);
            var shape = rainParticles.shape;
            shape.scale = new Vector3(screenW + 4f, 0.1f, 1f);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────

    public void Initialize(WeatherData weatherData)
    {
        data = weatherData;

        Color c = data.rainOverlayColor;
        c.a = 0f;
        overlayImage.color = c;

        var emission = rainParticles.emission;
        emission.rateOverTime = data.rainParticleRate;
    }

    public void FadeIn()
    {
        if (data == null) return;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        rainParticles.Play();
        fadeCoroutine = StartCoroutine(FadeOverlay(data.rainOverlayMaxAlpha, data.rainFadeInDuration));
    }

    public void FadeOut()
    {
        if (data == null) return;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOverlayThenStopParticles(data.rainFadeOutDuration));
    }

    public void ForceHide()
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        SetOverlayAlpha(0f);
        rainParticles.Stop();
        rainParticles.Clear();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Fade Coroutines
    // ─────────────────────────────────────────────────────────────────────

    private IEnumerator FadeOverlay(float targetAlpha, float duration)
    {
        float startAlpha = overlayImage.color.a;
        float elapsed    = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetOverlayAlpha(Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration));
            yield return null;
        }

        SetOverlayAlpha(targetAlpha);
    }

    private IEnumerator FadeOverlayThenStopParticles(float duration)
    {
        yield return FadeOverlay(0f, duration);

        var emission = rainParticles.emission;
        emission.rateOverTime = 0f;

        yield return new WaitForSeconds(rainMaxLifetime);
        rainParticles.Stop();
        rainParticles.Clear();

        if (data != null)
            emission.rateOverTime = data.rainParticleRate;
    }

    private void SetOverlayAlpha(float alpha)
    {
        Color c = overlayImage.color;
        c.a = alpha;
        overlayImage.color = c;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Builders
    // ─────────────────────────────────────────────────────────────────────

    private void BuildOverlayCanvas()
    {
        Transform existing = transform.Find("RainOverlayCanvas");
        if (existing != null) Destroy(existing.gameObject);

        GameObject canvasGO = new GameObject("RainOverlayCanvas");
        canvasGO.transform.SetParent(transform);

        overlayCanvas = canvasGO.AddComponent<Canvas>();
        overlayCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 50;

        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject imageGO = new GameObject("RainOverlay");
        imageGO.transform.SetParent(canvasGO.transform, false);

        overlayImage       = imageGO.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0f);

        RectTransform rt = imageGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        overlayImage.raycastTarget = false;
    }

    private void BuildRainParticles()
    {
        Transform existing = transform.Find("RainParticles");
        if (existing != null) Destroy(existing.gameObject);

        GameObject psGO = new GameObject("RainParticles");
        psGO.transform.SetParent(transform);
        psGO.transform.position = new Vector3(0f, rainEmitterY, -1f);
        psGO.transform.rotation = Quaternion.identity; // Angle handled via VelocityOverLifetime

        rainParticles = psGO.AddComponent<ParticleSystem>();

        // ── Main ───────────────────────────────────────────────────────────
        var main             = rainParticles.main;
        main.loop            = true;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(rainMinLifetime, rainMaxLifetime);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0f); // motion comes entirely from velocityOverLifetime
        main.startSize       = new ParticleSystem.MinMaxCurve(rainMinSize, rainMaxSize);
        main.startColor      = rainColor;
        main.gravityModifier = 0f; // straight line, no parabola — Update() sets the angled velocity
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        // ── Emission ───────────────────────────────────────────────────────
        var emission          = rainParticles.emission;
        emission.rateOverTime = 150f;

        // ── Shape: wide horizontal line ────────────────────────────────────
        var shape       = rainParticles.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Rectangle;
        shape.scale     = new Vector3(40f, 0.1f, 1f); // Update() corrects width every frame

        // ── Velocity Over Lifetime: adds horizontal wind component ─────────
        // This is the correct way to angle rain — not transform rotation
        var vel = rainParticles.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.World;
        vel.x       = new ParticleSystem.MinMaxCurve(0f); // Update() drives x/y from wind + severity
        vel.y       = new ParticleSystem.MinMaxCurve(-30f);
        vel.z       = new ParticleSystem.MinMaxCurve(0f);

        // ── Renderer ───────────────────────────────────────────────────────
        var renderer           = psGO.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode    = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.12f;
        renderer.lengthScale   = rainLengthScale;
        renderer.sortingOrder  = 5300; // rain in front of gusts (5200) + entities; below lightning (6000)
        renderer.material      = CreateRaindropMaterial();
    }

    private Material CreateRaindropMaterial()
    {
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color    = Color.white;
        return mat;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Editor Tools
    // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("Rebuild Rain Particles")]
    private void RebuildRainParticles()
    {
        bool wasPlaying = rainParticles != null && rainParticles.isPlaying;
        BuildRainParticles();

        if (data != null)
        {
            var emission = rainParticles.emission;
            emission.rateOverTime = data.rainParticleRate;
        }

        if (wasPlaying) rainParticles.Play();

        Debug.Log("[RainOverlayUI] Rain particles rebuilt with current Inspector values.");
    }

    [ContextMenu("Test: Force FadeIn")]
    private void TestFadeIn()
    {
        if (data == null) { Debug.LogWarning("[RainOverlayUI] No WeatherData assigned."); return; }
        FadeIn();
    }

    [ContextMenu("Test: Force FadeOut")]
    private void TestFadeOut()
    {
        if (data == null) { Debug.LogWarning("[RainOverlayUI] No WeatherData assigned."); return; }
        FadeOut();
    }

    [ContextMenu("Test: Force Hide")]
    private void TestForceHide() => ForceHide();
#endif
}