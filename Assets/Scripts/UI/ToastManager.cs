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

    [Header("Top Toast (parchment notice)")]
    [Tooltip("9-sliced parchment panel for the top toasts (Panel_Parchment_9Slice, border 5). " +
             "Falls back to the old flat dark card if unset.")]
    [SerializeField] private Sprite topBackground;
    [Tooltip("Pixel font for toast text. Falls back to the catch font when unset, so both " +
             "toast styles share one typeface without extra scene wiring.")]
    [SerializeField] private UnityEngine.TextCore.Text.FontAsset toastFont;
    [Tooltip("Title AND subtitle size in px. Keep it the font's bake size (Munro Pixel 41) or strokes go " +
             "uneven. One size for both: a smaller subtitle lost whole strokes whenever the view was scaled " +
             "down (e.g. a shrunk editor Game view), so the subtitle differs by colour only.")]
    [SerializeField] private int toastFontSize = 41;
    [Tooltip("Faux bold. Leave off for pixel fonts: it thickens strokes unevenly.")]
    [SerializeField] private bool toastTitleBold = false;
    [Tooltip("Default icon for ToastKind.Success when the caller supplies none.")]
    [SerializeField] private Sprite successIcon;
    [Tooltip("Default icon for ToastKind.Unlock when the caller supplies none.")]
    [SerializeField] private Sprite unlockIcon;

    private const int MAX_VISIBLE = 3;
    private const float IN_SEC = 0.25f;    // slide/fade in
    private const float HOLD_SEC = 2.2f;   // time fully visible
    private const float OUT_SEC = 0.3f;    // slide/fade out
    private const float HIDDEN_Y = -130f;  // translateY percent when off-screen (above)
    private const int TOAST_SORT_ORDER = 2000;

    // Parchment ink. Cream is a light field, so text is dark walnut rather than an accent
    // colour — gold on cream is illegible, the same finding recorded on the catch toast below.
    private static readonly Color INK = new Color32(0x3E, 0x2A, 0x16, 0xFF);
    private static readonly Color INK_MUTED = new Color32(0x5E, 0x45, 0x26, 0xFF); // dark enough to read on cream
    // Panel_Parchment_9Slice: 48x40 with a uniform 5px frame. Every border band is a solid run
    // along its stretch axis and the centre is one flat colour, so this slices without artifacts.
    private const int TOP_SLICE = 5;
    private const float TOP_SLICE_SCALE = 3f;

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
        public Sprite icon;
    }

    /// <summary>Text font for both toast styles; <see cref="catchFont"/> is the legacy fallback.</summary>
    private UnityEngine.TextCore.Text.FontAsset Font => toastFont != null ? toastFont : catchFont;

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

    /// <summary>
    /// Queue a top parchment notice. <paramref name="icon"/> is optional — when null the
    /// per-kind default is used, and if that is unset too the toast renders text-only.
    /// </summary>
    public static void Show(string title, string subtitle, ToastKind kind = ToastKind.Success,
                            Sprite icon = null)
    {
        if (Instance == null || string.IsNullOrEmpty(title)) return;
        Instance.Enqueue(title, subtitle, kind, icon);
    }

#if UNITY_EDITOR
    /// <summary>Dev preview (PlayModeBridge): a bottom catch toast with no icon.</summary>
    public static void DevShowCatch(string message) => ShowCatch(null, message);

    /// <summary>Dev preview (PlayModeBridge): swap the top-toast font by asset name from
    /// Fonts/UITK SDF at a size, turn faux bold off, and show a sample. Play-session only.</summary>
    public static string DevPreviewFont(string fontName, int size)
    {
        if (Instance == null) return "no ToastManager";
        Instance.toastFont = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.TextCore.Text.FontAsset>($"Assets/Fonts/UITK SDF/{fontName}.asset");
        Instance.toastFontSize = size;
        Instance.toastTitleBold = false;
        Show("Research Complete", "Water Speed Level 2", ToastKind.Success);
        return $"font={Instance.toastFont?.name}";
    }
