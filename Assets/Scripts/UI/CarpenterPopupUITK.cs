using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Carpenter shop popup — list of construction projects. Visually reuses ShopPopupUITK.uxml/.uss
/// for the wood center-card look. For now: a single hardcoded "Build Greenhouse" row.
/// </summary>
[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(1000)]
public class CarpenterPopupUITK : MonoBehaviour
{
    public static CarpenterPopupUITK Instance { get; private set; }

    [Header("Greenhouse Project")]
    [SerializeField] private string greenhouseTitle = "Build Greenhouse";
    [TextArea]
    [SerializeField] private string greenhouseDescription =
        "A safe, all-season home for your research — work continues rain or shine.";
    [SerializeField] private int greenhouseCost = 1000;

    private UIDocument document;
    private VisualElement root;
    private VisualElement popupRoot;
    private Label headerLabel;
    private Button closeButton;
    private ScrollView rowsList;

    private bool isOpen;
    private bool eventsSubscribed;
    private bool refreshPending;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        CacheElements();
        WireCallbacks();
        TrySubscribeEvents();
    }

    private void Start()
    {
        if (root == null) { CacheElements(); WireCallbacks(); }
    }

    private void OnDisable() => UnsubscribeEvents();

    private void TrySubscribeEvents()
    {
        if (eventsSubscribed) return;
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged += OnCurrencyChanged;
            CurrencyManager.Instance.OnWoodChanged += OnCurrencyChanged;
            eventsSubscribed = true;
        }
        if (WoodcuttingManager.Instance != null)
            WoodcuttingManager.Instance.OnAxeLevelChanged += OnCurrencyChanged;
        BuildingState.OnBuildingBuilt += OnBuildingBuilt;
    }

    private void UnsubscribeEvents()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged -= OnCurrencyChanged;
            CurrencyManager.Instance.OnWoodChanged -= OnCurrencyChanged;
        }
        if (WoodcuttingManager.Instance != null)
            WoodcuttingManager.Instance.OnAxeLevelChanged -= OnCurrencyChanged;
        BuildingState.OnBuildingBuilt -= OnBuildingBuilt;
        eventsSubscribed = false;
    }

    private void OnCurrencyChanged(int _) => MarkDirty();
    private void OnBuildingBuilt(string _) => MarkDirty();

    private void MarkDirty()
    {
        if (!isOpen || refreshPending || root == null) return;
        refreshPending = true;
        root.schedule.Execute(() =>
        {
            refreshPending = false;
            if (isOpen) Refresh();
        }).StartingIn(150);
    }

    private void CacheElements()
    {
        root = document.rootVisualElement;
        if (root == null) { Debug.LogError("[CarpenterPopupUITK] rootVisualElement is null"); return; }

        root.pickingMode = PickingMode.Ignore;

        popupRoot   = root.Q<VisualElement>("popup-root");
        headerLabel = root.Q<Label>("header-title");
        closeButton = root.Q<Button>("close-button");
        rowsList    = root.Q<ScrollView>("rows-list");

        if (headerLabel != null) headerLabel.text = "Carpenter";
    }

    private void WireCallbacks()
    {
        if (closeButton != null) closeButton.RegisterCallback<ClickEvent>(_ => Close());
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        TrySubscribeEvents();
        if (root != null) root.pickingMode = PickingMode.Position;
        if (popupRoot != null)
        {
            popupRoot.style.display = DisplayStyle.Flex;
            popupRoot.schedule.Execute(() => popupRoot.AddToClassList("open")).StartingIn(0);
        }
        Refresh();
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        if (popupRoot == null) return;
        popupRoot.RemoveFromClassList("open");
        popupRoot.schedule.Execute(() =>
        {
            if (isOpen) return;
            popupRoot.style.display = DisplayStyle.None;
            if (root != null) root.pickingMode = PickingMode.Ignore;
        }).StartingIn(260);
    }

    private void Refresh()
    {
        if (rowsList == null) return;
        rowsList.Clear();
        BuildGreenhouseRow();
        BuildAxeUpgradeRow();
    }

    private void BuildGreenhouseRow()
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("market-row");

        VisualElement textBlock = new VisualElement();
        textBlock.AddToClassList("market-row-text");

        Label title = new Label(greenhouseTitle);
        title.AddToClassList("market-row-title");
        Label desc = new Label(greenhouseDescription);
        desc.AddToClassList("market-row-desc");
        textBlock.Add(title);
        textBlock.Add(desc);

        VisualElement rightBlock = new VisualElement();
        rightBlock.AddToClassList("market-row-right");

        Label status = new Label();
        status.AddToClassList("market-row-status");
        Label cost = new Label();
        cost.AddToClassList("market-row-cost");
        rightBlock.Add(status);
        rightBlock.Add(cost);

        row.Add(textBlock);
        row.Add(rightBlock);

        bool built = BuildingState.IsBuilt(BuildingState.GreenhouseKey);
        bool canAfford = CurrencyManager.Instance != null && CurrencyManager.Instance.CanAffordCoins(greenhouseCost);

        if (built)
        {
            row.AddToClassList("market-row--owned");
            status.text = "✓ Built";
            cost.text = "";
        }
        else if (canAfford)
        {
            row.AddToClassList("market-row--buy");
            status.text = "BUILD";
            cost.text = FormatCoinCost(greenhouseCost);

            row.RegisterCallback<ClickEvent>(_ =>
            {
                if (CurrencyManager.Instance == null) return;
                if (!CurrencyManager.Instance.SpendCoins(greenhouseCost)) return;
                BuildingState.MarkBuilt(BuildingState.GreenhouseKey);
            });
            WirePressedFeedback(row, "market-row--pressed");
        }
        else
        {
            row.AddToClassList("market-row--cant-afford");
            status.text = "🔒 LOCKED";
            cost.text = FormatCoinCost(greenhouseCost);
        }

        rowsList.Add(row);
    }

    private void BuildAxeUpgradeRow()
    {
        var wm = WoodcuttingManager.Instance;
        if (wm == null) return;

        VisualElement row = new VisualElement();
        row.AddToClassList("market-row");

        VisualElement textBlock = new VisualElement();
        textBlock.AddToClassList("market-row-text");
        Label title = new Label($"Upgrade Axe (Lv {wm.AxeLevel}/{wm.MaxAxeLevel})");
        title.AddToClassList("market-row-title");
        Label desc = new Label("Fell harder trees and chop faster.");
        desc.AddToClassList("market-row-desc");
        textBlock.Add(title);
        textBlock.Add(desc);

        VisualElement rightBlock = new VisualElement();
        rightBlock.AddToClassList("market-row-right");
        Label status = new Label();
        status.AddToClassList("market-row-status");
        Label cost = new Label();
        cost.AddToClassList("market-row-cost");
        rightBlock.Add(status);
        rightBlock.Add(cost);

        row.Add(textBlock);
        row.Add(rightBlock);

        bool maxed = wm.AxeLevel >= wm.MaxAxeLevel;
        if (maxed)
        {
            row.AddToClassList("market-row--owned");
            status.text = "✓ Max";
            cost.text = "";
        }
        else
        {
            cost.text = $"{FormatCoinCost(wm.NextUpgradeCoinCost())} + {wm.NextUpgradeWoodCost()} wood";
            if (wm.CanUpgradeAxe())
            {
                row.AddToClassList("market-row--buy");
                status.text = "UPGRADE";
                row.RegisterCallback<ClickEvent>(_ => { WoodcuttingManager.Instance.TryUpgradeAxe(); });
                WirePressedFeedback(row, "market-row--pressed");
            }
            else
            {
                row.AddToClassList("market-row--cant-afford");
                status.text = "🔒 LOCKED";
            }
        }

        rowsList.Add(row);
    }

    private static void WirePressedFeedback(VisualElement ve, string pressedClass)
    {
        ve.RegisterCallback<PointerDownEvent>(_ => ve.AddToClassList(pressedClass));
        ve.RegisterCallback<PointerUpEvent>(_ => ve.RemoveFromClassList(pressedClass));
        ve.RegisterCallback<PointerLeaveEvent>(_ => ve.RemoveFromClassList(pressedClass));
        ve.RegisterCallback<PointerCancelEvent>(_ => ve.RemoveFromClassList(pressedClass));
    }

    private static string FormatCoinCost(int cost)
    {
        if (cost >= 1_000_000) return $"{cost / 1_000_000f:0.#}M";
        if (cost >= 1_000)     return $"{cost / 1_000f:0.#}k";
        return cost.ToString();
    }
}
