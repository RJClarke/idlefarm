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

    private static readonly string[] TierFlavor =
    {
        "double-harvest chance", "free-seed chance", "sprinkler radius +1", "2x whirlpools",
        "double wood knockdown", "second-egg chance", "double-jar chance",
    };

    // What each point actually buys, shown right on the row — mirrors FarmSkillsManager's
    // per-point values exactly (harvestingPerPoint=0.04, plantingPerPoint=0.02, etc.); update
    // both together if those tuning values ever change.
    private static readonly string[] TrackBonusText =
    {
        "+4%/lvl Money", "+2%/lvl Growth", "+3%/lvl Moisture", "+2%/lvl Faster Bites",
        "+2%/lvl Wood", "+3%/lvl Egg Value", "+2%/lvl Speed",
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
    [Tooltip("Wooden frame for the card, e.g. UI_Wood/UI_Wood_Frame_Standard_02 (same as the Town Requests board).")]
    [SerializeField] private Sprite boardFrame;
    [Tooltip("Renown progress bar track, e.g. UI_Book/UI_NoteBook_Bar01a.")]
    [SerializeField] private Sprite barTrack;
    [Tooltip("Renown progress bar fill, e.g. UI_Book/UI_NoteBook_BarFill01a.")]
    [SerializeField] private Sprite barFill;
    [Tooltip("Close button icon, e.g. UI_Wood/UI_Wood_Cross_Medium.png.")]
    [SerializeField] private Sprite closeIcon;

    private UIDocument document;
    private PanelSettings runtimePanelSettings;
    private VisualElement root;
    private VisualElement popupRoot;
    private VisualElement tracksColumn;
    private Label pointsLabel;
    private VisualElement renownFill;
    private Label renownLabel;
    private VisualElement tooltip;
    private Label tooltipLabel;
    private VisualElement tooltipAnchor;
    private IVisualElementScheduledItem tooltipHideTimer;
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
    }

    private void OnDestroy()
    {
        if (FarmSkillsManager.Instance != null) FarmSkillsManager.Instance.OnChanged -= OnChanged;
        if (ReputationManager.Instance != null) ReputationManager.Instance.OnChanged -= OnChanged;
        if (Instance == this) Instance = null;
        if (runtimePanelSettings != null) Destroy(runtimePanelSettings);
    }

    private void OnChanged() { if (isOpen) BuildContent(); }

    public void Open()
    {
        if (isOpen || popupRoot == null) return;
        isOpen = true;
        root.pickingMode = PickingMode.Position;
        BuildContent();
        popupRoot.style.display = DisplayStyle.Flex;
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
        // Padding clears the rendered frame border (slice 30 x scale 2 = 60px on every edge) with
        // a little extra so content never grazes the planks.
        card.style.paddingLeft = 64; card.style.paddingRight = 64;
        card.style.paddingTop = 70; card.style.paddingBottom = 70;
        ApplyFrame(card, boardFrame, 30, new Color(0.70f, 0.60f, 0.43f), 2f);
        popupRoot.Add(card);

        VisualElement header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;
        card.Add(header);

        Label title = new Label("Barn");
        title.style.fontSize = 34;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color = TitleBrown;
        header.Add(title);

        // Close: a real icon when one is wired, otherwise the "×" glyph.
        Button closeBtn = new Button(Close) { text = closeIcon != null ? string.Empty : "×" };
        closeBtn.style.width = 48; closeBtn.style.height = 48;
        closeBtn.style.fontSize = 30;
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

        // 7 tracks don't reliably fit under the card's maxHeight, so everything below the header
        // scrolls in its own body — without this, content overflowed straight through the wood
        // frame's bottom plank instead of being contained by it. minHeight=0 overrides Yoga's
        // default (a flex child won't shrink below its content size otherwise), letting the
        // ScrollView actually claim only the space left after the fixed header.
        var body = new ScrollView(ScrollViewMode.Vertical) { name = "barn-body" };
        body.style.flexGrow = 1;
        body.style.minHeight = 0;
        body.style.marginTop = 6;
        card.Add(body);

        pointsLabel = new Label();
        pointsLabel.style.fontSize = 24;
        pointsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        pointsLabel.style.color = new Color(0.15f, 0.35f, 0.1f);
        pointsLabel.style.marginTop = 4; pointsLabel.style.marginBottom = 10;
        body.Add(pointsLabel);

        // Renown bar (total levels 0-175). Milestone rewards are Phase 4 — notches are clickable
        // and honest about that ("Reward not yet implemented"), per user feedback on readability.
        VisualElement renownTrack = new VisualElement();
        renownTrack.style.height = 28;
        renownTrack.style.overflow = Overflow.Hidden;
        renownTrack.style.marginBottom = 4;
        ApplyFrame(renownTrack, barTrack, 3, new Color(0f, 0f, 0f, 0.2f));
        if (barTrack != null)
            // The track sprite is near-white; tint it tan so it doesn't wash out on the wood card.
            renownTrack.style.unityBackgroundImageTintColor = new Color(0.78f, 0.70f, 0.56f);
        body.Add(renownTrack);

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
        renownTitle.style.fontSize = 16;
        renownTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        renownTitle.style.unityTextAlign = TextAnchor.MiddleLeft;
        renownTrack.Add(renownTitle);

        renownLabel = new Label();
        renownLabel.style.position = Position.Absolute;
        renownLabel.style.right = 10; renownLabel.style.top = 0; renownLabel.style.bottom = 0;
        renownLabel.style.color = InkBlack;
        renownLabel.style.fontSize = 18;
        renownLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        renownLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        renownTrack.Add(renownLabel);

        VisualElement renownNotches = new VisualElement();
        renownNotches.style.flexDirection = FlexDirection.Row;
        renownNotches.style.justifyContent = Justify.SpaceBetween;
        renownNotches.style.marginBottom = 14;
        int[] renownMilestones = { 10, 25, 50, 100, 175 };
        string[] renownDesc =
        {
            "Reward not yet implemented.",
            "Silo: increases inventory space. (Not yet implemented.)",
            "Reward not yet implemented.",
            "Reward not yet implemented.",
            "Reward not yet implemented.",
        };
        for (int i = 0; i < renownMilestones.Length; i++)
        {
            int threshold = renownMilestones[i];
            string desc = renownDesc[i];
            Button notch = new Button { text = threshold.ToString() };
            notch.style.fontSize = 14;
            notch.style.height = 30;
            notch.style.backgroundColor = new Color(0.35f, 0.22f, 0.1f);
            notch.style.color = Color.white;
            notch.style.borderTopWidth = 0; notch.style.borderBottomWidth = 0;
            notch.style.borderLeftWidth = 0; notch.style.borderRightWidth = 0;
            string notchTitle = threshold == 25 ? "Silo (Farm Level 25)" : $"Farm Level {threshold}";
            notch.clicked += () => ShowTooltip(notch, $"{notchTitle}\n{desc}");
            renownNotches.Add(notch);
        }
        body.Add(renownNotches);

        tracksColumn = new VisualElement();
        body.Add(tracksColumn);

        // Floating tooltip: added to popupRoot (not card) so it can float above the card edges
        // without being clipped, and positioned per-anchor in ShowTooltip below. Replaces the old
        // always-reserved description panel at the bottom of the card.
        tooltip = new VisualElement { name = "barn-tooltip" };
        tooltip.style.position = Position.Absolute;
        tooltip.style.display = DisplayStyle.None;
        tooltip.style.maxWidth = 280;
        tooltip.style.paddingLeft = 14; tooltip.style.paddingRight = 14;
        tooltip.style.paddingTop = 10; tooltip.style.paddingBottom = 10;
        tooltip.style.backgroundColor = new Color(0.16f, 0.10f, 0.05f, 0.95f);
        tooltip.style.borderTopLeftRadius = 10; tooltip.style.borderTopRightRadius = 10;
        tooltip.style.borderBottomLeftRadius = 10; tooltip.style.borderBottomRightRadius = 10;
        // Centers horizontally on the anchor and sits with its bottom edge at the anchor's top,
        // without needing to pre-measure the tooltip's own size (percentages are relative to it).
        tooltip.style.translate = new StyleTranslate(new Translate(Length.Percent(-50), Length.Percent(-100)));
        tooltip.pickingMode = PickingMode.Ignore;
        popupRoot.Add(tooltip);

        tooltipLabel = new Label();
        tooltipLabel.style.color = Color.white;
        tooltipLabel.style.fontSize = 16;
        tooltipLabel.style.whiteSpace = WhiteSpace.Normal;
        tooltipLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        tooltip.Add(tooltipLabel);
    }

    /// <summary>Shows (or, tapping the same anchor again, hides) a tooltip floating above `anchor`.
    /// Auto-hides after a few seconds so it never needs its own close affordance.</summary>
    private void ShowTooltip(VisualElement anchor, string text)
    {
        if (tooltip == null || popupRoot == null) return;

        if (tooltipAnchor == anchor && tooltip.style.display == DisplayStyle.Flex)
        {
            HideTooltip();
            return;
        }

        tooltipAnchor = anchor;
        tooltipLabel.text = text;
        tooltip.style.display = DisplayStyle.Flex;

        Vector2 topCenterWorld = new Vector2(anchor.worldBound.center.x, anchor.worldBound.yMin);
        Vector2 local = popupRoot.WorldToLocal(topCenterWorld);
        tooltip.style.left = local.x;
        tooltip.style.top = local.y - 10f;

        tooltipHideTimer?.Pause();
        tooltipHideTimer = tooltip.schedule.Execute(HideTooltip).StartingIn(3500);
    }

    private void HideTooltip()
    {
        if (tooltip == null) return;
        tooltip.style.display = DisplayStyle.None;
        tooltipAnchor = null;
    }

    private void BuildContent()
    {
        var fs = FarmSkillsManager.Instance;
        var rm = ReputationManager.Instance;
        if (fs == null || rm == null || tracksColumn == null) return;

        pointsLabel.text = $"Points to spend: {rm.UnspentPoints}";

        int totalLevels = fs.TotalLevels;
        renownFill.style.width = Length.Percent(totalLevels / 175f * 100f);
        renownLabel.text = $"{totalLevels} / 175";

        tracksColumn.Clear();
        foreach (FarmSkillTrack track in System.Enum.GetValues(typeof(FarmSkillTrack)))
            tracksColumn.Add(BuildTrackRow(track));
    }

    private VisualElement BuildTrackRow(FarmSkillTrack track)
    {
        var fs = FarmSkillsManager.Instance;
        var meta = TrackMeta[(int)track];
        int level = fs.GetLevel(track);

        VisualElement row = new VisualElement();
        row.style.marginBottom = 14;

        // Name plus a small "i" badge for what the track actually buys you — tried flexing the
        // bonus text to the row's far right first, but the ScrollView's reserved scrollbar-track
        // width made the row's true right edge narrower than it looked (ticks don't stretch to
        // fill, so nothing else revealed the mismatch), and the text quietly clipped. A tap-to-
        // reveal badge next to the name sidesteps needing to know that edge at all.
        VisualElement nameRow = new VisualElement();
        nameRow.style.flexDirection = FlexDirection.Row;
        nameRow.style.alignItems = Align.Center;
        nameRow.style.marginBottom = 4;
        row.Add(nameRow);

        Label nameLabel = new Label($"{meta.name}  ({level}/{FarmSkillsCore.MaxLevel})");
        nameLabel.style.fontSize = 20;
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        // The raw track colour is tuned for small vivid tick fills — as label text against the
        // card's light interior it read too pale, hence the darkened variant here.
        nameLabel.style.color = Darken(meta.color, 0.6f);
        nameRow.Add(nameLabel);

        Button infoBadge = new Button { text = "i" };
        infoBadge.style.width = 22; infoBadge.style.height = 22;
        infoBadge.style.marginLeft = 8;
        infoBadge.style.fontSize = 14;
        infoBadge.style.unityFontStyleAndWeight = FontStyle.BoldAndItalic;
        infoBadge.style.color = InkBlack;
        infoBadge.style.backgroundColor = Darken(meta.color, 0.85f);
        infoBadge.style.borderTopWidth = 1; infoBadge.style.borderBottomWidth = 1;
        infoBadge.style.borderLeftWidth = 1; infoBadge.style.borderRightWidth = 1;
        infoBadge.style.borderTopColor = InkBlack; infoBadge.style.borderBottomColor = InkBlack;
        infoBadge.style.borderLeftColor = InkBlack; infoBadge.style.borderRightColor = InkBlack;
        infoBadge.style.borderTopLeftRadius = 11; infoBadge.style.borderTopRightRadius = 11;
        infoBadge.style.borderBottomLeftRadius = 11; infoBadge.style.borderBottomRightRadius = 11;
        infoBadge.style.paddingLeft = 0; infoBadge.style.paddingRight = 0;
        infoBadge.style.paddingTop = 0; infoBadge.style.paddingBottom = 0;
        string bonusText = TrackBonusText[(int)track];
        infoBadge.clicked += () => ShowTooltip(infoBadge, $"{meta.name}\n{bonusText}");
        nameRow.Add(infoBadge);

        VisualElement tickRow = new VisualElement();
        tickRow.style.flexDirection = FlexDirection.Row;
        tickRow.style.alignItems = Align.Center;
        row.Add(tickRow);

        VisualElement ticksWrap = new VisualElement();
        ticksWrap.style.flexDirection = FlexDirection.Row;
        // No flexGrow: this used to stretch to fill the row, which just pushed a big empty gap
        // between the last tick and the "+" button instead of sitting snug next to it.
        ticksWrap.style.flexWrap = Wrap.Wrap;
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
                tierBtn.style.width = 34; tierBtn.style.height = 34;
                tierBtn.style.marginRight = 2;
                tierBtn.style.fontSize = 15;
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
                tierBtn.style.borderTopLeftRadius = 8; tierBtn.style.borderTopRightRadius = 8;
                tierBtn.style.borderBottomLeftRadius = 8; tierBtn.style.borderBottomRightRadius = 8;
                tierBtn.clicked += () => OnTierClicked(track, tierLevel, tierBtn);
                ticksWrap.Add(tierBtn);
            }
            else
            {
                bool filled = lvl <= level;
                VisualElement tick = new VisualElement();
                tick.style.width = 16;
                tick.style.height = 26;
                tick.style.marginRight = 2;
                tick.style.borderTopLeftRadius = 3; tick.style.borderTopRightRadius = 3;
                tick.style.borderBottomLeftRadius = 3; tick.style.borderBottomRightRadius = 3;
                tick.style.backgroundColor = filled ? meta.color : new Color(meta.color.r, meta.color.g, meta.color.b, 0.25f);
                ticksWrap.Add(tick);
            }
        }

        bool canLevel = level < FarmSkillsCore.MaxLevel && ReputationManager.Instance.UnspentPoints > 0;
        Button plusBtn = new Button(() => OnPlusClicked(track)) { text = "+" };
        plusBtn.style.width = 44; plusBtn.style.height = 34;
        plusBtn.style.marginLeft = 4;
        plusBtn.style.fontSize = 22;
        plusBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
        plusBtn.style.backgroundColor = canLevel ? meta.color : new Color(0.6f, 0.6f, 0.6f);
        plusBtn.style.color = Color.white;
        plusBtn.style.borderTopWidth = 0; plusBtn.style.borderBottomWidth = 0;
        plusBtn.style.borderLeftWidth = 0; plusBtn.style.borderRightWidth = 0;
        plusBtn.style.borderTopLeftRadius = 8; plusBtn.style.borderTopRightRadius = 8;
        plusBtn.style.borderBottomLeftRadius = 8; plusBtn.style.borderBottomRightRadius = 8;
        plusBtn.SetEnabled(canLevel);
        tickRow.Add(plusBtn);

        return row;
    }

    private void OnTierClicked(FarmSkillTrack track, int tierLevel, VisualElement anchor)
    {
        var fs = FarmSkillsManager.Instance;
        string flavor = TierFlavor[(int)track];
        string status = fs.IsTierUnlocked(track, tierLevel) ? "Unlocked" : "Locked";
        ShowTooltip(anchor, $"{TrackMeta[(int)track].name} Lv {tierLevel} ({status})\n{flavor}");
    }

    private void OnPlusClicked(FarmSkillTrack track)
    {
        FarmSkillsManager.Instance.TryLevelUp(track);
    }
}
