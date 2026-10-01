using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The Town Requests board popup: a wooden notice board holding 3 pinned paper notes
/// (Easy/Medium/Hard). Each note shows who is asking and why, a have/need progress bar per
/// requested item (requests may carry several), the Reputation reward inline, and a Submit button
/// enabled only once every line is satisfied — so the player never has to open anything to decide.
/// Fully code-driven UIDocument (ToastManager pattern). Spec: 2026-07-19-reputation-design.md §3.
/// </summary>
[DefaultExecutionOrder(1050)]
public class TownRequestsPopupUITK : MonoBehaviour
{
    public static TownRequestsPopupUITK Instance { get; private set; }
    private const int SORT_ORDER = 1000;
    private static readonly string[] DifficultyNames = { "EASY", "MEDIUM", "HARD" };
    private static readonly Color[] DifficultyColors =
    {
        new Color(0.35f, 0.58f, 0.28f),  // Easy - green
        new Color(0.72f, 0.56f, 0.15f),  // Medium - gold
        new Color(0.70f, 0.28f, 0.24f),  // Hard - red
    };

    // Board + paper palette. Used as-is when the 9-slice sprites below are unassigned, so the
    // board still reads correctly before any art is wired up.
    private static readonly Color CorkBrown = new Color(0.46f, 0.34f, 0.22f);
    private static readonly Color PaperCream = new Color(0.97f, 0.94f, 0.85f);
    private static readonly Color Ink = new Color(0.20f, 0.14f, 0.07f);
    private static readonly Color InkSoft = new Color(0.42f, 0.33f, 0.22f);
    private static readonly Color TrackEmpty = new Color(0.80f, 0.74f, 0.62f);

    // ONE green does double duty: the progress-bar fill and the "satisfied" count text. Chosen dark
    // enough to clear WCAG AA (~5.7:1) as text on the cream paper, which a lighter fill-green would
    // fail — so the bar and the text can share a colour without the text becoming unreadable.
    private static readonly Color Green = new Color(0.180f, 0.420f, 0.165f);
    // Matches the gem icon's purple, darkened to stay legible on cream (~5.2:1).
    private static readonly Color GemPurple = new Color(0.455f, 0.329f, 0.643f);
    // The dark brown used for headings elsewhere (FarmNamePopup title).
    private static readonly Color TitleBrown = new Color(0.373f, 0.275f, 0.149f);

    [Tooltip("Shared RunewoodPanelSettings, cloned at runtime with a higher sort order (ToastManager pattern). " +
             "If left null, a fresh PanelSettings is created — but with no theme, text will not render.")]
    [SerializeField] private PanelSettings sourcePanelSettings;

    [Header("Board art (all optional — solid colours are used when unassigned)")]
    [Tooltip("Wooden frame for the board itself, e.g. UI_Wood/UI_Wood_Frame_Standard_02.")]
    [SerializeField] private Sprite boardFrame;
    [Tooltip("Paper note behind each request, e.g. UI_Book/UI_NoteBook_Frame01a.")]
    [SerializeField] private Sprite noteFrame;
    [Tooltip("Progress bar track, e.g. UI_Book/UI_NoteBook_Bar01a.")]
    [SerializeField] private Sprite barTrack;
    [Tooltip("Progress bar fill, e.g. UI_Book/UI_NoteBook_BarFill01a.")]
    [SerializeField] private Sprite barFill;

    [Tooltip("Gem icon for the Skip cost, e.g. Icons_Essential/Gem.png.")]
    [SerializeField] private Sprite gemIcon;
    [Tooltip("Close button icon, e.g. UI_Wood/UI_Wood_Cross_Medium.png.")]
    [SerializeField] private Sprite closeIcon;

    [Header("Item icons (optional — rows fall back to text only)")]
    [SerializeField] private CropDatabase cropDatabase;
    [SerializeField] private Sprite woodIcon;
    [SerializeField] private Sprite compostIcon;
    [SerializeField] private Sprite eggIcon;
    [SerializeField] private Sprite fishIcon;

    private UIDocument document;
    private PanelSettings runtimePanelSettings;
    private VisualElement root;
    private VisualElement popupRoot;
    private VisualElement backdrop;
    private VisualElement slotsColumn;
    private bool isOpen;
    public bool IsOpen => isOpen;

