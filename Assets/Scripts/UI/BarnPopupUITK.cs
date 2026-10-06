using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The Barn: 7 skill tracks as 25-tick bars with a "+" spend button per row, a Renown bar
/// (total levels, max 175) up top, and clickable tier/milestone markers that show their bonus
/// in a floating tooltip anchored above the tapped marker. No respec. Fully code-driven
/// UIDocument (ToastManager pattern) with a wood-frame board background matching the Town
/// Requests board. Spec: 2026-07-19-reputation-design.md §4.
/// </summary>
[DefaultExecutionOrder(1050)]
public class BarnPopupUITK : MonoBehaviour
{
    public static BarnPopupUITK Instance { get; private set; }
    private const int SORT_ORDER = 1000;

    private static readonly (string name, Color color)[] TrackMeta =
    {
        ("Harvesting", new Color(0.86f, 0.72f, 0.20f)), // yellow
        ("Planting",   new Color(0.36f, 0.66f, 0.32f)), // green
        ("Watering",   new Color(0.25f, 0.70f, 0.75f)), // cyan
        ("Fishing",    new Color(0.30f, 0.50f, 0.85f)), // blue
        ("Forestry",   new Color(0.80f, 0.48f, 0.20f)), // orange
        ("Ranching",   new Color(0.80f, 0.32f, 0.30f)), // red
        ("Processing", new Color(0.60f, 0.35f, 0.75f)), // purple
    };

    // Plain-language "what does this skill do" for the info badge, plus the noun its percentage
    // applies to. The actual numbers come live from FarmSkillsManager.GetPerPoint, so retuning
    // the manager never leaves this text stale.
    private static readonly (string effect, string unit)[] TrackInfo =
    {
        ("Your crops sell for more money.",        "more money"),
        ("Your crops grow faster.",                "faster growing"),
        ("Watered soil stays wet longer.",         "longer-lasting water"),
        ("Fish bite sooner.",                      "shorter wait for a bite"),
        ("You get more wood from every chop.",     "more wood"),
        ("Eggs sell for more.",                    "more egg value"),
        ("The Cannery and Smokehouse cook faster.", "faster cooking"),
    };

    private static readonly Color RenownColor = new Color(0.78f, 0.35f, 0.85f);
    private static readonly Color TitleBrown = new Color(0.373f, 0.275f, 0.149f);
    // Warm near-black (not pure #000, which reads harsh against the wood/paper palette) for every
    // label and outline that used to be white — white was low-contrast on the light card interior.
    private static readonly Color InkBlack = new Color(0.12f, 0.08f, 0.05f);

    /// <summary>Darkened variant of a track colour for text — the raw palette (tuned for small
    /// vivid tick fills) reads too pale as label text against the card's light interior.</summary>
    private static Color Darken(Color c, float factor) => new Color(c.r * factor, c.g * factor, c.b * factor, c.a);

    [Tooltip("Shared RunewoodPanelSettings, cloned at runtime (ToastManager pattern) so text renders.")]
    [SerializeField] private PanelSettings sourcePanelSettings;

    [Header("Board art (all optional — solid colours are used when unassigned)")]
    [Tooltip("Wooden frame for the card, e.g. UI_Wood/UI_Wood_Frame_Standard_02 (same as the Town Requests board). Swap this to change the whole board look — tune Frame Slice / Frame Slice Scale to match the new sprite's border.")]
    [SerializeField] private Sprite boardFrame;
    [Tooltip("9-slice inset (px into the source sprite) for the board frame. Must be less than half the sprite's width/height. Thin-border sprites (e.g. Runewood FrameInfo, 48px) want ~8-10; the chunky 96px Wood_Standard wants ~30.")]
    [SerializeField] private int frameSlice = 30;
    [Tooltip("Multiplier on the rendered frame-border thickness. Lower = thinner planks / more interior. Standard wood reads well at ~1.5.")]
    [SerializeField] private float frameSliceScale = 1.5f;
    [Tooltip("Semi-transparent wash laid over the board interior to lighten/colorize it. UITK image tint only multiplies (can darken, never brighten), so a translucent overlay is how you make the frame lighter. White + ~0.35 alpha whitens the tan for punchier contrast; change the hue to colorize. Alpha 0 disables it.")]
    [SerializeField] private Color interiorWash = new Color(1f, 1f, 1f, 0.4f);
    [Tooltip("Renown progress bar track, e.g. UI_Book/UI_NoteBook_Bar01a.")]
    [SerializeField] private Sprite barTrack;
    [Tooltip("Renown progress bar fill, e.g. UI_Book/UI_NoteBook_BarFill01a.")]
    [SerializeField] private Sprite barFill;
    [Tooltip("Close button icon, e.g. UI_Wood/UI_Wood_Cross_Medium.png.")]
    [SerializeField] private Sprite closeIcon;
    [Tooltip("Small house icon shown left of the farm-name title, e.g. Buildings/Farmer_House_1_32x32.")]
    [SerializeField] private Sprite houseIcon;
    [Tooltip("Pixel font for the farm-name title — same UITK TextCore FontAsset as the catch toast (Fonts/UITK SDF/CayetanoRoundBold Pixel). Falls back to the default font when unassigned.")]
    [SerializeField] private UnityEngine.TextCore.Text.FontAsset titleFont;

