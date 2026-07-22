using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD alert for a fish on the line while you're AWAY from the Lake: the same cream speech
/// bubble as the in-world bite indicator, docked to the right screen edge with its tail pointing
/// right (toward the Lake). Shown at the Farm / Greenhouse / Woods — never at the Market, and
/// not at the Lake itself where the world bubble over the bobber does the job. Anchors slightly
/// higher at the Woods and lower at the Greenhouse (tunable). Tapping it pans to the Lake, same
/// as the Lake nav button. Builds its own overlay canvas — the scene just needs one GameObject
/// with this component, nothing to wire.
/// </summary>
public class LakeBiteAlert : MonoBehaviour
{
    [Header("Placement (anchor Y per location, 0..1 of screen height)")]
    [SerializeField] private float farmY = 0.5f;
    [SerializeField] private float woodsY = 0.60f;
    [SerializeField] private float greenhouseY = 0.40f;
    [Tooltip("Gap between the tail tip and the screen's right edge.")]
    [SerializeField] private float edgeMargin = 6f;

    [Header("Style (matches the world bite bubble)")]
    [SerializeField] private Color boxFill = new Color32(0xFA, 0xF3, 0xE1, 0xF5);
    [SerializeField] private Color boxStroke = new Color32(0x32, 0x2F, 0x2B, 0xFF);
    [SerializeField] private int cornerRadius = 10;
    [SerializeField] private int borderThickness = 4;

    private const float TailLength = 20f;
    private const float TailBreadth = 30f;

    private Canvas canvas;
    private RectTransform bubble;
    private Image icon;
    private CameraPanController pan;
    private LakeNode lakeNode;
    private CameraPanController.Location lastLoc = CameraPanController.Location.Farm;

    private void Start()
    {
        BuildUI();

        pan = Camera.main != null ? Camera.main.GetComponent<CameraPanController>() : null;
        lakeNode = FindFirstObjectByType<LakeNode>(FindObjectsInactive.Include);
        if (pan != null)
        {
            pan.OnPanStarted   += OnPanChanged;
            pan.OnPanCompleted += OnPanChanged;
            lastLoc = pan.CurrentLocation;
        }
        if (FishingManager.Instance != null) FishingManager.Instance.OnChanged += Refresh;

        Refresh();
    }

    private void OnDestroy()
    {
        if (pan != null)
        {
            pan.OnPanStarted   -= OnPanChanged;
            pan.OnPanCompleted -= OnPanChanged;
        }
        if (FishingManager.Instance != null) FishingManager.Instance.OnChanged -= Refresh;
    }

    // Track the pan TARGET so the alert swaps at the start of a trip, like the nav buttons do.
    private void OnPanChanged(CameraPanController.Location loc) { lastLoc = loc; Refresh(); }

    private void Refresh()
    {
        if (bubble == null) return;
        var fm = FishingManager.Instance;
        bool show = fm != null && fm.HasBite && IsAwayLocation(lastLoc);
        bubble.gameObject.SetActive(show);
        if (!show) return;

        if (icon != null && lakeNode != null)
        {
            Sprite s = lakeNode.FishIcon(fm.PendingTier);
            icon.sprite = s;
            icon.enabled = s != null;
        }

        float y = lastLoc == CameraPanController.Location.Woods      ? woodsY
                : lastLoc == CameraPanController.Location.Greenhouse ? greenhouseY
                : farmY;
        bubble.anchorMin = new Vector2(1f, y);
        bubble.anchorMax = new Vector2(1f, y);
    }

    private static bool IsAwayLocation(CameraPanController.Location loc)
        => loc == CameraPanController.Location.Farm
        || loc == CameraPanController.Location.Greenhouse
        || loc == CameraPanController.Location.Woods;

    private void OnTapped()
    {
        if (pan != null) pan.PanTo(CameraPanController.Location.Lake);
    }

    private void BuildUI()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400; // above the HUD, below floating rewards (500) and toasts
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
        }

        var go = new GameObject("BiteBubble", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(canvas.transform, false);
        bubble = (RectTransform)go.transform;
        bubble.pivot = new Vector2(1f, 0.5f);
        bubble.sizeDelta = new Vector2(96f, 88f);
        // Leave room for the tail (it sticks out past the box's right edge).
        bubble.anchoredPosition = new Vector2(-(edgeMargin + TailLength - borderThickness), 0f);

        Image box = go.GetComponent<Image>();
        box.sprite = WorldHintPopup.BuildBoxSprite(boxFill, boxStroke, cornerRadius, borderThickness);
        box.type = Image.Type.Sliced;

        go.GetComponent<Button>().onClick.AddListener(OnTapped);

        var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(go.transform, false);
        icon = iconGO.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        var irt = (RectTransform)iconGO.transform;
        irt.anchorMin = new Vector2(0.5f, 0.5f);
        irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.sizeDelta = new Vector2(56f, 56f);

        var tailGO = new GameObject("Tail", typeof(RectTransform), typeof(Image));
        tailGO.transform.SetParent(go.transform, false);
        Image tail = tailGO.GetComponent<Image>();
        tail.sprite = WorldHintPopup.BuildTailSprite(boxFill, boxStroke, borderThickness,
            pointRight: true, length: (int)TailLength, breadth: (int)TailBreadth);
        tail.raycastTarget = false;
        var trt = (RectTransform)tailGO.transform;
        trt.anchorMin = new Vector2(1f, 0.5f);
        trt.anchorMax = new Vector2(1f, 0.5f);
        trt.pivot = new Vector2(0f, 0.5f);
        trt.sizeDelta = new Vector2(TailLength, TailBreadth);
        // Overlap the box's stroke so the tail's fill columns open the border.
        trt.anchoredPosition = new Vector2(-borderThickness, 0f);

        // Gentle attention pulse so a waiting fish catches the eye without shouting.
        LeanTween.scale(go, Vector3.one * 1.05f, 0.6f).setLoopPingPong().setEaseInOutSine().setIgnoreTimeScale(true);

        go.SetActive(false);
    }
}
