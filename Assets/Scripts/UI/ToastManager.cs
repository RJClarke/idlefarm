using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Transient celebratory banners ("toasts") for big completed milestones — research
/// finishing, first-time unlocks, etc. Slides down from the top, holds, slides back up.
///
/// Distinct from FloatingTextManager (world-anchored +$/+G currency pops). Fully
/// code-driven: owns its own UIDocument + a cloned PanelSettings at a high sort order so
/// toasts render above every popup. The only scene step is placing one GameObject with
/// this component and assigning <see cref="sourcePanelSettings"/>.
///
/// Animation is driven by coroutines (unscaled time) writing inline styles each frame,
/// rather than USS transitions / VisualElement.schedule — those proved unreliable on a
/// runtime-created panel.
/// </summary>
[DefaultExecutionOrder(1100)]
public class ToastManager : MonoBehaviour
{
    public static ToastManager Instance { get; private set; }

    public enum ToastKind { Success, Unlock }

    [Tooltip("Shared RunewoodPanelSettings. Cloned at runtime with a higher sort order so " +
             "toasts draw above popups. If left null, a fresh PanelSettings is created.")]
    [SerializeField] private PanelSettings sourcePanelSettings;

    [Header("Catch Toast (fishing)")]
    [Tooltip("9-sliced wooden plank background for the bottom catch toast (Runewood frame). " +
             "Falls back to the flat dark style if unset.")]
    [SerializeField] private Sprite catchBackground;
    [Tooltip("Pixel font for the catch toast text (a UITK TextCore FontAsset from Fonts/UITK SDF).")]
    [SerializeField] private UnityEngine.TextCore.Text.FontAsset catchFont;

    private const int MAX_VISIBLE = 3;
    private const float IN_SEC = 0.25f;    // slide/fade in
    private const float HOLD_SEC = 2.2f;   // time fully visible
    private const float OUT_SEC = 0.3f;    // slide/fade out
    private const float HIDDEN_Y = -130f;  // translateY percent when off-screen (above)
    private const int TOAST_SORT_ORDER = 2000;

    private UIDocument document;
    private PanelSettings runtimePanelSettings;
    private VisualElement stack;
    private VisualElement bottomStack;   // horizontal catch toasts, anchored above the bottom nav
    private int visibleCount;
    private readonly Queue<PendingToast> pending = new Queue<PendingToast>();

    private struct PendingToast
    {
        public string title;
        public string subtitle;
        public ToastKind kind;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        document = GetComponent<UIDocument>();
        if (document == null) document = gameObject.AddComponent<UIDocument>();

        // Clone the shared settings so we keep the project's theme/scale but draw on top.
        if (sourcePanelSettings != null)
        {
            runtimePanelSettings = Instantiate(sourcePanelSettings);
        }
        else
        {
            runtimePanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            runtimePanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            runtimePanelSettings.referenceResolution = new Vector2Int(1080, 1920);
            runtimePanelSettings.match = 0.5f;
        }
        runtimePanelSettings.name = "ToastPanelSettings (runtime)";
        runtimePanelSettings.sortingOrder = TOAST_SORT_ORDER;

        // The UIDocument's OnEnable already ran (during AddComponent) with no panelSettings,
        // so its panel/rootVisualElement was never built. Assign settings, then re-enable to
        // force the panel to build with them in place.
        document.enabled = false;
        document.panelSettings = runtimePanelSettings;
        document.enabled = true;
    }

