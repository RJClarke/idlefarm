using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The Barn: 7 skill tracks as 25-tick bars with a "+" spend button per row, a Renown bar
/// (total levels, max 175) up top, and clickable tier/milestone markers that show their bonus
/// description. No respec. Fully code-driven UIDocument. Spec: 2026-07-19-reputation-design.md §4.
/// </summary>
[DefaultExecutionOrder(1050)]
public class BarnPopupUITK : MonoBehaviour
{
    public static BarnPopupUITK Instance { get; private set; }
    private const int SORT_ORDER = 1000;

    [Tooltip("Shared RunewoodPanelSettings, cloned at runtime (ToastManager pattern) so text renders.")]
    [SerializeField] private PanelSettings sourcePanelSettings;

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

    private UIDocument document;
    private PanelSettings runtimePanelSettings;
    private VisualElement root;
    private VisualElement popupRoot;
    private VisualElement tracksColumn;
    private Label pointsLabel;
    private VisualElement renownFill;
    private Label renownLabel;
    private Label descriptionLabel;
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
        if (popupRoot != null) popupRoot.style.display = DisplayStyle.None;
        if (root != null) root.pickingMode = PickingMode.Ignore;
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
        card.style.width = Length.Percent(90);
        card.style.maxWidth = 720;
        card.style.maxHeight = Length.Percent(85);
        card.style.backgroundColor = new Color(0.70f, 0.60f, 0.43f);
        card.style.borderTopLeftRadius = 18; card.style.borderTopRightRadius = 18;
        card.style.borderBottomLeftRadius = 18; card.style.borderBottomRightRadius = 18;
        card.style.paddingLeft = 20; card.style.paddingRight = 20;
        card.style.paddingTop = 16; card.style.paddingBottom = 16;
        popupRoot.Add(card);

        VisualElement header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;
        card.Add(header);

        Label title = new Label("Barn");
        title.style.fontSize = 34;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color = new Color(0.21f, 0.13f, 0.06f);
        header.Add(title);

        Button closeBtn = new Button(Close) { text = "×" };
        closeBtn.style.width = 48; closeBtn.style.height = 48;
        closeBtn.style.fontSize = 30;
        closeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
        closeBtn.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        closeBtn.style.borderTopWidth = 0; closeBtn.style.borderBottomWidth = 0;
        closeBtn.style.borderLeftWidth = 0; closeBtn.style.borderRightWidth = 0;
        closeBtn.style.color = new Color(0.21f, 0.13f, 0.06f);
        header.Add(closeBtn);

        pointsLabel = new Label();
        pointsLabel.style.fontSize = 24;
        pointsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        pointsLabel.style.color = new Color(0.15f, 0.35f, 0.1f);
        pointsLabel.style.marginTop = 4; pointsLabel.style.marginBottom = 10;
        card.Add(pointsLabel);

        // Renown bar (total levels 0-175). Milestone rewards are Phase 4 — notches are clickable
        // and honest about that ("Reward not yet implemented"), per user feedback on readability.
        VisualElement renownTrack = new VisualElement();
        renownTrack.style.height = 28;
        renownTrack.style.backgroundColor = new Color(0f, 0f, 0f, 0.2f);
        renownTrack.style.borderTopLeftRadius = 8; renownTrack.style.borderTopRightRadius = 8;
        renownTrack.style.borderBottomLeftRadius = 8; renownTrack.style.borderBottomRightRadius = 8;
        renownTrack.style.overflow = Overflow.Hidden;
        renownTrack.style.marginBottom = 4;
        card.Add(renownTrack);

        renownFill = new VisualElement();
        renownFill.style.height = Length.Percent(100);
        renownFill.style.backgroundColor = new Color(0.78f, 0.35f, 0.85f);
        renownTrack.Add(renownFill);

        renownLabel = new Label();
        renownLabel.style.position = Position.Absolute;
        renownLabel.style.left = 0; renownLabel.style.right = 0; renownLabel.style.top = 0; renownLabel.style.bottom = 0;
        renownLabel.style.color = Color.white;
        renownLabel.style.fontSize = 18;
        renownLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        renownTrack.Add(renownLabel);

        VisualElement renownNotches = new VisualElement();
        renownNotches.style.flexDirection = FlexDirection.Row;
        renownNotches.style.justifyContent = Justify.SpaceBetween;
        renownNotches.style.marginBottom = 14;
        int[] renownMilestones = { 10, 25, 50, 100, 175 };
        string[] renownDesc =
        {
            "10 total levels: Reward not yet implemented.",
            "25 total levels: Reward not yet implemented.",
            "50 total levels: Reward not yet implemented.",
            "100 total levels: Reward not yet implemented.",
            "175 total levels: Reward not yet implemented.",
        };
        for (int i = 0; i < renownMilestones.Length; i++)
        {
            int threshold = renownMilestones[i];
            string desc = renownDesc[i];
            Button notch = new Button(() => ShowDescription($"Renown {threshold}", desc)) { text = threshold.ToString() };
            notch.style.fontSize = 14;
            notch.style.height = 30;
            notch.style.backgroundColor = new Color(0.35f, 0.22f, 0.1f);
            notch.style.color = Color.white;
            notch.style.borderTopWidth = 0; notch.style.borderBottomWidth = 0;
            notch.style.borderLeftWidth = 0; notch.style.borderRightWidth = 0;
            renownNotches.Add(notch);
        }
        card.Add(renownNotches);

