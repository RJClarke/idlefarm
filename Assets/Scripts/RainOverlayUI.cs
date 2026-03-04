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
        if (rainParticles != null && Camera.main != null)
        {
            Vector3 camPos = Camera.main.transform.position;

            // Wind pushes rain sideways over its lifetime, so offset the emitter upwind
            // and widen it so rain covers the full screen at every Y level
            float avgLifetime = (rainMinLifetime + rainMaxLifetime) * 0.5f;
            float windDrift = Mathf.Abs(rainWindX) * avgLifetime;
            float windOffsetX = -Mathf.Sign(rainWindX) * windDrift * 0.5f;

            rainParticles.transform.position = new Vector3(
                camPos.x + windOffsetX,
                camPos.y + rainEmitterY,
                -1f);

            // Emitter must be wide enough for screen + full wind drift on both sides
            float screenWidth = Camera.main.orthographicSize * Camera.main.aspect * 2f;
            float emitterWidth = screenWidth + windDrift + 4f;
            var shape = rainParticles.shape;
            shape.scale = new Vector3(emitterWidth, 0.1f, 1f);
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
        main.startSpeed      = new ParticleSystem.MinMaxCurve(rainMinSpeed, rainMaxSpeed);
        main.startSize       = new ParticleSystem.MinMaxCurve(rainMinSize, rainMaxSize);
        main.startColor      = rainColor;
        main.gravityModifier = rainGravity;
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
        vel.x       = new ParticleSystem.MinMaxCurve(rainWindX);
        vel.y       = new ParticleSystem.MinMaxCurve(0f);
        vel.z       = new ParticleSystem.MinMaxCurve(0f);

        // ── Renderer ───────────────────────────────────────────────────────
        var renderer           = psGO.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode    = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.12f;
        renderer.lengthScale   = rainLengthScale;
        renderer.sortingOrder  = 55;
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