    /// <summary>Live handles for one requested line, so bars refresh without rebuilding the note.</summary>
    private class LineView
    {
        public DeliveryLineItem item;
        public VisualElement fill;
        public Label count;
    }

    /// <summary>Live handles for one slot's note.</summary>
    private class SlotView
    {
        public readonly List<LineView> lines = new List<LineView>();
        public Button submit;
        public Label cooldown;
    }

    private readonly SlotView[] slotViews = new SlotView[3];

    // Height of the last live card shown in each slot, measured as it lays out. A completed slot's
    // ghost reuses it so the outline is exactly the size of the card that was taken down, instead
    // of collapsing to an arbitrary height and shuffling the board.
    private readonly float[] lastCardHeight = new float[3];
    // Fallbacks for a slot that has never shown a live card this session (e.g. opening the board
    // straight after a reload while it is still on cooldown). Easy=1 item, Medium=2, Hard=2-3.
    private static readonly float[] DefaultGhostHeight = { 250f, 292f, 330f };

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
        runtimePanelSettings.name = "TownRequestsPanelSettings (runtime)";
        runtimePanelSettings.sortingOrder = SORT_ORDER;

        document.enabled = false;
        document.panelSettings = runtimePanelSettings;
        document.enabled = true;
    }

    private void Start()
    {
        Build();
        if (ReputationManager.Instance != null)
            ReputationManager.Instance.OnChanged += OnChanged;
    }

    private void OnDestroy()
    {
        if (ReputationManager.Instance != null)
            ReputationManager.Instance.OnChanged -= OnChanged;
        if (Instance == this) Instance = null;
        if (runtimePanelSettings != null) Destroy(runtimePanelSettings);
    }

    private void Update()
    {
        // Countdowns tick and inventory can change while the board is open; refresh the live bits
        // in place rather than rebuilding the notes every frame.
        if (isOpen) RefreshLive();
    }

    private void OnChanged() { if (isOpen) BuildSlots(); }

    public void Open()
    {
        if (isOpen || popupRoot == null) return;
        isOpen = true;
        root.pickingMode = PickingMode.Position;
        BuildSlots();
        popupRoot.style.display = DisplayStyle.Flex;
        OnboardingTutorials.OnMenuOpened("tip_town_requests"); // one-time how-to (new players)
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        if (popupRoot != null) popupRoot.style.display = DisplayStyle.None;
        if (root != null) root.pickingMode = PickingMode.Ignore;
    }

    /// <summary>
    /// Applies a sprite as a 9-sliced background, falling back to a flat colour. sliceScale scales
    /// the rendered border thickness — the board art is small (96px), so it needs scaling up to
    /// read as a chunky wooden frame at popup size.
    /// </summary>
    private static void ApplyFrame(VisualElement element, Sprite sprite, int slice, Color fallback, float sliceScale = 1f)
    {
        if (sprite == null)
        {
            element.style.backgroundColor = fallback;
            element.style.borderTopLeftRadius = 10; element.style.borderTopRightRadius = 10;
            element.style.borderBottomLeftRadius = 10; element.style.borderBottomRightRadius = 10;
            return;
        }
        element.style.backgroundImage = new StyleBackground(sprite);
        element.style.unitySliceLeft = slice;
        element.style.unitySliceRight = slice;
        element.style.unitySliceTop = slice;
        element.style.unitySliceBottom = slice;
        element.style.unitySliceScale = sliceScale;
    }

    private void Build()
    {
        root = document.rootVisualElement;
        if (root == null) { Debug.LogWarning("[TownRequestsPopupUITK] rootVisualElement null."); return; }
        root.pickingMode = PickingMode.Ignore;
        // Default root height is auto/content-sized; a Position.Absolute child (popupRoot) is out
        // of flow and contributes nothing, so root collapses to height 0 without this.
        root.style.height = Length.Percent(100);

        popupRoot = new VisualElement { name = "town-requests-root" };
        popupRoot.style.position = Position.Absolute;
        popupRoot.style.left = 0; popupRoot.style.right = 0; popupRoot.style.top = 0; popupRoot.style.bottom = 0;
        popupRoot.style.alignItems = Align.Center;
        popupRoot.style.justifyContent = Justify.Center;
        popupRoot.style.display = DisplayStyle.None;
        root.Add(popupRoot);

        backdrop = new VisualElement { name = "town-requests-backdrop" };
        backdrop.style.position = Position.Absolute;
        backdrop.style.left = 0; backdrop.style.right = 0; backdrop.style.top = 0; backdrop.style.bottom = 0;
        backdrop.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
        backdrop.pickingMode = PickingMode.Position;
        backdrop.RegisterCallback<ClickEvent>(_ => Close());
        popupRoot.Add(backdrop);

        // The board: wooden frame around a cork interior.
        VisualElement board = new VisualElement { name = "town-requests-board" };
        board.style.width = Length.Percent(96);
        board.style.maxWidth = 860;
        // Completing a request removes its note; without a floor the whole board would shrink and
        // re-centre under the player's finger. minHeight (not height) pins it at the common case
        // so it never shrinks, while still growing rather than clipping if three long 3-item notes
        // ever exceed it.
        board.style.minHeight = 1460;
        // Padding must clear the rendered frame border (slice 30 x scale 2 = 60px) with plenty to
        // spare. Top/bottom get more than left/right so the planks read as a real frame.
        board.style.paddingLeft = 110; board.style.paddingRight = 110;
        board.style.paddingTop = 124; board.style.paddingBottom = 132;
        ApplyFrame(board, boardFrame, 30, CorkBrown, 2f);
        popupRoot.Add(board);

        VisualElement header = new VisualElement { name = "header" };
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;
        // Generous gap so the first card's pin (which pokes above its card) clears the title row.
        header.style.marginBottom = 46;
        board.Add(header);

        Label title = new Label("TOWN REQUESTS");
        title.style.fontSize = 34;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.letterSpacing = 2;
        title.style.color = TitleBrown;
        header.Add(title);

        // Close: a real icon when one is wired, otherwise the "×" glyph.
        Button closeBtn = new Button(Close) { text = closeIcon != null ? string.Empty : "×" };
        closeBtn.style.width = 52; closeBtn.style.height = 52;
        closeBtn.style.fontSize = 32;
        closeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
        closeBtn.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        closeBtn.style.borderTopWidth = 0; closeBtn.style.borderBottomWidth = 0;
        closeBtn.style.borderLeftWidth = 0; closeBtn.style.borderRightWidth = 0;
        closeBtn.style.color = TitleBrown;
        if (closeIcon != null)
        {
            closeBtn.style.backgroundImage = new StyleBackground(closeIcon);
            // Contain, or a non-square source gets stretched into a smear.
            closeBtn.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
        }
        header.Add(closeBtn);

        slotsColumn = new VisualElement { name = "slots" };
        slotsColumn.style.flexDirection = FlexDirection.Column;
        slotsColumn.style.flexGrow = 1;
        // Spread the slots down the board so collected (short) slots leave even gaps instead of
        // piling at the top with a dead void underneath.
        slotsColumn.style.justifyContent = Justify.SpaceBetween;
        board.Add(slotsColumn);
    }

    private void BuildSlots()
    {
        if (slotsColumn == null || ReputationManager.Instance == null) return;
        slotsColumn.Clear();
        for (int slot = 0; slot < 3; slot++)
        {
            slotViews[slot] = new SlotView();
            slotsColumn.Add(BuildNote(slot));
        }
        RefreshLive();
    }

    private VisualElement BuildNote(int slot)
    {
        var rm = ReputationManager.Instance;
        SlotView view = slotViews[slot];

        // Wrapper carries the tilt so the pin can sit straight-ish on a tilted note.
        VisualElement wrapper = new VisualElement { name = "note-wrap-" + slot };
        wrapper.style.marginBottom = 18;
        wrapper.style.rotate = new StyleRotate(new Rotate(new Angle(slot % 2 == 0 ? -0.7f : 0.7f, AngleUnit.Degree)));

        bool completed = rm.IsSlotOnCooldown(slot);
        DeliveryRequest pending = rm.GetSlotRequest(slot);
        if (completed || pending == null || pending.items == null || pending.items.Length == 0)
        {
            BuildEmptySlot(wrapper, view, slot);
            return wrapper;
        }

        VisualElement note = new VisualElement { name = "slot-" + slot };
        ApplyFrame(note, noteFrame, 6, PaperCream);
        note.style.paddingLeft = 20; note.style.paddingRight = 20;
        note.style.paddingTop = 18; note.style.paddingBottom = 16;
        // Remember how tall this card ends up so the ghost that replaces it can match.
        int captured = slot;
        note.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            if (evt.newRect.height > 40f) lastCardHeight[captured] = evt.newRect.height;
        });
        wrapper.Add(note);

        // Pin
        VisualElement pin = new VisualElement { name = "pin" };
        pin.style.position = Position.Absolute;
        pin.style.top = -6; pin.style.left = Length.Percent(50);
        pin.style.marginLeft = -7;
        pin.style.width = 14; pin.style.height = 14;
        pin.style.backgroundColor = DifficultyColors[slot];
        pin.style.borderTopLeftRadius = 7; pin.style.borderTopRightRadius = 7;
        pin.style.borderBottomLeftRadius = 7; pin.style.borderBottomRightRadius = 7;
        wrapper.Add(pin);

        // Difficulty stamp, top-right — replaces the old coloured left stripe.
        Label stamp = BuildStamp(slot);
        stamp.style.position = Position.Absolute;
        stamp.style.right = 14; stamp.style.top = 12;
        note.Add(stamp);

        DeliveryRequest request = pending;

        // Header row: primary item icon anchor + requester line and blurb.
        VisualElement headRow = new VisualElement();
        headRow.style.flexDirection = FlexDirection.Row;
        headRow.style.alignItems = Align.FlexStart;
        headRow.style.marginTop = 6;
        headRow.style.marginRight = 90; // clear the stamp
        note.Add(headRow);

        VisualElement anchor = new VisualElement { name = "anchor" };
        anchor.style.width = 62; anchor.style.height = 62;
        anchor.style.marginRight = 12;
        anchor.style.flexShrink = 0;
        anchor.style.backgroundColor = new Color(0f, 0f, 0f, 0.06f);
        anchor.style.borderTopLeftRadius = 8; anchor.style.borderTopRightRadius = 8;
        anchor.style.borderBottomLeftRadius = 8; anchor.style.borderBottomRightRadius = 8;
        Sprite primary = ResolveIcon(request.items[0].itemId);
        if (primary != null)
        {
            anchor.style.backgroundImage = new StyleBackground(primary);
            anchor.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }
        headRow.Add(anchor);

        VisualElement headText = new VisualElement();
        headText.style.flexGrow = 1;
        headRow.Add(headText);

        Label who = new Label(string.IsNullOrEmpty(request.requesterName) ? "A neighbour" : request.requesterName);
        who.style.fontSize = 26;
        who.style.unityFontStyleAndWeight = FontStyle.Bold;
        who.style.color = Ink;
        headText.Add(who);

        if (!string.IsNullOrEmpty(request.flavorText))
        {
            Label blurb = new Label(request.flavorText);
            blurb.style.fontSize = 19;
            blurb.style.color = InkSoft;
            blurb.style.whiteSpace = WhiteSpace.Normal;
            blurb.style.marginTop = 2;
            headText.Add(blurb);
        }

        // One row per requested item: icon, name, have/need bar.
        VisualElement itemsBox = new VisualElement();
        itemsBox.style.marginTop = 12;
        note.Add(itemsBox);
        foreach (DeliveryLineItem line in request.items)
            itemsBox.Add(BuildLineRow(view, line));

        // Footer: reward inline + Submit, with Skip as the quieter option.
        VisualElement footer = new VisualElement();
        footer.style.flexDirection = FlexDirection.Row;
        footer.style.justifyContent = Justify.SpaceBetween;
        footer.style.alignItems = Align.Center;
        footer.style.marginTop = 14;
        note.Add(footer);

        // Boxed so the payoff reads as a distinct chip instead of blending into the paper.
        VisualElement rewardBox = new VisualElement();
        rewardBox.style.flexDirection = FlexDirection.Column;
        rewardBox.style.paddingLeft = 14; rewardBox.style.paddingRight = 16;
        rewardBox.style.paddingTop = 7; rewardBox.style.paddingBottom = 8;
        rewardBox.style.backgroundColor = new Color(Green.r, Green.g, Green.b, 0.10f);
        rewardBox.style.borderTopWidth = 2; rewardBox.style.borderBottomWidth = 2;
        rewardBox.style.borderLeftWidth = 2; rewardBox.style.borderRightWidth = 2;
        rewardBox.style.borderTopColor = Green; rewardBox.style.borderBottomColor = Green;
        rewardBox.style.borderLeftColor = Green; rewardBox.style.borderRightColor = Green;
        rewardBox.style.borderTopLeftRadius = 10; rewardBox.style.borderTopRightRadius = 10;
        rewardBox.style.borderBottomLeftRadius = 10; rewardBox.style.borderBottomRightRadius = 10;
        footer.Add(rewardBox);

        Label rewardCaption = new Label("REWARD");
        rewardCaption.style.fontSize = 13;
        rewardCaption.style.letterSpacing = 2;
        rewardCaption.style.unityFontStyleAndWeight = FontStyle.Bold;
        rewardCaption.style.color = Green;
        rewardBox.Add(rewardCaption);

        Label reward = new Label($"+{request.repReward} Reputation");
        reward.style.fontSize = 23;
        reward.style.unityFontStyleAndWeight = FontStyle.Bold;
        reward.style.color = Green;
        rewardBox.Add(reward);

        VisualElement buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        buttons.style.alignItems = Align.Center;
        footer.Add(buttons);

        // Skip: purple text matching the gem, with the real gem sprite as an icon child (UITK
        // labels cannot host inline sprites, so the cost is a row of label + image).
        int skipCost = rm.NextSkipCost;
        Button skipBtn = new Button(() => OnSkipClicked(slot)) { text = string.Empty };
        skipBtn.style.height = 52;
        skipBtn.style.marginRight = 10;
        skipBtn.style.paddingLeft = 14; skipBtn.style.paddingRight = 14;
        skipBtn.style.flexDirection = FlexDirection.Row;
        skipBtn.style.alignItems = Align.Center;
        skipBtn.style.backgroundColor = new Color(GemPurple.r, GemPurple.g, GemPurple.b, 0.10f);
        skipBtn.style.borderTopWidth = 0; skipBtn.style.borderBottomWidth = 0;
        skipBtn.style.borderLeftWidth = 0; skipBtn.style.borderRightWidth = 0;
        skipBtn.style.borderTopLeftRadius = 10; skipBtn.style.borderTopRightRadius = 10;
        skipBtn.style.borderBottomLeftRadius = 10; skipBtn.style.borderBottomRightRadius = 10;
        buttons.Add(skipBtn);
        if (request.isWelcomeBasket) skipBtn.style.display = DisplayStyle.None; // tutorial request: no skipping

        Label skipLabel = new Label($"Skip  {skipCost}");
        skipLabel.style.fontSize = 18;
        skipLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        skipLabel.style.color = GemPurple;
        skipBtn.Add(skipLabel);

        VisualElement gem = new VisualElement();
        gem.style.width = 22; gem.style.height = 22;
        gem.style.marginLeft = 5;
        gem.style.flexShrink = 0;
        if (gemIcon != null) gem.style.backgroundImage = new StyleBackground(gemIcon);
        else { gem.style.backgroundColor = GemPurple; gem.style.rotate = new StyleRotate(new Rotate(new Angle(45f, AngleUnit.Degree))); }
        skipBtn.Add(gem);

        Button submitBtn = new Button(() => OnSubmitClicked(slot, request)) { text = "Submit" };
        submitBtn.style.width = 176; submitBtn.style.height = 56;
        submitBtn.style.fontSize = 25;
        submitBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
        submitBtn.style.color = Color.white;
        submitBtn.style.borderTopWidth = 0; submitBtn.style.borderBottomWidth = 0;
        submitBtn.style.borderLeftWidth = 0; submitBtn.style.borderRightWidth = 0;
        submitBtn.style.borderTopLeftRadius = 12; submitBtn.style.borderTopRightRadius = 12;
        submitBtn.style.borderBottomLeftRadius = 12; submitBtn.style.borderBottomRightRadius = 12;
        buttons.Add(submitBtn);
        view.submit = submitBtn;

        return wrapper;
    }

    /// <summary>
    /// A completed slot: the note has been taken down, leaving the pin in the board and a ghost
    /// of the card — a semi-transparent dashed outline where the paper used to hang.
    /// </summary>
    private void BuildEmptySlot(VisualElement wrapper, SlotView view, int slot)
    {
        VisualElement ghost = new VisualElement { name = "slot-" + slot };
        ghost.style.height = lastCardHeight[slot] > 40f ? lastCardHeight[slot] : DefaultGhostHeight[slot];
        ghost.style.paddingLeft = 20; ghost.style.paddingRight = 20;
        ghost.style.paddingTop = 14; ghost.style.paddingBottom = 14;
        // UITK has no dashed-border style, so the outline is drawn directly.
        ghost.generateVisualContent += PaintDashedOutline;
        wrapper.Add(ghost);

        // The pin stays behind in the board.
        VisualElement pin = new VisualElement { name = "pin" };
        pin.style.position = Position.Absolute;
        pin.style.top = -6; pin.style.left = Length.Percent(50);
        pin.style.marginLeft = -7;
        pin.style.width = 14; pin.style.height = 14;
        pin.style.backgroundColor = DifficultyColors[slot];
        pin.style.borderTopLeftRadius = 7; pin.style.borderTopRightRadius = 7;
        pin.style.borderBottomLeftRadius = 7; pin.style.borderBottomRightRadius = 7;
        wrapper.Add(pin);

        // Top row mirrors the live card: status top-left, difficulty stamp top-right.
        VisualElement topRow = new VisualElement();
        topRow.style.flexDirection = FlexDirection.Row;
        topRow.style.justifyContent = Justify.SpaceBetween;
        topRow.style.alignItems = Align.FlexStart;
        ghost.Add(topRow);

        Label done = new Label("COMPLETED");
        done.style.fontSize = 17;
        done.style.unityFontStyleAndWeight = FontStyle.Bold;
        done.style.letterSpacing = 2;
        done.style.color = new Color(TitleBrown.r, TitleBrown.g, TitleBrown.b, 0.75f);
        topRow.Add(done);

        topRow.Add(BuildStamp(slot));

        // Countdown centred in the empty space, split over two lines so the ghost has some height.
        VisualElement centre = new VisualElement();
        centre.style.flexGrow = 1;
        centre.style.justifyContent = Justify.Center;
        centre.style.alignItems = Align.Center;
        ghost.Add(centre);

        Label caption = new Label("Next request");
        caption.style.fontSize = 19;
        caption.style.color = new Color(TitleBrown.r, TitleBrown.g, TitleBrown.b, 0.8f);
        centre.Add(caption);

        Label cd = new Label { name = "cooldown-label" };
        cd.style.fontSize = 26;
        cd.style.unityFontStyleAndWeight = FontStyle.Bold;
        cd.style.color = TitleBrown;
        cd.style.marginTop = 2;
        centre.Add(cd);
        view.cooldown = cd;
    }

    /// <summary>The difficulty stamp, shared by the live note and the completed ghost.</summary>
    private Label BuildStamp(int slot)
    {
        Label stamp = new Label(DifficultyNames[slot]);
        stamp.style.fontSize = 16;
        stamp.style.unityFontStyleAndWeight = FontStyle.Bold;
        stamp.style.letterSpacing = 2;
        stamp.style.color = DifficultyColors[slot];
        stamp.style.paddingLeft = 8; stamp.style.paddingRight = 8;
        stamp.style.paddingTop = 2; stamp.style.paddingBottom = 2;
        stamp.style.borderTopWidth = 2; stamp.style.borderBottomWidth = 2;
        stamp.style.borderLeftWidth = 2; stamp.style.borderRightWidth = 2;
        stamp.style.borderTopColor = DifficultyColors[slot]; stamp.style.borderBottomColor = DifficultyColors[slot];
        stamp.style.borderLeftColor = DifficultyColors[slot]; stamp.style.borderRightColor = DifficultyColors[slot];
        stamp.style.borderTopLeftRadius = 4; stamp.style.borderTopRightRadius = 4;
        stamp.style.borderBottomLeftRadius = 4; stamp.style.borderBottomRightRadius = 4;
        stamp.style.rotate = new StyleRotate(new Rotate(new Angle(-4f, AngleUnit.Degree)));
        return stamp;
    }

    // ── Dashed ghost outline ──────────────────────────────────────────────
    // UI Toolkit borders are solid-only, so the "card removed" outline is generated as geometry:
    // a rounded-rect path is walked at a fixed step, stroking only where the dash pattern is "on".

    private static void PaintDashedOutline(MeshGenerationContext ctx)
    {
        VisualElement ve = ctx.visualElement;
        float w = ve.layout.width, h = ve.layout.height;
        if (w < 8f || h < 8f) return;

        const float inset = 2f, radius = 14f, dash = 6f, gap = 7f, step = 1.5f;
        Rect rect = new Rect(inset, inset, w - inset * 2f, h - inset * 2f);

        List<Vector2> path = RoundedRectPath(rect, radius, 5);
        if (path.Count < 2) return;

        float[] cum = new float[path.Count];
        float total = 0f;
        for (int i = 1; i < path.Count; i++) { total += Vector2.Distance(path[i - 1], path[i]); cum[i] = total; }
        if (total <= 0f) return;

        var p = ctx.painter2D;
        p.lineWidth = 2f;
        p.lineCap = LineCap.Butt;
        p.strokeColor = new Color(TitleBrown.r, TitleBrown.g, TitleBrown.b, 0.45f);

        float period = dash + gap;
        bool drawing = false;
        for (float d = 0f; d <= total; d += step)
        {
            bool on = (d % period) < dash;
            Vector2 pt = PointAlong(path, cum, total, d);
            if (on)
            {
                if (!drawing) { p.BeginPath(); p.MoveTo(pt); drawing = true; }
                else p.LineTo(pt);
            }
            else if (drawing) { p.Stroke(); drawing = false; }
        }
        if (drawing) p.Stroke();
    }

    /// <summary>Rounded-rect as a polyline (UITK space is y-down), corners sampled per arc.</summary>
    private static List<Vector2> RoundedRectPath(Rect r, float rad, int seg)
    {
        rad = Mathf.Min(rad, Mathf.Min(r.width, r.height) * 0.5f);
        var pts = new List<Vector2>();

        void Arc(Vector2 c, float fromDeg, float toDeg)
        {
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, i / (float)seg) * Mathf.Deg2Rad;
                pts.Add(new Vector2(c.x + Mathf.Cos(a) * rad, c.y + Mathf.Sin(a) * rad));
            }
        }

        pts.Add(new Vector2(r.xMin + rad, r.yMin));
        pts.Add(new Vector2(r.xMax - rad, r.yMin));
        Arc(new Vector2(r.xMax - rad, r.yMin + rad), -90f, 0f);
        pts.Add(new Vector2(r.xMax, r.yMax - rad));
        Arc(new Vector2(r.xMax - rad, r.yMax - rad), 0f, 90f);
        pts.Add(new Vector2(r.xMin + rad, r.yMax));
        Arc(new Vector2(r.xMin + rad, r.yMax - rad), 90f, 180f);
        pts.Add(new Vector2(r.xMin, r.yMin + rad));
        Arc(new Vector2(r.xMin + rad, r.yMin + rad), 180f, 270f);
        pts.Add(new Vector2(r.xMin + rad, r.yMin));
        return pts;
    }

    private static Vector2 PointAlong(List<Vector2> path, float[] cum, float total, float d)
    {
        d = Mathf.Clamp(d, 0f, total);
        int lo = 0, hi = cum.Length - 1;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) / 2;
            if (cum[mid] <= d) lo = mid; else hi = mid;
        }
        float segLen = cum[hi] - cum[lo];
        float t = segLen <= 0.0001f ? 0f : (d - cum[lo]) / segLen;
        return Vector2.Lerp(path[lo], path[hi], t);
    }

    private VisualElement BuildLineRow(SlotView view, DeliveryLineItem line)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 7;

        VisualElement icon = new VisualElement();
        icon.style.width = 30; icon.style.height = 30;
        icon.style.flexShrink = 0;
        icon.style.marginRight = 9;
        Sprite sprite = ResolveIcon(line.itemId);
        if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite);
        row.Add(icon);

        Label name = new Label(RequestItemDisplay.Name(line.itemId));
        name.style.fontSize = 19;
        name.style.color = Ink;
        name.style.width = 132;
        name.style.flexShrink = 0;
        row.Add(name);

        // Track + fill
        VisualElement track = new VisualElement();
        track.style.flexGrow = 1;
        track.style.height = 20;
        track.style.overflow = Overflow.Hidden;
        ApplyFrame(track, barTrack, 3, TrackEmpty);
        if (barTrack == null)
        {
            track.style.borderTopLeftRadius = 6; track.style.borderTopRightRadius = 6;
            track.style.borderBottomLeftRadius = 6; track.style.borderBottomRightRadius = 6;
        }
        else
        {
            // The track sprite is near-white; on cream paper an empty bar would vanish, so knock
            // it down to a warm tan that still reads as "empty" against the fill.
            track.style.unityBackgroundImageTintColor = new Color(0.78f, 0.70f, 0.56f);
        }
        row.Add(track);

        VisualElement fill = new VisualElement();
        fill.style.height = Length.Percent(100);
        fill.style.width = Length.Percent(0);
        if (barFill != null)
        {
            fill.style.backgroundImage = new StyleBackground(barFill);
            fill.style.unityBackgroundImageTintColor = Green;
        }
        else fill.style.backgroundColor = Green;
        track.Add(fill);

        Label count = new Label();
        count.style.fontSize = 18;
        count.style.unityFontStyleAndWeight = FontStyle.Bold;
        count.style.color = Ink;
        count.style.marginLeft = 9;
        count.style.minWidth = 96;
        count.style.unityTextAlign = TextAnchor.MiddleRight;
        row.Add(count);

        view.lines.Add(new LineView { item = line, fill = fill, count = count });
        return row;
    }

    /// <summary>Refreshes bars, counts, Submit state and countdowns without rebuilding the notes.</summary>
    private void RefreshLive()
    {
        var rm = ReputationManager.Instance;
        if (rm == null) return;

        for (int slot = 0; slot < 3; slot++)
        {
            SlotView view = slotViews[slot];
            if (view == null) continue;

            // The "Next request" caption is a separate static line above this one.
            if (view.cooldown != null)
                view.cooldown.text = TimeFormat.Hms((float)rm.GetSlotCooldownRemainingSeconds(slot));

            bool all = view.lines.Count > 0;
            foreach (LineView lv in view.lines)
            {
                int held = DeliveryService.HeldCount(lv.item);
                int need = Mathf.Max(1, lv.item.count);
                bool ok = held >= need;
                if (!ok) all = false;

                // One green throughout: the fill is always Green, so progress reads the same whether
                // a line is part-done or finished; only the count text switches to Green on success.
                lv.fill.style.width = Length.Percent(Mathf.Clamp01(held / (float)need) * 100f);

                lv.count.text = $"{Mathf.Min(held, need)} / {need}";
                lv.count.style.color = ok ? Green : Ink;
            }

            if (view.submit != null)
            {
                view.submit.SetEnabled(all);
                view.submit.style.backgroundColor = all ? Green : new Color(0.62f, 0.58f, 0.50f);
            }
        }
    }

    /// <summary>Item icon for a request line: crops from the database, staples from the fields above.</summary>
    private Sprite ResolveIcon(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        if (itemId == "wood") return woodIcon;
        if (itemId == "compost") return compostIcon;
        if (itemId == "egg") return eggIcon;
        if (itemId.StartsWith("fish_")) return fishIcon;

        if (cropDatabase != null && cropDatabase.allCrops != null)
            foreach (CropData crop in cropDatabase.allCrops)
                if (crop != null && crop.cropName == itemId)
                    return crop.cropSprite != null ? crop.cropSprite : crop.harvestableSprite;
        return null;
    }

    private void OnSubmitClicked(int slot, DeliveryRequest request)
    {
        int reward = request.repReward;
        if (ReputationManager.Instance.TryFulfill(slot))
            ToastManager.Show("Request Fulfilled", $"+{reward} Reputation", ToastManager.ToastKind.Success);
    }

    private void OnSkipClicked(int slot)
    {
        ReputationManager.Instance.TrySkip(slot);
    }
}
