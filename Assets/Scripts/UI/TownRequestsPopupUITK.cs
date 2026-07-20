using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The Town Requests board popup: 3 fixed-difficulty slots (Easy/Medium/Hard), each showing the
/// current request, a Fulfill button (enabled only when affordable), a Skip button (costs Gems,
/// escalating), or a cooldown countdown once fulfilled. Fully code-driven UIDocument (ToastManager
/// pattern). Spec: 2026-07-19-reputation-design.md §3.
/// </summary>
[DefaultExecutionOrder(1050)]
public class TownRequestsPopupUITK : MonoBehaviour
{
    public static TownRequestsPopupUITK Instance { get; private set; }
    private const int SORT_ORDER = 1000;
    private static readonly string[] DifficultyNames = { "Easy", "Medium", "Hard" };
    private static readonly Color[] DifficultyColors =
    {
        new Color(0.45f, 0.75f, 0.4f),   // Easy - green
        new Color(0.85f, 0.72f, 0.25f),  // Medium - gold
        new Color(0.8f, 0.35f, 0.3f),    // Hard - red
    };

    [Tooltip("Shared RunewoodPanelSettings, cloned at runtime with a higher sort order (ToastManager pattern). " +
             "If left null, a fresh PanelSettings is created — but with no theme, text will not render.")]
    [SerializeField] private PanelSettings sourcePanelSettings;