    // Board art shared with other code-built boards (the Farmer's Almanac reuses it so both match).
    public Sprite BoardFrame => boardFrame;
    public int FrameSlice => frameSlice;
    public float FrameSliceScale => frameSliceScale;
    public Color InteriorWash => interiorWash;
    public Sprite BarTrack => barTrack;
    public Sprite BarFill => barFill;
    public Sprite CloseIcon => closeIcon;
    public UnityEngine.TextCore.Text.FontAsset TitleFont => titleFont;
    public PanelSettings SourcePanelSettings => sourcePanelSettings;

    private UIDocument document;
    private PanelSettings runtimePanelSettings;
    private VisualElement root;
    private VisualElement popupRoot;
    private VisualElement tracksColumn;
    private Label titleLabel;
    private Label pointsLabel;
    private VisualElement renownFill;
    private Label renownLabel;
    private FloatingTooltip tooltip;
    private bool isOpen;
    public bool IsOpen => isOpen;

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
        runtimePanelSettings.name = "BarnPanelSettings (runtime)";
        runtimePanelSettings.sortingOrder = SORT_ORDER;

        document.enabled = false;
        document.panelSettings = runtimePanelSettings;
        document.enabled = true;
    }

    private void Start()
    {
        Build();
        if (FarmSkillsManager.Instance != null) FarmSkillsManager.Instance.OnChanged += OnChanged;
        if (ReputationManager.Instance != null) ReputationManager.Instance.OnChanged += OnChanged;
        if (NarrativeManager.Instance != null) NarrativeManager.Instance.OnFarmNameChanged += UpdateTitle;
        UpdateTitle();
    }

    private void OnDestroy()
    {
        if (FarmSkillsManager.Instance != null) FarmSkillsManager.Instance.OnChanged -= OnChanged;
        if (ReputationManager.Instance != null) ReputationManager.Instance.OnChanged -= OnChanged;
        if (NarrativeManager.Instance != null) NarrativeManager.Instance.OnFarmNameChanged -= UpdateTitle;
        if (Instance == this) Instance = null;
        if (runtimePanelSettings != null) Destroy(runtimePanelSettings);
    }

    private void OnChanged() { if (isOpen) BuildContent(); }

    /// <summary>The board is titled with the player's farm name (this menu is the farm's own
    /// upgrade hub, not a generic "Barn"). Falls back to "Your Farm" before naming has happened.</summary>
    private void UpdateTitle()
    {
        if (titleLabel == null) return;
        string name = NarrativeManager.Instance != null ? NarrativeManager.Instance.FarmName : null;
        titleLabel.text = string.IsNullOrWhiteSpace(name) ? "Your Farm" : name;
    }

    public void Open()
    {
        if (isOpen || popupRoot == null) return;
        isOpen = true;
        root.pickingMode = PickingMode.Position;
        UpdateTitle();
        BuildContent();
        popupRoot.style.display = DisplayStyle.Flex;
        OnboardingTutorials.OnMenuOpened("tip_barn"); // one-time how-to (new players)
        OnboardingTutorials.OnBarnOpened();             // then, with a point to spend, spotlight a "+"
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        HideTooltip();
        if (popupRoot != null) popupRoot.style.display = DisplayStyle.None;
        if (root != null) root.pickingMode = PickingMode.Ignore;
    }

    /// <summary>
    /// Applies a sprite as a 9-sliced background, falling back to a flat colour. sliceScale scales
    /// the rendered border thickness — the board art is small (96px), so it needs scaling up to
    /// read as a chunky wooden frame at popup size. Mirrors TownRequestsPopupUITK.ApplyFrame.
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
        if (root == null) { Debug.LogWarning("[BarnPopupUITK] rootVisualElement null."); return; }
        root.pickingMode = PickingMode.Ignore;
        root.style.height = Length.Percent(100); // absolute children need this — see ReputationBarUITK/TownRequestsPopupUITK

        popupRoot = new VisualElement { name = "barn-root" };
        popupRoot.style.position = Position.Absolute;
        popupRoot.style.left = 0; popupRoot.style.right = 0; popupRoot.style.top = 0; popupRoot.style.bottom = 0;
        popupRoot.style.alignItems = Align.Center;
        popupRoot.style.justifyContent = Justify.Center;
        popupRoot.style.display = DisplayStyle.None;
        root.Add(popupRoot);

        VisualElement backdrop = new VisualElement { name = "barn-backdrop" };
        backdrop.style.position = Position.Absolute;
        backdrop.style.left = 0; backdrop.style.right = 0; backdrop.style.top = 0; backdrop.style.bottom = 0;
        backdrop.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
        backdrop.pickingMode = PickingMode.Position;
        backdrop.RegisterCallback<ClickEvent>(_ => Close());
        popupRoot.Add(backdrop);

        VisualElement card = new VisualElement { name = "barn-card" };
        // Matches TownRequestsPopupUITK's board width exactly (96% / maxWidth 860) — a wider
        // maxWidth (920, tried earlier) pushed the card past the visible area and off-center on
        // narrower/non-portrait windows instead of just capping cleanly.
        card.style.width = Length.Percent(96);
        card.style.maxWidth = 860;
        // A fixed minHeight (not just a maxHeight cap) so the card reads as a substantial board
        // filling the screen rather than shrink-wrapping to content — the flat-card version could
        // get away with that, but a small card floating in a sea of dimmed farm looks undersized
        // once it's got a wood frame drawing attention to its edges.
        card.style.minHeight = Length.Percent(72);
        card.style.maxHeight = Length.Percent(92);
        // Padding just clears the rendered frame border (frameSlice x frameSliceScale px on each
        // edge) plus a small margin, so content never grazes the planks. Deriving it from the frame
        // fields means swapping to a thinner frame automatically reclaims interior space — no need to
        // re-tune padding by hand when testing a different board sprite.
        int frameBorder = Mathf.RoundToInt(frameSlice * frameSliceScale);
        card.style.paddingLeft = frameBorder + 30; card.style.paddingRight = frameBorder + 30;
        card.style.paddingTop = frameBorder + 30; card.style.paddingBottom = frameBorder + 30;
        ApplyFrame(card, boardFrame, frameSlice, new Color(0.70f, 0.60f, 0.43f), frameSliceScale);
        popupRoot.Add(card);

        // Interior wash: a translucent overlay that lightens/colorizes the frame's tan fill (the
        // sprite itself can't be brightened by tint — tint only multiplies). Inset by the frame
        // border so it only covers the interior, and added first so it sits behind all content.
        if (interiorWash.a > 0f)
        {
            VisualElement wash = new VisualElement { name = "barn-wash" };
            wash.style.position = Position.Absolute;
            wash.style.left = frameBorder; wash.style.right = frameBorder;
            wash.style.top = frameBorder; wash.style.bottom = frameBorder;
            wash.style.backgroundColor = interiorWash;
            wash.style.borderTopLeftRadius = 6; wash.style.borderTopRightRadius = 6;
            wash.style.borderBottomLeftRadius = 6; wash.style.borderBottomRightRadius = 6;
            wash.pickingMode = PickingMode.Ignore;
            card.Add(wash);
        }

        VisualElement header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;
        // Never squeezed by the fixed-height card: a shrunk header let a long farm name wrap onto a
        // second line that then drew underneath the Overall Farm Level bar.
        header.style.flexShrink = 0;
        card.Add(header);

        // Farm name + a little house icon read as a single unit on the left. The title text is
        // filled in per-open from NarrativeManager.FarmName (see UpdateTitle) rather than hardcoded.
        VisualElement titleGroup = new VisualElement();
        titleGroup.style.flexDirection = FlexDirection.Row;
        titleGroup.style.alignItems = Align.Center;
        titleGroup.style.flexShrink = 1;
        titleGroup.style.flexGrow = 1;
        header.Add(titleGroup);

        if (houseIcon != null)
        {
            VisualElement houseEl = new VisualElement();
            houseEl.style.width = 46; houseEl.style.height = 46;
            houseEl.style.marginRight = 12;
            houseEl.style.flexShrink = 0;
            houseEl.style.backgroundImage = new StyleBackground(houseIcon);
            houseEl.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
            titleGroup.Add(houseEl);
        }

        titleLabel = new Label("Barn");
        titleLabel.style.fontSize = 41; // Munro Pixel 41's bake size: off-size pixel fonts smear
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
        titleLabel.style.color = TitleBrown;
        // One line, trimmed with "..." if a name is ever too long. Wrapping measured the pixel font
        // wrong: it broke after the first word and drew the rest underneath the Farm Level bar.
        titleLabel.style.whiteSpace = WhiteSpace.NoWrap;
        titleLabel.style.overflow = Overflow.Hidden;
        titleLabel.style.textOverflow = TextOverflow.Ellipsis;
        titleLabel.style.flexShrink = 1;
        // Take the whole row rather than the pixel font's own measured width, which comes out
        // narrower than it draws (that's what clipped the name early).
        titleLabel.style.flexGrow = 1;
        // Pixel font (same as the catch toast) so the farm name reads as a title, not body text.
        if (titleFont != null) titleLabel.style.unityFontDefinition = new StyleFontDefinition(titleFont);
        titleGroup.Add(titleLabel);

        // Close: a real icon when one is wired, otherwise the "×" glyph.
        Button closeBtn = new Button(Close) { text = closeIcon != null ? string.Empty : "×" };
        closeBtn.style.width = 58; closeBtn.style.height = 58;
        closeBtn.style.flexShrink = 0;
        closeBtn.style.marginLeft = 12;
        closeBtn.style.fontSize = 40;
        closeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
        closeBtn.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        closeBtn.style.borderTopWidth = 0; closeBtn.style.borderBottomWidth = 0;
        closeBtn.style.borderLeftWidth = 0; closeBtn.style.borderRightWidth = 0;
        closeBtn.style.color = TitleBrown;
        if (closeIcon != null)
        {
            closeBtn.style.backgroundImage = new StyleBackground(closeIcon);
            closeBtn.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
        }
        header.Add(closeBtn);
        header.style.marginBottom = 16;

        // The title, the Overall Farm Level bar, the milestones and the points count together form
        // the fixed "header" of the card. The 7 track rows added after them (tracksColumn) flex-grow
        // and distribute to fill the rest of the card down to the bottom edge — so the traits read as
        // the content body sitting beneath the header. Everything fits without scrolling at the card's
        // min/max height, so there's no ScrollView.

        // Renown bar (total levels 0-175). Milestone rewards are Phase 4 — notches are clickable
        // and honest about that ("Reward not yet implemented"), per user feedback on readability.
        VisualElement renownTrack = new VisualElement();
        renownTrack.style.height = 42;
        renownTrack.style.overflow = Overflow.Hidden;
        renownTrack.style.marginBottom = 6;
        ApplyFrame(renownTrack, barTrack, 3, new Color(0f, 0f, 0f, 0.2f));
        if (barTrack != null)
            // The track sprite is near-white; tint it tan so it doesn't wash out on the wood card.
            renownTrack.style.unityBackgroundImageTintColor = new Color(0.78f, 0.70f, 0.56f);
        card.Add(renownTrack);

        renownFill = new VisualElement();
        renownFill.style.height = Length.Percent(100);
        if (barFill != null)
        {
            renownFill.style.backgroundImage = new StyleBackground(barFill);
            renownFill.style.unityBackgroundImageTintColor = RenownColor;
        }
        else renownFill.style.backgroundColor = RenownColor;
        renownTrack.Add(renownFill);

        // "Overall Farm Level" name reads left, the X / 175 count reads right — two labels sharing
        // the track rather than one centered string, so both stay legible without crowding the
        // middle of the bar.
        Label renownTitle = new Label("Overall Farm Level");
        renownTitle.style.position = Position.Absolute;
        renownTitle.style.left = 10; renownTitle.style.top = 0; renownTitle.style.bottom = 0;
        renownTitle.style.color = InkBlack;
        renownTitle.style.fontSize = 24;
        renownTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        renownTitle.style.unityTextAlign = TextAnchor.MiddleLeft;
        renownTrack.Add(renownTitle);

        renownLabel = new Label();
        renownLabel.style.position = Position.Absolute;
        renownLabel.style.right = 10; renownLabel.style.top = 0; renownLabel.style.bottom = 0;
        renownLabel.style.color = InkBlack;
        renownLabel.style.fontSize = 26;
        renownLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        renownLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        renownTrack.Add(renownLabel);

        VisualElement renownNotches = new VisualElement();
        renownNotches.style.flexDirection = FlexDirection.Row;
        renownNotches.style.justifyContent = Justify.SpaceBetween;
        renownNotches.style.marginBottom = 20;
        int[] renownMilestones = { 10, 25, 50, 100, 175 };
        for (int i = 0; i < renownMilestones.Length; i++)
        {
            int threshold = renownMilestones[i];
            // The town's Free Gift chest grows with Farm Level; each milestone shows its coins.
            string gift = $"Free Gift: {FreeGiftManager.CoinsAtLevel(threshold):N0} coins.";
            string desc = threshold == 25 ? "Silo: increases inventory space. (Not yet implemented.)\n" + gift : gift;
            Button notch = new Button { text = threshold.ToString(), name = $"renown-notch-{threshold}" };
            notch.style.fontSize = 21;
            notch.style.height = 46;
            notch.style.minWidth = 52;
            notch.style.backgroundColor = new Color(0.35f, 0.22f, 0.1f);
            notch.style.color = Color.white;
            notch.style.borderTopWidth = 0; notch.style.borderBottomWidth = 0;
            notch.style.borderLeftWidth = 0; notch.style.borderRightWidth = 0;
            string notchTitle = threshold == 25 ? "Silo (Farm Level 25)" : $"Farm Level {threshold}";
            notch.clicked += () => ShowTooltip(notch, $"{notchTitle}\n{desc}");
            renownNotches.Add(notch);
        }
        card.Add(renownNotches);

        // Points to spend: a small right-aligned chip — the last line of the header block, visually
        // set apart from the plain header text by a rounded rectangle with a lighter fill + hairline.
        VisualElement pointsRow = new VisualElement();
        pointsRow.style.flexDirection = FlexDirection.Row;
        pointsRow.style.justifyContent = Justify.FlexEnd;
        pointsRow.style.marginBottom = 12;
        card.Add(pointsRow);

        pointsLabel = new Label();
        pointsLabel.style.fontSize = 20;
        pointsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        pointsLabel.style.color = new Color(0.15f, 0.35f, 0.1f);
        pointsLabel.style.paddingLeft = 12; pointsLabel.style.paddingRight = 12;
        pointsLabel.style.paddingTop = 5; pointsLabel.style.paddingBottom = 5;
        pointsLabel.style.backgroundColor = new Color(1f, 1f, 1f, 0.45f);
        pointsLabel.style.borderTopLeftRadius = 4; pointsLabel.style.borderTopRightRadius = 4;
        pointsLabel.style.borderBottomLeftRadius = 4; pointsLabel.style.borderBottomRightRadius = 4;
        pointsLabel.style.borderTopWidth = 1; pointsLabel.style.borderBottomWidth = 1;
        pointsLabel.style.borderLeftWidth = 1; pointsLabel.style.borderRightWidth = 1;
        Color pointsStroke = new Color(InkBlack.r, InkBlack.g, InkBlack.b, 0.5f);
        pointsLabel.style.borderTopColor = pointsStroke; pointsLabel.style.borderBottomColor = pointsStroke;
        pointsLabel.style.borderLeftColor = pointsStroke; pointsLabel.style.borderRightColor = pointsStroke;
        pointsRow.Add(pointsLabel);

        // The 7 tracks are the content: a block anchored to the bottom of the card (FlexEnd), leaving a
        // clear gap under the header so the two read as distinct sections. flexGrow claims the space;
        // the rows keep their own inter-row margins for spacing.
        tracksColumn = new VisualElement();
        tracksColumn.style.flexGrow = 1;
        tracksColumn.style.minHeight = 0;
        tracksColumn.style.justifyContent = Justify.FlexEnd;
        card.Add(tracksColumn);

        // Floating tooltip: added to popupRoot (not card) so it can float above the card edges
        // without being clipped, and positioned per-anchor in ShowTooltip below. Replaces the old
        // always-reserved description panel at the bottom of the card.
        tooltip = new FloatingTooltip(popupRoot, "barn-tooltip");
    }

    /// <summary>Shows (or, tapping the same anchor again, hides) the help bubble above `anchor`.</summary>
    private void ShowTooltip(VisualElement anchor, string text) => tooltip?.Toggle(anchor, text);

    private void HideTooltip() => tooltip?.Hide();

    /// <summary>The first track's "+" that can spend a point right now (null if none) — the Barn
    /// "spend your point" tutorial spotlights it.</summary>
    public VisualElement FirstSpendableButton { get; private set; }

    private void BuildContent()
    {
        FirstSpendableButton = null;
        var fs = FarmSkillsManager.Instance;
        var rm = ReputationManager.Instance;
        if (fs == null || rm == null || tracksColumn == null) return;

        pointsLabel.text = $"Points to spend: {rm.UnspentPoints}";

        int totalLevels = fs.TotalLevels;
        renownFill.style.width = Length.Percent(totalLevels / 175f * 100f);
        renownLabel.text = $"{totalLevels} / 175";

        tracksColumn.Clear();
        VisualElement lastRow = null;
        foreach (FarmSkillTrack track in System.Enum.GetValues(typeof(FarmSkillTrack)))
        {
            lastRow = BuildTrackRow(track);
            tracksColumn.Add(lastRow);
        }
        // The block is bottom-anchored (FlexEnd); drop the trailing row's margin so its gap to the
        // card's bottom edge equals the side padding rather than that plus a row margin.
        if (lastRow != null) lastRow.style.marginBottom = 0;
    }

    private VisualElement BuildTrackRow(FarmSkillTrack track)
    {
        var fs = FarmSkillsManager.Instance;
        var meta = TrackMeta[(int)track];
        int level = fs.GetLevel(track);

        // tracksColumn stacks the rows as a block at the bottom (FlexEnd), so each row carries its own
        // gap to the next; BuildContent zeroes the last row's bottom margin so the block sits flush
        // against the bottom padding.
        VisualElement row = new VisualElement();
        row.style.marginBottom = 18;

        // Name plus a small "i" badge for what the track actually buys you — tried flexing the
        // bonus text to the row's far right first, but the ScrollView's reserved scrollbar-track
        // width made the row's true right edge narrower than it looked (ticks don't stretch to
        // fill, so nothing else revealed the mismatch), and the text quietly clipped. A tap-to-
        // reveal badge next to the name sidesteps needing to know that edge at all.
        VisualElement nameRow = new VisualElement();
        nameRow.style.flexDirection = FlexDirection.Row;
        nameRow.style.alignItems = Align.Center;
        nameRow.style.marginBottom = 8;
        row.Add(nameRow);

        Label nameLabel = new Label($"{meta.name}  ({level}/{FarmSkillsCore.MaxLevel})");
        nameLabel.style.fontSize = 28;
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        // The raw track colour is tuned for small vivid tick fills — as label text against the
        // card's light interior it read too pale, hence the darkened variant here.
        nameLabel.style.color = Darken(meta.color, 0.6f);
        nameRow.Add(nameLabel);

        Button infoBadge = new Button { text = "i" };
        infoBadge.style.width = 32; infoBadge.style.height = 32;
        infoBadge.style.marginLeft = 12;
        infoBadge.style.flexShrink = 0;
        infoBadge.style.fontSize = 19;
        infoBadge.style.unityFontStyleAndWeight = FontStyle.BoldAndItalic;
        // Every info badge shares one colour (defined with the Almanac's) so "i" reads as a single,
        // recognisable "tap for details" control rather than part of each track's colour coding.
        infoBadge.style.color = AlmanacPopupUITK.InfoBadgeText;
        infoBadge.style.backgroundColor = AlmanacPopupUITK.InfoBadgeColor;
        infoBadge.style.borderTopWidth = 1; infoBadge.style.borderBottomWidth = 1;
        infoBadge.style.borderLeftWidth = 1; infoBadge.style.borderRightWidth = 1;
        infoBadge.style.borderTopColor = InkBlack; infoBadge.style.borderBottomColor = InkBlack;
        infoBadge.style.borderLeftColor = InkBlack; infoBadge.style.borderRightColor = InkBlack;
        infoBadge.style.borderTopLeftRadius = 16; infoBadge.style.borderTopRightRadius = 16;
        infoBadge.style.borderBottomLeftRadius = 16; infoBadge.style.borderBottomRightRadius = 16;
        infoBadge.style.paddingLeft = 0; infoBadge.style.paddingRight = 0;
        infoBadge.style.paddingTop = 0; infoBadge.style.paddingBottom = 0;
        infoBadge.clicked += () => ShowTooltip(infoBadge, BuildTrackInfoText(track));
        nameRow.Add(infoBadge);

        VisualElement tickRow = new VisualElement();
        tickRow.style.flexDirection = FlexDirection.Row;
        tickRow.style.alignItems = Align.Center;
        row.Add(tickRow);

        VisualElement ticksWrap = new VisualElement();
        ticksWrap.style.flexDirection = FlexDirection.Row;
        // Fill the row's full width and spread the 25 ticks/markers evenly across it (SpaceBetween),
        // matching the milestone bar above — instead of clumping them at the left with dead space on
        // the right. The per-tick margins are dropped (set to 0 below) so the gaps come purely from
        // the even distribution.
        ticksWrap.style.flexGrow = 1;
        ticksWrap.style.flexWrap = Wrap.Wrap;
        ticksWrap.style.justifyContent = Justify.SpaceBetween;
        ticksWrap.style.alignItems = Align.Center;
        tickRow.Add(ticksWrap);

        for (int lvl = 1; lvl <= FarmSkillsCore.MaxLevel; lvl++)
        {
            bool isTier = System.Array.IndexOf(FarmSkillsManager.TierMarkers, lvl) >= 0;

            if (isTier)
            {
                // A tier level gets one bigger, square, numbered marker instead of a regular tick —
                // both showing which level it is and doubling as the clickable "what does this
                // unlock" affordance (previously a plain "|" glyph button next to the tick).
                int tierLevel = lvl;
                bool unlocked = fs.IsTierUnlocked(track, tierLevel);

                Button tierBtn = new Button { text = tierLevel.ToString() };
                tierBtn.style.width = 42; tierBtn.style.height = 42;
                tierBtn.style.fontSize = 21;
                tierBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
                tierBtn.style.color = InkBlack;
                // Locked used to be a near-invisible 25%-alpha wash; darkening the fill instead of
                // just fading it keeps the box readable as a box while still reading "dimmer" than
                // the vivid unlocked fill.
                tierBtn.style.backgroundColor = unlocked ? meta.color : Darken(meta.color, 0.7f);
                tierBtn.style.borderTopWidth = 2; tierBtn.style.borderBottomWidth = 2;
                tierBtn.style.borderLeftWidth = 2; tierBtn.style.borderRightWidth = 2;
                Color ring = unlocked ? InkBlack : new Color(InkBlack.r, InkBlack.g, InkBlack.b, 0.45f);
                tierBtn.style.borderTopColor = ring; tierBtn.style.borderBottomColor = ring;
                tierBtn.style.borderLeftColor = ring; tierBtn.style.borderRightColor = ring;
                tierBtn.style.borderTopLeftRadius = 10; tierBtn.style.borderTopRightRadius = 10;
                tierBtn.style.borderBottomLeftRadius = 10; tierBtn.style.borderBottomRightRadius = 10;
                tierBtn.clicked += () => OnTierClicked(track, tierLevel, tierBtn);
                ticksWrap.Add(tierBtn);
            }
            else
            {
                bool filled = lvl <= level;
                VisualElement tick = new VisualElement();
                tick.style.width = 18;
                tick.style.height = 34;
                tick.style.borderTopLeftRadius = 4; tick.style.borderTopRightRadius = 4;
                tick.style.borderBottomLeftRadius = 4; tick.style.borderBottomRightRadius = 4;
                tick.style.backgroundColor = filled ? meta.color : new Color(meta.color.r, meta.color.g, meta.color.b, 0.25f);
                ticksWrap.Add(tick);
            }
        }

        bool canLevel = level < FarmSkillsCore.MaxLevel && ReputationManager.Instance.UnspentPoints > 0;
        Button plusBtn = new Button(() => OnPlusClicked(track)) { text = "+" };
        plusBtn.style.width = 56; plusBtn.style.height = 44;
        plusBtn.style.marginLeft = 8;
        plusBtn.style.flexShrink = 0;
        plusBtn.style.fontSize = 30;
        plusBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
        plusBtn.style.backgroundColor = canLevel ? meta.color : new Color(0.6f, 0.6f, 0.6f);
        plusBtn.style.color = Color.white;
        plusBtn.style.borderTopWidth = 0; plusBtn.style.borderBottomWidth = 0;
        plusBtn.style.borderLeftWidth = 0; plusBtn.style.borderRightWidth = 0;
        plusBtn.style.borderTopLeftRadius = 10; plusBtn.style.borderTopRightRadius = 10;
        plusBtn.style.borderBottomLeftRadius = 10; plusBtn.style.borderBottomRightRadius = 10;
        plusBtn.SetEnabled(canLevel);
        tickRow.Add(plusBtn);
        if (canLevel && FirstSpendableButton == null) FirstSpendableButton = plusBtn; // tutorial spotlight target

        return row;
    }

    /// <summary>Info-badge text: what the skill does in plain words, what one level adds, what the
    /// player has right now, and what maxing it gives — no "/lvl" shorthand.</summary>
    private static string BuildTrackInfoText(FarmSkillTrack track)
    {
        var fs = FarmSkillsManager.Instance;
        var info = TrackInfo[(int)track];
        int level = fs.GetLevel(track);
        float perLevel = fs.GetPerPoint(track) * 100f;
        return $"<b>{TrackMeta[(int)track].name}</b>\n{info.effect}\n\n"
             + $"Each level: +{perLevel:0.#}% {info.unit}\n"
             + $"You have now: +{perLevel * level:0.#}% (level {level})\n"
             + $"At level {FarmSkillsCore.MaxLevel}: +{perLevel * FarmSkillsCore.MaxLevel:0.#}%";
    }

    private void OnTierClicked(FarmSkillTrack track, int tierLevel, VisualElement anchor)
    {
        var fs = FarmSkillsManager.Instance;
        int level = fs.GetLevel(track);
        string status = fs.IsTierUnlocked(track, tierLevel)
            ? "Unlocked!"
            : $"Reach level {tierLevel} to unlock (you're level {level}).";
        ShowTooltip(anchor, $"<b>{TrackMeta[(int)track].name} - Level {tierLevel}</b>\n{DescribeMilestone(track, tierLevel)}\n\n{status}");
    }

    /// <summary>
    /// Exactly what a milestone gives, with numbers read live from FarmSkillsManager so the text
    /// always matches gameplay. Levels 5/10/15/20 grow one perk per track (the text names the
    /// total at that milestone, not the increment); level 25 is the track's unique capstone.
    /// </summary>
    private static string DescribeMilestone(FarmSkillTrack track, int tierLevel)
    {
        var fs = FarmSkillsManager.Instance;
        if (tierLevel >= FarmSkillsCore.MaxLevel)
        {
            return track switch
            {
                FarmSkillTrack.Harvesting => $"Golden crops: {fs.GoldenCropChanceValue * 100f:0.#}% chance a harvest pays {fs.GoldenCropMultiplierValue}x money and coins.",
                FarmSkillTrack.Planting   => $"Head start: {fs.InstantSproutChanceValue * 100f:0.#}% chance a new seed starts as a sprout.",
                FarmSkillTrack.Watering   => "Rain fully waters every crop.",
                FarmSkillTrack.Fishing    => $"Rare fish (Bass and Pike) are {fs.RareFishMultiplierValue:0.#}x as likely.",
                FarmSkillTrack.Forestry   => $"Trees regrow {fs.TreeRegrowMultiplierValue:0.#}x as fast.",
                FarmSkillTrack.Ranching   => $"Your dog runs {(fs.DogSpeedMultiplierValue - 1f) * 100f:0.#}% faster.",
                FarmSkillTrack.Processing => $"Cooking and smoking get an extra +{fs.ProcessingCapstoneSpeedValue * 100f:0.#}% speed.",
                _ => string.Empty,
            };
        }

        int steps = FarmSkillsCore.GrowingTiersReached(tierLevel);
        float step = track == FarmSkillTrack.Watering ? fs.SprinklerReachStep : fs.MilestoneChanceStep;
        float pct = steps * step * 100f;
        string upFrom = steps > 1 ? $" (up from {(steps - 1) * step * 100f:0.#}%)" : string.Empty;
        string perk = track switch
        {
            FarmSkillTrack.Harvesting => $"{pct:0.#}% chance a harvest pays double money and coins",
            FarmSkillTrack.Planting   => $"{pct:0.#}% chance a harvest gives its seed back free",
            FarmSkillTrack.Watering   => $"Sprinklers reach {pct:0.#}% farther",
            FarmSkillTrack.Fishing    => $"{pct:0.#}% chance to catch 2 fish at once",
            FarmSkillTrack.Forestry   => $"{pct:0.#}% chance a chop gives double wood",
            FarmSkillTrack.Ranching   => $"{pct:0.#}% chance an egg counts double",
            FarmSkillTrack.Processing => $"{pct:0.#}% chance a finished jar or smoked fish comes out as two",
            _ => string.Empty,
        };
        return perk + upFrom + ".";
    }

    private void OnPlusClicked(FarmSkillTrack track)
    {
        FarmSkillsManager.Instance.TryLevelUp(track);
    }
}