#endif

    private void Enqueue(string title, string subtitle, ToastKind kind, Sprite icon)
    {
        pending.Enqueue(new PendingToast { title = title, subtitle = subtitle, kind = kind, icon = icon });
        Pump();
    }

    // ── Stack / queue ────────────────────────────

    private void Pump()
    {
        if (stack == null) return;
        while (visibleCount < MAX_VISIBLE && pending.Count > 0)
        {
            PendingToast p = pending.Dequeue();
            SpawnToast(p.title, p.subtitle, p.kind, p.icon);
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
    public static void ShowCatch(Sprite icon, string message) => ShowCatch(icon, message, null);

    /// <summary>
    /// As above, but tappable: onClick fires if the player hits the toast before it sinks. Used by
    /// the Reputation level-up toast to jump the player to the Barn.
    /// </summary>
    public static void ShowCatch(Sprite icon, string message, System.Action onClick)
    {
        if (Instance == null || string.IsNullOrEmpty(message)) return;
        if (Instance.bottomStack == null) return;
        Instance.SpawnCatchToast(icon, message, onClick);
    }

    private void SpawnCatchToast(Sprite icon, string message, System.Action onClick)
    {
        VisualElement toast = BuildCatchElement(icon, message, onClick);
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

    private VisualElement BuildCatchElement(Sprite icon, string message, System.Action onClick = null)
    {
        VisualElement toast = new VisualElement { name = "catch-toast" };
        // Catch toasts are normally inert; a toast with an action becomes tappable and consumes
        // the click so it cannot fall through to the world behind it.
        toast.pickingMode = onClick == null ? PickingMode.Ignore : PickingMode.Position;
        if (onClick != null)
            toast.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); onClick(); });
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
        text.style.fontSize = 31; // Munro Pixel 31's bake size: off-size pixel fonts smear
        text.style.unityFontStyleAndWeight = FontStyle.Normal;
        text.style.unityTextAlign = TextAnchor.MiddleLeft;
        if (catchFont != null)
            text.style.unityFontDefinition = new StyleFontDefinition(catchFont);
        toast.Add(text);

        toast.style.opacity = 0f;
        toast.style.translate = new Translate(0, Length.Percent(130f));
        return toast;
    }

    private void SpawnToast(string title, string subtitle, ToastKind kind, Sprite icon)
    {
        visibleCount++;
        VisualElement toast = BuildToastElement(title, subtitle, kind, icon);
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

    /// <summary>
    /// A parchment notice: icon on the left, title + detail to its right. The text pair lives in
    /// a wrapping row, so short messages sit on one line and long ones drop the detail onto a
    /// second line — no length special-casing. The toast hugs its content up to 95% width.
    /// </summary>
    private VisualElement BuildToastElement(string title, string subtitle, ToastKind kind, Sprite icon)
    {
        VisualElement toast = new VisualElement { name = "toast" };
        toast.pickingMode = PickingMode.Ignore;
        toast.style.flexDirection = FlexDirection.Row;
        toast.style.alignItems = Align.Center;
        toast.style.maxWidth = Length.Percent(95);
        toast.style.minHeight = 88;
        toast.style.marginBottom = 10;

        if (topBackground != null)
        {
            toast.style.backgroundImage = new StyleBackground(topBackground);
            toast.style.unitySliceLeft = TOP_SLICE;
            toast.style.unitySliceRight = TOP_SLICE;
            toast.style.unitySliceTop = TOP_SLICE;
            toast.style.unitySliceBottom = TOP_SLICE;
            toast.style.unitySliceScale = TOP_SLICE_SCALE;
            // Clear of the 15px scaled frame on every side.
            toast.style.paddingLeft = 26;
            toast.style.paddingRight = 32;
            toast.style.paddingTop = 20;
            toast.style.paddingBottom = 20;
        }
        else
        {
            // Fallback: the pre-parchment flat dark card, so a missing sprite degrades quietly.
            SetBorderRadius(toast, 24);
            toast.style.backgroundColor = new Color(0.11f, 0.12f, 0.13f, 0.95f);
            SetBorderWidth(toast, 2);
            SetBorderColor(toast, new Color(1f, 0.84f, 0f));
            toast.style.paddingLeft = 24;
            toast.style.paddingRight = 24;
            toast.style.paddingTop = 16;
            toast.style.paddingBottom = 16;
        }

        bool onParchment = topBackground != null;
        Color titleColor = onParchment ? INK : new Color(1f, 0.84f, 0f);
        Color subColor = onParchment ? INK_MUTED : new Color(1f, 1f, 1f, 0.82f);

        Sprite shown = icon != null ? icon : DefaultIconFor(kind);
        if (shown != null)
        {
            Image img = new Image { sprite = shown, scaleMode = ScaleMode.ScaleToFit };
            img.pickingMode = PickingMode.Ignore;
            img.style.width = 48;
            img.style.height = 48;
            img.style.flexShrink = 0;
            img.style.marginRight = 16;
            toast.Add(img);
        }

        // A wrap container reports its intrinsic width as its widest child rather than the sum,
        // so an auto-width toast can hug to less than its own text. With wrapping labels that
        // showed up as the title splitting and short words breaking mid-character ("Hors/e").
        // The labels are therefore NoWrap — they can only break *between* title and subtitle,
        // never inside a word — and the container keeps its full content width.
        VisualElement text = new VisualElement { name = "toast-text" };
        text.pickingMode = PickingMode.Ignore;
        text.style.flexDirection = FlexDirection.Row;
        text.style.flexWrap = Wrap.Wrap;
        text.style.alignItems = Align.Center;
        text.style.flexShrink = 0;
        toast.Add(text);

        Label titleLabel = new Label(title);
        titleLabel.pickingMode = PickingMode.Ignore;
        titleLabel.style.color = titleColor;
        titleLabel.style.fontSize = toastFontSize;
        titleLabel.style.unityFontStyleAndWeight = toastTitleBold ? FontStyle.Bold : FontStyle.Normal;
        titleLabel.style.whiteSpace = WhiteSpace.NoWrap;
        titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        if (Font != null) titleLabel.style.unityFontDefinition = new StyleFontDefinition(Font);
        text.Add(titleLabel);

        if (!string.IsNullOrEmpty(subtitle))
        {
            Label subLabel = new Label(subtitle);
            subLabel.pickingMode = PickingMode.Ignore;
            subLabel.style.color = subColor;
            subLabel.style.fontSize = toastFontSize;
            subLabel.style.whiteSpace = WhiteSpace.NoWrap;
            subLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            subLabel.style.marginLeft = 16; // gap when inline; harmless once wrapped
            if (Font != null) subLabel.style.unityFontDefinition = new StyleFontDefinition(Font);
            text.Add(subLabel);
        }

        // Start hidden + nudged up; the lifecycle coroutine animates it into place.
        toast.style.opacity = 0f;
        toast.style.translate = new Translate(0, Length.Percent(HIDDEN_Y));
        return toast;
    }

    /// <summary>
    /// Per-kind fallback icon. Toast <em>kind</em> is now carried by the icon rather than by an
    /// accent colour, since coloured text is unreadable on the cream parchment.
    /// </summary>
    private Sprite DefaultIconFor(ToastKind kind) => kind switch
    {
        ToastKind.Unlock => unlockIcon,
        _                => successIcon,
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

    // Emoji are deliberately absent from these titles — they render invisible in UITK text on
    // Android. The sprite icon carries that meaning instead.

    private void OnResearchLeveledUp(string researchID, int newLevel)
    {
        var rd = ResearchManager.Instance != null ? ResearchManager.Instance.GetResearch(researchID) : null;
        string name = rd != null && !string.IsNullOrEmpty(rd.displayName) ? rd.displayName : researchID;
        Sprite branchIcon = rd != null ? Research.ResearchBranchIcons.For(rd.branchID) : null;
        Show("Research Complete", $"{name} Level {newLevel}", ToastKind.Success, branchIcon);
    }

    private void OnAnimalUnlocked(string animalID)
    {
        var data = AnimalManager.Instance != null ? AnimalManager.Instance.GetAnimalData(animalID) : null;
        string name = data != null && !string.IsNullOrEmpty(data.displayName) ? data.displayName : animalID;
        Show("New Unlock!", name, ToastKind.Unlock, Research.ResearchBranchIcons.For("animals"));
    }
}
