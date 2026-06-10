using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Top-left strip of "🌱 CropName: N" counters, one per active crop, driven by SeedInventory.
/// Pulses red when a crop has 0 seeds. Code-built Canvas (no UXML) for a trial-quality HUD.
/// </summary>
public class SeedCounterHUD : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset font;
    private Canvas _canvas;
    private readonly Dictionary<CropData, TextMeshProUGUI> _labels = new Dictionary<CropData, TextMeshProUGUI>();

    private void Awake()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 400;
        gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
    }

    private void OnEnable()
    {
        if (SeedInventory.Instance != null)
            SeedInventory.Instance.OnSeedCountChanged += HandleChange;
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += Clear;
            RunManager.Instance.OnRunEnded += Clear;
        }
    }

    private void OnDisable()
    {
        if (SeedInventory.Instance != null)
            SeedInventory.Instance.OnSeedCountChanged -= HandleChange;
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= Clear;
            RunManager.Instance.OnRunEnded -= Clear;
        }
    }

    private void Clear()
    {
        foreach (var lbl in _labels.Values) if (lbl != null) Destroy(lbl.gameObject);
        _labels.Clear();
    }

    private void HandleChange(CropData crop, int remaining)
    {
        if (crop == null) return;
        if (!_labels.TryGetValue(crop, out var lbl) || lbl == null)
        {
            lbl = CreateLabel(_labels.Count);
            _labels[crop] = lbl;
        }
        lbl.text = $"\U0001F331 {crop.cropName}: {remaining}";
        lbl.color = remaining <= 0 ? new Color(0.85f, 0.15f, 0.15f) : Color.white;
    }

    private TextMeshProUGUI CreateLabel(int index)
    {
        var go = new GameObject($"SeedCounter_{index}", typeof(RectTransform));
        go.transform.SetParent(_canvas.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 28;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(360, 40);
        rt.anchoredPosition = new Vector2(20f, -20f - index * 36f);
        return tmp;
    }
}
