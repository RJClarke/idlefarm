using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Guided onboarding overlay: dims the screen (~70% black) with a "spotlight" hole over the
/// one control the player should use, plus a tooltip card + arrow. Steps advance by tap-anywhere,
/// by pressing the spotlighted control, or by a named game event (TutorialManager.Notify), so a
/// step can wait for the player to actually complete an action ("Spend your coins now").
///
/// Fully code-driven UITK (same pattern as ToastManager): owns its UIDocument + cloned
/// PanelSettings above every popup/toast. The dimmer is four pickable black strips around the
/// hole — they block all UI clicks except the spotlighted control, which receives the real press
/// (uGUI or UITK) because nothing pickable covers the hole. World-object clicks (OnMouseDown)
/// are blocked by zeroing Camera.main.eventMask while active; manual input pollers should check
/// <see cref="IsActive"/>.
///
/// First-time-only: completed sequence ids persist in the save (TutorialLedger, EconomyCore).
/// TryStart on a completed id is a no-op — so unlock-time tutorials months in are just TryStart
/// calls in the unlock code path.
/// </summary>
[DefaultExecutionOrder(1200)]
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    /// <summary>True while a sequence is showing. World-input pollers should early-out on this.</summary>
    public static bool IsActive => Instance != null && Instance.activeSequence != null;

    /// <summary>Id of the running sequence, or null.</summary>
    public static string ActiveId => Instance != null && Instance.activeSequence != null ? Instance.activeSequence.id : null;

    /// <summary>Fired with the sequence id when a sequence finishes its last step.</summary>
    public static event Action<string> OnSequenceCompleted;

    [Tooltip("Shared RunewoodPanelSettings. Cloned at runtime with a higher sort order so the " +
             "tutorial draws above popups and toasts. If null, a fresh PanelSettings is created.")]
    [SerializeField] private PanelSettings sourcePanelSettings;

    [Tooltip("Pixel font for tooltip text (UITK TextCore FontAsset from Fonts/UITK SDF).")]
    [SerializeField] private UnityEngine.TextCore.Text.FontAsset tooltipFont;
    [Tooltip("Smaller bake of the tooltip font for the hint line (pixel fonts are only crisp at their bake size). Falls back to the tooltip font.")]
    [SerializeField] private UnityEngine.TextCore.Text.FontAsset tooltipHintFont;

    [Range(0f, 1f)]
    [Tooltip("Dimmer strength. Design calls for 60-75% black.")]
    [SerializeField] private float dimOpacity = 0.7f;

    private const int SORT_ORDER = 2500;          // above toasts (2000) and every popup
    private const float TOOLTIP_WIDTH_FRAC = 0.8f;
    private const float HOLE_GAP = 40f;           // panel px between spotlight edge and tooltip card
    private const float ARROW_HALF = 14f;

    private UIDocument document;
    private PanelSettings runtimePanelSettings;
    private VisualElement root;

    private readonly TutorialLedger ledger = new TutorialLedger();
    private TutorialSequence activeSequence;
    private TutorialFlow flow;
    private int builtStepIndex = -1;

    // Elements of the currently shown step (positions refreshed every frame).
    private VisualElement stripTop, stripBottom, stripLeft, stripRight, frame, arrow, card, catcher;
    private Label cardText, cardHint;

    private Camera blockedCamera;
    private int savedEventMask;

    // ── Lifecycle ────────────────────────────────────────────────

    // Safety-net bootstrap (same pattern as SettingsManager's boot hook): if the loaded scene
    // has no TutorialManager, create a bare one so the framework always exists. Deliberately
    // NOT DontDestroyOnLoad — when the Splash scene bootstraps one, it dies on the switch to
    // FarmMain, whose scene-placed copy (with the pixel font + RunewoodPanelSettings assigned)
    // then claims Instance. Styling fields are unassigned on a bootstrap copy; ApplyFont falls
    // back to the engine font so text still renders.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("TutorialManager (bootstrap)");
        go.AddComponent<TutorialManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        document = GetComponent<UIDocument>();
        if (document == null) document = gameObject.AddComponent<UIDocument>();

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
        runtimePanelSettings.name = "TutorialPanelSettings (runtime)";
        runtimePanelSettings.sortingOrder = SORT_ORDER;

        // UIDocument.OnEnable already ran during AddComponent with no settings; re-enable so the
        // panel builds with ours (same dance as ToastManager).
        document.enabled = false;
        document.panelSettings = runtimePanelSettings;
        document.enabled = true;

        root = document.rootVisualElement;
        if (root != null)
        {
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.top = 0; root.style.bottom = 0; root.style.left = 0; root.style.right = 0;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            UnblockWorldInput();
            Instance = null;
        }
        if (runtimePanelSettings != null) Destroy(runtimePanelSettings);
    }

    // ── Public API ───────────────────────────────────────────────

    /// <summary>Starts the sequence unless it was already completed (or one is running). The
    /// normal entry point — safe to call every time the trigger condition occurs.</summary>
    public static bool TryStart(TutorialSequence sequence)
    {
        if (Instance == null || sequence == null || string.IsNullOrEmpty(sequence.id)) return false;
        if (Instance.activeSequence != null) return false;
        if (Instance.ledger.IsCompleted(sequence.id)) return false;
        Instance.Begin(sequence);
        return true;
    }

    /// <summary>Starts regardless of completion state (debug / explicit replay).</summary>
    public static void ForceStart(TutorialSequence sequence)
    {
        if (Instance == null || sequence == null) return;
        Instance.EndActive(markCompleted: false);
        Instance.Begin(sequence);
    }

    /// <summary>Runs the built-in 3-step demo (the farm-upgrade example from the onboarding
    /// design) so anyone can eyeball the spotlight/dim/tooltip/arrow without authoring real
    /// content first. Step 3 is a GameEvent step — pair with a "Fire Demo Event" button/call
    /// that invokes <c>Notify("demo_event")</c>, or use the Dev Tools drawer buttons which wire
    /// both up already. Also reachable via the TutorialManager component's right-click context
    /// menu in the Inspector during Play mode.</summary>
    public static void ForceStartDemo() => ForceStart(BuildDemoSequence());

    [ContextMenu("Start Demo Tutorial")]
    private void StartDemoFromInspector() => ForceStartDemo();

    [ContextMenu("Fire Demo Event (advance step 3)")]
    private void FireDemoEventFromInspector() => Notify("demo_event");

    /// <summary>Game code reports a named event ("farm_upgrade_purchased"). Advances the active
    /// step if it is waiting on exactly this id; otherwise a no-op.</summary>
    public static void Notify(string eventId)
    {
        if (Instance == null || Instance.flow == null) return;
        if (Instance.flow.HandleGameEvent(eventId)) Instance.AfterAdvance();
    }

    /// <summary>Dismisses a running sequence without marking it completed (it will re-trigger).</summary>
    public static void Abort()
    {
        if (Instance != null) Instance.EndActive(markCompleted: false);
    }

    /// <summary>Ends the running sequence as completed if it is <paramref name="id"/> — the player did the
    /// thing it points at by another route (e.g. claimed the Free Gift from the Store mid-spotlight).</summary>
    public static void CompleteIfActive(string id)
    {
        if (Instance != null && ActiveId == id) Instance.EndActive(markCompleted: true);
    }

    /// <summary>Has this tutorial already been completed? (For callers that want to skip setup.)</summary>
    public static bool IsCompleted(string id) => Instance != null && Instance.ledger.IsCompleted(id);

    /// <summary>Dev tools: dismiss anything showing and forget every completion so all tutorials replay.</summary>
    public static void DevResetAll()
    {
        if (Instance == null) return;
        Instance.EndActive(markCompleted: false);
        Instance.ledger.Clear();
        if (SaveManager.Instance != null) SaveManager.Instance.SaveGame();
    }

    // Save wiring (SaveManager): same shape as NewContentTracker's seenContentIds.
    public string[] GetCompletedForSave() => ledger.GetForSave();
    public void LoadState(string[] completedIds) => ledger.LoadState(completedIds);

    // ── Sequence driving ─────────────────────────────────────────

    private void Begin(TutorialSequence sequence)
    {
        var modes = new TutorialAdvance[sequence.steps.Count];
        var eventIds = new string[sequence.steps.Count];
        for (int i = 0; i < sequence.steps.Count; i++)
        {
            modes[i] = sequence.steps[i].advance;
            eventIds[i] = sequence.steps[i].eventId;
        }

        activeSequence = sequence;
        flow = new TutorialFlow(modes, eventIds);
        builtStepIndex = -1;
        BlockWorldInput();

        if (flow.IsComplete) EndActive(markCompleted: true); // empty sequence — nothing to show
    }

    private void AfterAdvance()
    {
        if (flow.IsComplete) EndActive(markCompleted: true);
        else builtStepIndex = -1; // next Update rebuilds the new step's elements
    }

    private void EndActive(bool markCompleted)
    {
        if (activeSequence == null) return;
        string id = activeSequence.id;

        activeSequence = null;
        flow = null;
        builtStepIndex = -1;
        if (root != null) root.Clear();
        stripTop = stripBottom = stripLeft = stripRight = frame = arrow = card = catcher = null;
        UnblockWorldInput();

        if (markCompleted)
        {
            ledger.Complete(id);
            if (SaveManager.Instance != null) SaveManager.Instance.SaveGame();
            OnSequenceCompleted?.Invoke(id);
        }
    }

    private void Update()
    {
        DebugPollRequests();
        if (activeSequence == null || flow == null || root == null) return;

        float panelW = root.resolvedStyle.width;
        float panelH = root.resolvedStyle.height;
        if (float.IsNaN(panelW) || panelW <= 0f || float.IsNaN(panelH) || panelH <= 0f) return;

        TutorialStep step = activeSequence.steps[flow.StepIndex];
        if (builtStepIndex != flow.StepIndex)
        {
            BuildStepElements(step);
            builtStepIndex = flow.StepIndex;
        }

        Rect? screenHole = ResolveHole(step);
        LayoutStep(step, screenHole, panelW, panelH);

        // TargetPressed steps: the hole passes the press through to the real control; we watch raw
        // input to know it happened. (Guarded by mode so a hole on other step kinds never advances.)
        if (flow.CurrentMode == TutorialAdvance.TargetPressed && screenHole.HasValue)
        {
            Vector2? press = PressedThisFrame();
            if (press.HasValue && screenHole.Value.Contains(press.Value) && flow.HandleTargetPressed())
                AfterAdvance();
        }
    }

    private static Vector2? PressedThisFrame()
    {
        Mouse m = Mouse.current;
        if (m != null && m.leftButton.wasPressedThisFrame) return m.position.ReadValue();
        Touchscreen t = Touchscreen.current;
        if (t != null && t.primaryTouch.press.wasPressedThisFrame) return t.primaryTouch.position.ReadValue();
        return null;
    }

    private Rect? ResolveHole(TutorialStep step)
    {
        Rect? r = step.getTargetScreenRect != null ? step.getTargetScreenRect() : null;
        if (!r.HasValue) return null;
        Rect rect = r.Value;
        float pad = step.spotlightPadding;
        return new Rect(rect.x - pad, rect.y - pad, rect.width + pad * 2f, rect.height + pad * 2f);
    }

    // ── Rendering ────────────────────────────────────────────────

    private void BuildStepElements(TutorialStep step)
    {
        root.Clear();
        Color dim = new Color(0f, 0f, 0f, PerceivedDimAlpha(step.dimOpacity ?? dimOpacity));

        stripTop = MakeStrip(dim);
        stripBottom = MakeStrip(dim);
        stripLeft = MakeStrip(dim);
        stripRight = MakeStrip(dim);

        frame = new VisualElement { name = "tutorial-spotlight-frame" };
        frame.pickingMode = PickingMode.Ignore;
        frame.style.position = Position.Absolute;
        SetBorderWidth(frame, 3);
        SetBorderColor(frame, new Color(1f, 0.84f, 0f));
        SetBorderRadius(frame, 14);
        root.Add(frame);

        arrow = new VisualElement { name = "tutorial-arrow" };
        arrow.pickingMode = PickingMode.Ignore;
        arrow.style.position = Position.Absolute;
        arrow.style.width = ARROW_HALF * 2f;
        arrow.style.height = ARROW_HALF * 2f;
        arrow.style.backgroundColor = new Color(0.11f, 0.12f, 0.13f, 0.98f);
        arrow.style.rotate = new Rotate(45f);
        SetBorderWidth(arrow, 2);
        SetBorderColor(arrow, new Color(1f, 0.84f, 0f));
        root.Add(arrow);

        card = new VisualElement { name = "tutorial-card" };
        card.pickingMode = PickingMode.Ignore;
        card.style.position = Position.Absolute;
        card.style.backgroundColor = new Color(0.11f, 0.12f, 0.13f, 0.98f);
        SetBorderRadius(card, 24);
        SetBorderWidth(card, 2);
        SetBorderColor(card, new Color(1f, 0.84f, 0f));
        card.style.paddingLeft = 28; card.style.paddingRight = 28;
        card.style.paddingTop = 22; card.style.paddingBottom = 20;

        cardText = new Label(step.text ?? "");
        cardText.pickingMode = PickingMode.Ignore;
        cardText.style.color = Color.white;
        cardText.style.fontSize = 31; // Munro Pixel 31's bake size
        cardText.style.whiteSpace = WhiteSpace.Normal;
        cardText.style.unityTextAlign = TextAnchor.MiddleCenter;
        ApplyFont(cardText);
        card.Add(cardText);

        cardHint = new Label(HintFor(step.advance));
        cardHint.pickingMode = PickingMode.Ignore;
        cardHint.style.color = new Color(1f, 1f, 1f, 0.55f);
        cardHint.style.fontSize = 20; // Munro Pixel 20 (the hint uses the smaller bake)
        cardHint.style.marginTop = 10;
        cardHint.style.unityTextAlign = TextAnchor.MiddleCenter;
        ApplyFont(cardHint, tooltipHintFont);
        cardHint.style.display = string.IsNullOrEmpty(cardHint.text) ? DisplayStyle.None : DisplayStyle.Flex;
        card.Add(cardHint);
        root.Add(card);

        // Tap-anywhere steps: one invisible catcher on top of everything (including the hole)
        // both blocks all interaction and advances on any press.
        catcher = null;
        if (step.advance == TutorialAdvance.TapAnywhere)
        {
            catcher = new VisualElement { name = "tutorial-catcher" };
            catcher.style.position = Position.Absolute;
            catcher.style.top = 0; catcher.style.bottom = 0; catcher.style.left = 0; catcher.style.right = 0;
            catcher.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (flow != null && flow.HandleTapAnywhere()) AfterAdvance();
            });
            root.Add(catcher);
        }
    }

    /// <summary>
    /// The project renders in Linear color space, so UI alpha blends in linear light: a 70% black
    /// overlay only darkens the screen by ~40% as the eye sees it (measured on the farm grass).
    /// Convert the designer-facing "how much darker it should look" into the linear alpha that
    /// actually produces it: visible brightness f = 1 - perceived, needs (1 - a) = f^2.2.
    /// </summary>
    private static float PerceivedDimAlpha(float perceived)
    {
        if (QualitySettings.activeColorSpace != ColorSpace.Linear) return perceived;
        float remaining = Mathf.Clamp01(1f - perceived);
        return 1f - Mathf.Pow(remaining, 2.2f);
    }

    private VisualElement MakeStrip(Color dim)
    {
        var strip = new VisualElement();
        strip.style.position = Position.Absolute;
        strip.style.backgroundColor = dim; // pickable: blocks everything it covers
        root.Add(strip);
        return strip;
    }

    private void LayoutStep(TutorialStep step, Rect? screenHole, float panelW, float panelH)
    {
        if (!screenHole.HasValue)
        {
            // No target: full-screen dim, centered card, no frame/arrow.
            SetRect(stripTop, 0, 0, panelW, panelH);
            SetRect(stripBottom, 0, 0, 0, 0);
            SetRect(stripLeft, 0, 0, 0, 0);
            SetRect(stripRight, 0, 0, 0, 0);
            frame.style.display = DisplayStyle.None;
            arrow.style.display = DisplayStyle.None;

            float w = panelW * TOOLTIP_WIDTH_FRAC;
            card.style.width = w;
            card.style.left = (panelW - w) / 2f;
            card.style.top = StyleKeyword.Auto;
            // Menu tips sit just above the bottom edge (clear of a phone's home-indicator inset),
            // below the popup, rather than across the middle of it.
            float safeBottom = Screen.height > 0 ? Screen.safeArea.yMin / Screen.height * panelH : 0f;
            card.style.bottom = step.cardAtBottom ? safeBottom + panelH * 0.025f : panelH * 0.45f;
            return;
        }

        Rect hole = ScreenRectToPanel(screenHole.Value, panelW, panelH);
        hole.xMin = Mathf.Clamp(hole.xMin, 0f, panelW);
        hole.xMax = Mathf.Clamp(hole.xMax, 0f, panelW);
        hole.yMin = Mathf.Clamp(hole.yMin, 0f, panelH);
        hole.yMax = Mathf.Clamp(hole.yMax, 0f, panelH);

        SetRect(stripTop, 0, 0, panelW, hole.yMin);
        SetRect(stripBottom, 0, hole.yMax, panelW, panelH - hole.yMax);
        SetRect(stripLeft, 0, hole.yMin, hole.xMin, hole.height);
        SetRect(stripRight, hole.xMax, hole.yMin, panelW - hole.xMax, hole.height);

        frame.style.display = DisplayStyle.Flex;
        SetRect(frame, hole.xMin, hole.yMin, hole.width, hole.height);

        // Card above or below the hole, whichever side has more room; arrow points at the hole.
        float spaceAbove = hole.yMin - HOLE_GAP;
        float spaceBelow = panelH - hole.yMax - HOLE_GAP;
        bool below = spaceBelow >= spaceAbove;
        float cardW = panelW * TOOLTIP_WIDTH_FRAC;
        float cardX = Mathf.Clamp(hole.center.x - cardW / 2f, panelW * 0.02f, panelW * 0.98f - cardW);
        float arrowX = Mathf.Clamp(hole.center.x - ARROW_HALF, cardX + 30f, cardX + cardW - 30f - ARROW_HALF * 2f);

        card.style.width = cardW;
        card.style.left = cardX;
        arrow.style.display = DisplayStyle.Flex;
        arrow.style.left = arrowX;

        // A big spotlight (a whole popup) can leave no room either side — pin the card to the top
        // edge over the target instead of pushing it off-screen. The card isn't pickable, so taps
        // still reach the spotlighted control underneath it.
        float cardH = card.resolvedStyle.height;
        if (float.IsNaN(cardH) || cardH <= 0f) cardH = 240f;
        if (Mathf.Max(spaceAbove, spaceBelow) < cardH + panelH * 0.02f)
        {
            arrow.style.display = DisplayStyle.None;
            card.style.bottom = StyleKeyword.Auto;
            card.style.top = panelH * 0.03f;
            return;
        }

        if (below)
        {
            card.style.bottom = StyleKeyword.Auto;
            card.style.top = hole.yMax + HOLE_GAP;
            arrow.style.top = hole.yMax + HOLE_GAP - ARROW_HALF;
            arrow.style.bottom = StyleKeyword.Auto;
        }
        else
        {
            card.style.top = StyleKeyword.Auto;
            card.style.bottom = panelH - hole.yMin + HOLE_GAP;
            arrow.style.top = hole.yMin - HOLE_GAP - ARROW_HALF;
            arrow.style.bottom = StyleKeyword.Auto;
        }
    }

    /// <summary>Screen rect (bottom-left origin, px) → this panel's coords (top-left origin).</summary>
    private Rect ScreenRectToPanel(Rect screen, float panelW, float panelH)
    {
        float sx = panelW / Screen.width;
        float sy = panelH / Screen.height;
        float topLeftY = Screen.height - screen.yMax; // flip Y
        return new Rect(screen.x * sx, topLeftY * sy, screen.width * sx, screen.height * sy);
    }

    /// <summary>A runtime-created PanelSettings has no theme, so Labels have NO default font and
    /// render nothing. Prefer the assigned pixel font; otherwise fall back to the engine's
    /// built-in font so text always shows (same gotcha class as project_uitoolkit_gotchas).</summary>
    private void ApplyFont(Label label, UnityEngine.TextCore.Text.FontAsset preferred = null)
    {
        var font = preferred != null ? preferred : tooltipFont;
        if (font != null)
        {
            label.style.unityFontDefinition = new StyleFontDefinition(font);
            return;
        }
        Font builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (builtin != null)
            label.style.unityFontDefinition = new StyleFontDefinition(builtin);
    }

    private static string HintFor(TutorialAdvance advance) => advance switch
    {
        TutorialAdvance.TapAnywhere => "Tap to continue",
        _ => "",
    };

    private static void SetRect(VisualElement el, float x, float y, float w, float h)
    {
        el.style.left = x;
        el.style.top = y;
        el.style.width = Mathf.Max(0f, w);
        el.style.height = Mathf.Max(0f, h);
    }

    private static void SetBorderRadius(VisualElement el, float r)
    {
        el.style.borderTopLeftRadius = r; el.style.borderTopRightRadius = r;
        el.style.borderBottomLeftRadius = r; el.style.borderBottomRightRadius = r;
    }

    private static void SetBorderWidth(VisualElement el, float w)
    {
        el.style.borderTopWidth = w; el.style.borderBottomWidth = w;
        el.style.borderLeftWidth = w; el.style.borderRightWidth = w;
    }

    private static void SetBorderColor(VisualElement el, Color c)
    {
        el.style.borderTopColor = c; el.style.borderBottomColor = c;
        el.style.borderLeftColor = c; el.style.borderRightColor = c;
    }

    // ── World-input blocking ─────────────────────────────────────

    private void BlockWorldInput()
    {
        blockedCamera = Camera.main;
        if (blockedCamera != null)
        {
            savedEventMask = blockedCamera.eventMask;
            blockedCamera.eventMask = 0; // suppresses all OnMouseDown/Up world clickboxes
        }
    }

    private void UnblockWorldInput()
    {
        if (blockedCamera != null)
        {
            blockedCamera.eventMask = savedEventMask;
            blockedCamera = null;
        }
    }

    // ── Editor debug driver (file-triggered, same convention as PlayModeBridge) ──

    private float nextDebugPoll;

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void DebugPollRequests()
    {
        if (Time.unscaledTime < nextDebugPoll) return;
        nextDebugPoll = Time.unscaledTime + 2f;

        if (System.IO.File.Exists("Temp/tutorial_demo.request"))
        {
            System.IO.File.Delete("Temp/tutorial_demo.request");
            ForceStart(BuildDemoSequence());
        }
        if (System.IO.File.Exists("Temp/tutorial_event.request"))
        {
            System.IO.File.Delete("Temp/tutorial_event.request");
            Notify("demo_event");
        }
        if (System.IO.File.Exists("Temp/tutorial_advance.request"))
        {
            // Headless step-advance for MCP verification (no way to inject real taps).
            System.IO.File.Delete("Temp/tutorial_advance.request");
            if (flow != null && !flow.IsComplete)
            {
                bool advanced = flow.CurrentMode switch
                {
                    TutorialAdvance.TapAnywhere => flow.HandleTapAnywhere(),
                    TutorialAdvance.TargetPressed => flow.HandleTargetPressed(),
                    _ => false,
                };
                if (advanced) AfterAdvance();
            }
        }
    }

    /// <summary>The farm-upgrade example from the onboarding design, on fixed screen areas —
    /// exercises all three advance modes without needing real content wiring.</summary>
    private static TutorialSequence BuildDemoSequence()
    {
        return new TutorialSequence
        {
            id = "demo",
            steps =
            {
                new TutorialStep
                {
                    text = "You can upgrade various things on your farm with Coins down here.",
                    advance = TutorialAdvance.TapAnywhere,
                    getTargetScreenRect = TutorialTargets.Normalized(0.05f, 0.015f, 0.9f, 0.09f),
                },
                new TutorialStep
                {
                    text = "Let's upgrade your Farm - tap here.",
                    advance = TutorialAdvance.TargetPressed,
                    getTargetScreenRect = TutorialTargets.Normalized(0.06f, 0.02f, 0.2f, 0.08f),
                },
                new TutorialStep
                {
                    text = "This will help you grow more crops. Spend your Coins on this upgrade now.",
                    advance = TutorialAdvance.GameEvent,
                    eventId = "demo_event",
                    getTargetScreenRect = TutorialTargets.Normalized(0.3f, 0.35f, 0.4f, 0.12f),
                },
            },
        };
    }
}
