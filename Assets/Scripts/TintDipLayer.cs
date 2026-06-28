using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Cloud passes over the sun" style: a faint full-screen overlay that slowly pulses
/// darker/lighter. No moving patches. Mirrors RainOverlayUI's screen-space canvas pattern.
/// </summary>
public class TintDipLayer
{
    private readonly Transform parent;
    private WeatherData data;
    private Canvas canvas;
    private Image image;

    public TintDipLayer(Transform parent) { this.parent = parent; }

    public void Configure(WeatherData d)
    {
        data = d;
        if (canvas == null) Build();
    }

    public void SetActive(bool active)
    {
        if (canvas != null) canvas.gameObject.SetActive(active);
        if (!active && image != null) SetAlpha(0f);
    }

    public void Tick(float unscaledTime, float intensity)
    {
        if (data == null || image == null) return;
        // Slow sine; keep only the darkening half so the scene spends more time bright than dark.
        float phase = Mathf.Sin(unscaledTime * (2f * Mathf.PI / Mathf.Max(1f, data.tintDipPeriod)));
        float pulse = Mathf.Clamp01(phase);
        float peak  = data.tintDipMaxAlpha * (1f + Mathf.Clamp01(intensity) * data.shadowStormOpacityMul);
        SetAlpha(pulse * peak);
    }

    public void Clear() { if (image != null) SetAlpha(0f); }

    private void SetAlpha(float a)
    {
        Color c = data != null ? data.tintDipColor : Color.black;
        c.a = a;
        image.color = c;
    }

    private void Build()
    {
        var canvasGO = new GameObject("TintDipCanvas");
        canvasGO.transform.SetParent(parent, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 48; // just below RainOverlayUI's 50
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var imageGO = new GameObject("TintDip");
        imageGO.transform.SetParent(canvasGO.transform, false);
        image = imageGO.AddComponent<Image>();
        image.raycastTarget = false;
        var rt = imageGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        SetAlpha(0f);
    }
}