    private UIDocument document;
    private PanelSettings runtimePanelSettings;
    private VisualElement root;
    private VisualElement popupRoot;
    private VisualElement backdrop;
    private VisualElement slotsColumn;
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
        // Countdown labels tick every second; only touch the DOM while actually open.
        if (isOpen) RefreshCountdownsOnly();
    }

    private void OnChanged() { if (isOpen) BuildSlots(); }

    public void Open()
    {
        if (isOpen || popupRoot == null) return;
        isOpen = true;
        root.pickingMode = PickingMode.Position;
        BuildSlots();
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
        if (root == null) { Debug.LogWarning("[TownRequestsPopupUITK] rootVisualElement null."); return; }
        root.pickingMode = PickingMode.Ignore;
        // Default root height is auto/content-sized; a Position.Absolute child (popupRoot) is out
        // of flow and contributes nothing, so root collapses to height 0 without this. Width
        // stretches by default (column cross-axis), height does not (column main-axis) — hence
        // only height needs the explicit override.
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
        backdrop.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
        backdrop.pickingMode = PickingMode.Position;
        backdrop.RegisterCallback<ClickEvent>(_ => Close());
        popupRoot.Add(backdrop);

        VisualElement card = new VisualElement { name = "town-requests-card" };
        card.style.width = Length.Percent(86);
        card.style.maxWidth = 680;
        card.style.backgroundColor = new Color(0.70f, 0.60f, 0.43f);
        card.style.borderTopLeftRadius = 18; card.style.borderTopRightRadius = 18;
        card.style.borderBottomLeftRadius = 18; card.style.borderBottomRightRadius = 18;
        card.style.paddingLeft = 20; card.style.paddingRight = 20;
        card.style.paddingTop = 16; card.style.paddingBottom = 20;
        popupRoot.Add(card);

        VisualElement header = new VisualElement { name = "header" };
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;
        header.style.marginBottom = 12;
        card.Add(header);

        Label title = new Label("Town Requests");
        title.style.fontSize = 34;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color = new Color(0.21f, 0.13f, 0.06f);
        header.Add(title);

        Button closeBtn = new Button(Close) { text = "×" }; // "×"
        closeBtn.style.width = 48; closeBtn.style.height = 48;
        closeBtn.style.fontSize = 30;
        closeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
        closeBtn.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        closeBtn.style.borderTopWidth = 0; closeBtn.style.borderBottomWidth = 0;
        closeBtn.style.borderLeftWidth = 0; closeBtn.style.borderRightWidth = 0;
        closeBtn.style.color = new Color(0.21f, 0.13f, 0.06f);
        header.Add(closeBtn);

        slotsColumn = new VisualElement { name = "slots" };
        slotsColumn.style.flexDirection = FlexDirection.Column;
        card.Add(slotsColumn);
    }

    private void BuildSlots()
    {
        if (slotsColumn == null || ReputationManager.Instance == null) return;
        slotsColumn.Clear();
        for (int slot = 0; slot < 3; slot++)
            slotsColumn.Add(BuildSlotCard(slot));
    }

    private VisualElement BuildSlotCard(int slot)
    {
        var rm = ReputationManager.Instance;
        VisualElement card = new VisualElement { name = "slot-" + slot };
        card.style.backgroundColor = new Color(1f, 0.98f, 0.93f);
        card.style.borderTopLeftRadius = 12; card.style.borderTopRightRadius = 12;
        card.style.borderBottomLeftRadius = 12; card.style.borderBottomRightRadius = 12;
        card.style.borderLeftWidth = 6;
        card.style.borderLeftColor = DifficultyColors[slot];
        card.style.paddingLeft = 16; card.style.paddingRight = 16;
        card.style.paddingTop = 12; card.style.paddingBottom = 12;
        card.style.marginBottom = 14;

        Label diffLabel = new Label(DifficultyNames[slot]);
        diffLabel.style.fontSize = 20;
        diffLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        diffLabel.style.color = DifficultyColors[slot];
        card.Add(diffLabel);

        bool onCooldown = rm.IsSlotOnCooldown(slot);
        DeliveryRequest request = rm.GetSlotRequest(slot);

        if (onCooldown || request == null)
        {
            Label cooldownLabel = new Label();
            cooldownLabel.name = "cooldown-label";
            cooldownLabel.style.fontSize = 24;
            cooldownLabel.style.color = new Color(0.35f, 0.28f, 0.2f);
            cooldownLabel.style.marginTop = 6;
            cooldownLabel.text = $"Completed! Next request in {TimeFormat.Hms((float)rm.GetSlotCooldownRemainingSeconds(slot))}";
            card.Add(cooldownLabel);
            return card;
        }

        DeliveryLineItem lineItem = request.items[0];
        Label flavor = new Label($"{request.requesterName} needs {lineItem.count} {RequestItemDisplay.Name(lineItem.itemId)}");
        flavor.style.fontSize = 24;
        flavor.style.color = new Color(0.15f, 0.1f, 0.05f);
        flavor.style.whiteSpace = WhiteSpace.Normal;
        flavor.style.marginTop = 4;
        card.Add(flavor);

        Label reward = new Label($"Reward: {request.repReward} Reputation");
        reward.style.fontSize = 20;
        reward.style.color = new Color(0.4f, 0.3f, 0.15f);
        reward.style.marginTop = 2; reward.style.marginBottom = 10;
        card.Add(reward);

        VisualElement buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.justifyContent = Justify.SpaceBetween;
        card.Add(buttonRow);

        bool canFulfill = DeliveryService.CanFulfill(request);
        Button fulfillBtn = new Button(() => OnFulfillClicked(slot, request)) { text = "Fulfill" };
        fulfillBtn.style.flexGrow = 1;
        fulfillBtn.style.marginRight = 8;
        fulfillBtn.style.height = 48;
        fulfillBtn.style.backgroundColor = canFulfill ? new Color(0.36f, 0.62f, 0.32f) : new Color(0.6f, 0.6f, 0.6f);
        fulfillBtn.style.color = Color.white;
        fulfillBtn.SetEnabled(canFulfill);
        buttonRow.Add(fulfillBtn);

        int skipCost = rm.NextSkipCost;
        Button skipBtn = new Button(() => OnSkipClicked(slot)) { text = $"Skip ({skipCost} gems)" };
        skipBtn.style.flexGrow = 1;
        skipBtn.style.height = 48;
        skipBtn.style.backgroundColor = new Color(0.55f, 0.45f, 0.35f);
        skipBtn.style.color = Color.white;
        buttonRow.Add(skipBtn);

        return card;
    }

    private void OnFulfillClicked(int slot, DeliveryRequest request)
    {
        int reward = request.repReward;
        if (ReputationManager.Instance.TryFulfill(slot))
            ToastManager.Show("Request Fulfilled", $"+{reward} Reputation", ToastManager.ToastKind.Success);
    }

    private void OnSkipClicked(int slot)
    {
        ReputationManager.Instance.TrySkip(slot);
    }

    private void RefreshCountdownsOnly()
    {
        if (slotsColumn == null || ReputationManager.Instance == null) return;
        var rm = ReputationManager.Instance;
        for (int slot = 0; slot < 3; slot++)
        {
            if (!rm.IsSlotOnCooldown(slot)) continue;
            VisualElement card = slotsColumn.Q<VisualElement>("slot-" + slot);
            Label cooldownLabel = card?.Q<Label>("cooldown-label");
            if (cooldownLabel != null)
                cooldownLabel.text = $"Completed! Next request in {TimeFormat.Hms((float)rm.GetSlotCooldownRemainingSeconds(slot))}";
        }
    }
}