    private void Start()
    {
        BuildStack();
        Subscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this) Instance = null;
        if (runtimePanelSettings != null) Destroy(runtimePanelSettings);
    }

    // ── Public API ───────────────────────────────

    public static void Show(string message, ToastKind kind = ToastKind.Success) => Show(message, null, kind);

    public static void Show(string title, string subtitle, ToastKind kind = ToastKind.Success)
    {
        if (Instance == null || string.IsNullOrEmpty(title)) return;
        Instance.Enqueue(title, subtitle, kind);
    }

    private void Enqueue(string title, string subtitle, ToastKind kind)
    {
        pending.Enqueue(new PendingToast { title = title, subtitle = subtitle, kind = kind });
        Pump();
    }

    // ── Stack / queue ────────────────────────────

    private void Pump()
    {
        if (stack == null) return;
        while (visibleCount < MAX_VISIBLE && pending.Count > 0)
        {
            PendingToast p = pending.Dequeue();
            SpawnToast(p.title, p.subtitle, p.kind);
        }
    }

    private void BuildStack()
    {
        VisualElement root = document != null ? document.rootVisualElement : null;
        if (root == null) { Debug.LogWarning("[ToastManager] rootVisualElement null — panel not built."); return; }

        root.pickingMode = PickingMode.Ignore; // never eat clicks meant for the game/UI below

        stack = new VisualElement { name = "toast-stack" };
        stack.pickingMode = PickingMode.Ignore;
        stack.style.position = Position.Absolute;
        stack.style.top = 110;   // below the top bar
        stack.style.left = 0;
        stack.style.right = 0;
        stack.style.alignItems = Align.Center;
        root.Add(stack);

        bottomStack = new VisualElement { name = "toast-bottom-stack" };
        bottomStack.pickingMode = PickingMode.Ignore;
        bottomStack.style.position = Position.Absolute;
        bottomStack.style.bottom = 190; // clear the bottom nav
        bottomStack.style.left = 0;
        bottomStack.style.right = 0;
        bottomStack.style.alignItems = Align.Center;
        root.Add(bottomStack);

        Pump(); // flush anything queued before the stack was ready
    }

    // ── Bottom catch toast (icon left, text right) ───────────────

    /// <summary>A small bottom toast for a fish catch: the fish's sprite on the left, message on
    /// the right (rich text ok — the fish name arrives pre-colorized). Slides up from below, holds,
    /// slides back down. Fire-and-forget (no queue).</summary>
    public static void ShowCatch(Sprite icon, string message)
    {
        if (Instance == null || string.IsNullOrEmpty(message)) return;
        if (Instance.bottomStack == null) return;
        Instance.SpawnCatchToast(icon, message);
    }

    private void SpawnCatchToast(Sprite icon, string message)
    {
        VisualElement toast = BuildCatchElement(icon, message);
        bottomStack.Add(toast);
        StartCoroutine(CatchLifecycle(toast));
    }

    private IEnumerator CatchLifecycle(VisualElement toast)
    {
        yield return Animate(toast, 0f, 1f, 130f, 0f, IN_SEC, true);   // rise from below
        yield return new WaitForSecondsRealtime(HOLD_SEC);
        yield return Animate(toast, 1f, 0f, 0f, 130f, OUT_SEC, false); // sink back down
        toast.RemoveFromHierarchy();
    }

    private VisualElement BuildCatchElement(Sprite icon, string message)
    {
        VisualElement toast = new VisualElement { name = "catch-toast" };
        toast.pickingMode = PickingMode.Ignore;
        toast.style.flexDirection = FlexDirection.Row;
        toast.style.alignItems = Align.Center;
        // Generous padding so icon + text sit well clear of the sliced wooden frame.
        toast.style.paddingLeft = 44;
        toast.style.paddingRight = 50;
        toast.style.paddingTop = 30;
        toast.style.paddingBottom = 30;

        if (catchBackground != null)
        {
            // Wooden plank sign (Runewood frame, no authored border → slice manually).
            toast.style.backgroundImage = new StyleBackground(catchBackground);
            toast.style.unitySliceLeft = 14;
            toast.style.unitySliceRight = 14;
            toast.style.unitySliceTop = 12;
            toast.style.unitySliceBottom = 12;
            toast.style.unitySliceScale = 2f;
        }
        else
        {
            // Fallback: the old flat dark card with a watery-blue border.
            SetBorderRadius(toast, 22);
            toast.style.backgroundColor = new Color(0.11f, 0.12f, 0.13f, 0.95f);
            SetBorderWidth(toast, 2);
            SetBorderColor(toast, new Color(0.35f, 0.6f, 0.95f));
        }

        if (icon != null)
        {
            Image img = new Image { sprite = icon, scaleMode = ScaleMode.ScaleToFit };
            img.pickingMode = PickingMode.Ignore;
            img.style.width = 52;
            img.style.height = 52;
            img.style.marginRight = 14;
            toast.Add(img);
        }

        // Dark walnut text — cream was illegible on the light plank. No outline: the plank is a
        // flat light field, so a stroke just muddies the pixels. Rich-text tier tints on the fish
        // name stay their own (mid-dark) colors, which read fine on the wood.
        Label text = new Label(message);
        text.pickingMode = PickingMode.Ignore;
        text.style.color = (Color)new Color32(0x3E, 0x2A, 0x16, 0xFF);
        text.style.fontSize = 30;
        text.style.unityFontStyleAndWeight = FontStyle.Bold;
        text.style.unityTextAlign = TextAnchor.MiddleLeft;
        if (catchFont != null)
            text.style.unityFontDefinition = new StyleFontDefinition(catchFont);
        toast.Add(text);

        toast.style.opacity = 0f;
        toast.style.translate = new Translate(0, Length.Percent(130f));
        return toast;
    }

    private void SpawnToast(string title, string subtitle, ToastKind kind)
    {
        visibleCount++;
        VisualElement toast = BuildToastElement(title, subtitle, kind);
        stack.Insert(0, toast); // newest on top, older ones flow below
        StartCoroutine(ToastLifecycle(toast));
    }

    private IEnumerator ToastLifecycle(VisualElement toast)
    {
        yield return Animate(toast, 0f, 1f, HIDDEN_Y, 0f, IN_SEC, true);
        yield return new WaitForSecondsRealtime(HOLD_SEC);
        yield return Animate(toast, 1f, 0f, 0f, HIDDEN_Y, OUT_SEC, false);

        toast.RemoveFromHierarchy();
        visibleCount = Mathf.Max(0, visibleCount - 1);
        Pump();
    }

    private IEnumerator Animate(VisualElement el, float fromOpacity, float toOpacity,
                                float fromY, float toY, float dur, bool easeOut)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = easeOut ? 1f - Mathf.Pow(1f - k, 3f) : k * k * k; // easeOutCubic / easeInCubic
            el.style.opacity = Mathf.Lerp(fromOpacity, toOpacity, e);
            el.style.translate = new Translate(0, Length.Percent(Mathf.Lerp(fromY, toY, e)));
            yield return null;
        }
        el.style.opacity = toOpacity;
        el.style.translate = new Translate(0, Length.Percent(toY));
    }

    private VisualElement BuildToastElement(string title, string subtitle, ToastKind kind)
    {
        VisualElement toast = new VisualElement { name = "toast" };
        toast.pickingMode = PickingMode.Ignore;
        toast.style.flexDirection = FlexDirection.Column;
        toast.style.alignItems = Align.FlexStart;
        toast.style.width = Length.Percent(95);
        toast.style.marginBottom = 10;
        toast.style.paddingLeft = 24;
        toast.style.paddingRight = 24;
        toast.style.paddingTop = 16;
        toast.style.paddingBottom = 16;
        SetBorderRadius(toast, 24);
        toast.style.backgroundColor = new Color(0.11f, 0.12f, 0.13f, 0.95f);

        Color accent = AccentFor(kind);
        SetBorderWidth(toast, 2);
        SetBorderColor(toast, accent);

        Label titleLabel = new Label(title);
        titleLabel.pickingMode = PickingMode.Ignore;
        titleLabel.style.color = accent;
        titleLabel.style.fontSize = 36;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.whiteSpace = WhiteSpace.Normal;
        titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        toast.Add(titleLabel);

        if (!string.IsNullOrEmpty(subtitle))
        {
            Label subLabel = new Label(subtitle);
            subLabel.pickingMode = PickingMode.Ignore;
            subLabel.style.color = new Color(1f, 1f, 1f, 0.82f);
            subLabel.style.fontSize = 27;
            subLabel.style.whiteSpace = WhiteSpace.Normal;
            subLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            subLabel.style.marginTop = 3;
            toast.Add(subLabel);
        }

        // Start hidden + nudged up; the lifecycle coroutine animates it into place.
        toast.style.opacity = 0f;
        toast.style.translate = new Translate(0, Length.Percent(HIDDEN_Y));
        return toast;
    }

    private static Color AccentFor(ToastKind kind) => kind switch
    {
        ToastKind.Unlock => new Color(0.44f, 0.79f, 0.39f),  // green
        _                => new Color(1f, 0.84f, 0f),         // gold
    };

    private static void SetBorderRadius(VisualElement el, float r)
    {
        el.style.borderTopLeftRadius = r;
        el.style.borderTopRightRadius = r;
        el.style.borderBottomLeftRadius = r;
        el.style.borderBottomRightRadius = r;
    }

    private static void SetBorderWidth(VisualElement el, float w)
    {
        el.style.borderTopWidth = w;
        el.style.borderBottomWidth = w;
        el.style.borderLeftWidth = w;
        el.style.borderRightWidth = w;
    }

    private static void SetBorderColor(VisualElement el, Color c)
    {
        el.style.borderTopColor = c;
        el.style.borderBottomColor = c;
        el.style.borderLeftColor = c;
        el.style.borderRightColor = c;
    }

    // ── Triggers ─────────────────────────────────

    private void Subscribe()
    {
        if (ResearchManager.Instance != null)
            ResearchManager.Instance.OnResearchLeveledUp += OnResearchLeveledUp;
        if (AnimalManager.Instance != null)
            AnimalManager.Instance.OnAnimalUnlocked += OnAnimalUnlocked;
    }

    private void Unsubscribe()
    {
        if (ResearchManager.Instance != null)
            ResearchManager.Instance.OnResearchLeveledUp -= OnResearchLeveledUp;
        if (AnimalManager.Instance != null)
            AnimalManager.Instance.OnAnimalUnlocked -= OnAnimalUnlocked;
    }

    private void OnResearchLeveledUp(string researchID, int newLevel)
    {
        var rd = ResearchManager.Instance != null ? ResearchManager.Instance.GetResearch(researchID) : null;
        string name = rd != null && !string.IsNullOrEmpty(rd.displayName) ? rd.displayName : researchID;
        Show("✨ Research Complete", $"{name} Level {newLevel}", ToastKind.Success);
    }

    private void OnAnimalUnlocked(string animalID)
    {
        var data = AnimalManager.Instance != null ? AnimalManager.Instance.GetAnimalData(animalID) : null;
        string name = data != null && !string.IsNullOrEmpty(data.displayName) ? data.displayName : animalID;
        Show("\U0001F513 New Unlock!", name, ToastKind.Unlock);
    }
}