        tracksColumn = new VisualElement();
        card.Add(tracksColumn);

        descriptionLabel = new Label();
        descriptionLabel.style.fontSize = 20;
        descriptionLabel.style.color = new Color(0.15f, 0.1f, 0.05f);
        descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
        descriptionLabel.style.marginTop = 10;
        descriptionLabel.style.paddingTop = 10;
        descriptionLabel.style.borderTopWidth = 1;
        descriptionLabel.style.borderTopColor = new Color(0f, 0f, 0f, 0.2f);
        descriptionLabel.style.display = DisplayStyle.None;
        card.Add(descriptionLabel);
    }

    private void ShowDescription(string title, string body)
    {
        descriptionLabel.text = $"{title}\n{body}";
        descriptionLabel.style.display = DisplayStyle.Flex;
    }

    private void BuildContent()
    {
        var fs = FarmSkillsManager.Instance;
        var rm = ReputationManager.Instance;
        if (fs == null || rm == null || tracksColumn == null) return;

        pointsLabel.text = $"Points to spend: {rm.UnspentPoints}";

        int totalLevels = fs.TotalLevels;
        renownFill.style.width = Length.Percent(totalLevels / 175f * 100f);
        renownLabel.text = $"Renown  {totalLevels} / 175";

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

        Label nameLabel = new Label($"{meta.name}  ({level}/{FarmSkillsCore.MaxLevel})");
        nameLabel.style.fontSize = 20;
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        nameLabel.style.color = meta.color;
        nameLabel.style.marginBottom = 4;
        row.Add(nameLabel);

        VisualElement tickRow = new VisualElement();
        tickRow.style.flexDirection = FlexDirection.Row;
        tickRow.style.alignItems = Align.Center;
        row.Add(tickRow);

        VisualElement ticksWrap = new VisualElement();
        ticksWrap.style.flexDirection = FlexDirection.Row;
        ticksWrap.style.flexGrow = 1;
        ticksWrap.style.flexWrap = Wrap.Wrap;
        tickRow.Add(ticksWrap);

        for (int lvl = 1; lvl <= FarmSkillsCore.MaxLevel; lvl++)
        {
            bool filled = lvl <= level;
            bool isTier = System.Array.IndexOf(FarmSkillsManager.TierMarkers, lvl) >= 0;

            VisualElement tick = new VisualElement();
            tick.style.width = 16;
            tick.style.height = 26;
            tick.style.marginRight = 2;
            tick.style.borderTopLeftRadius = 3; tick.style.borderTopRightRadius = 3;
            tick.style.borderBottomLeftRadius = 3; tick.style.borderBottomRightRadius = 3;
            tick.style.backgroundColor = filled ? meta.color : new Color(meta.color.r, meta.color.g, meta.color.b, 0.25f);
            if (isTier)
            {
                tick.style.borderBottomWidth = 3;
                tick.style.borderBottomColor = Color.white;
            }
            ticksWrap.Add(tick);

            if (isTier)
            {
                int tierLevel = lvl;
                Button tierBtn = new Button(() => OnTierClicked(track, tierLevel)) { text = "|" };
                tierBtn.style.width = 20; tierBtn.style.height = 26;
                tierBtn.style.marginRight = 4;
                tierBtn.style.fontSize = 14;
                tierBtn.style.backgroundColor = fs.IsTierUnlocked(track, tierLevel) ? meta.color : new Color(0.4f, 0.4f, 0.4f, 0.6f);
                tierBtn.style.color = Color.white;
                tierBtn.style.borderTopWidth = 0; tierBtn.style.borderBottomWidth = 0;
                tierBtn.style.borderLeftWidth = 0; tierBtn.style.borderRightWidth = 0;
                ticksWrap.Add(tierBtn);
            }
        }

        bool canLevel = level < FarmSkillsCore.MaxLevel && ReputationManager.Instance.UnspentPoints > 0;
        Button plusBtn = new Button(() => OnPlusClicked(track)) { text = "+" };
        plusBtn.style.width = 44; plusBtn.style.height = 32;
        plusBtn.style.fontSize = 22;
        plusBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
        plusBtn.style.backgroundColor = canLevel ? meta.color : new Color(0.6f, 0.6f, 0.6f);
        plusBtn.style.color = Color.white;
        plusBtn.style.borderTopWidth = 0; plusBtn.style.borderBottomWidth = 0;
        plusBtn.style.borderLeftWidth = 0; plusBtn.style.borderRightWidth = 0;
        plusBtn.SetEnabled(canLevel);
        tickRow.Add(plusBtn);

        return row;
    }

    private void OnTierClicked(FarmSkillTrack track, int tierLevel)
    {
        var fs = FarmSkillsManager.Instance;
        string flavor = TierFlavor[(int)track];
        string status = fs.IsTierUnlocked(track, tierLevel) ? "Unlocked" : "Locked";
        ShowDescription($"{TrackMeta[(int)track].name} — Level {tierLevel} ({status})", flavor);
    }

    private void OnPlusClicked(FarmSkillTrack track)
    {
        FarmSkillsManager.Instance.TryLevelUp(track);
    }
}
