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

    [Header("Cannery Project (Pantry Economy Phase 1)")]
    [SerializeField] private string canneryTitle = "Build Cannery";
    [TextArea]
    [SerializeField] private string canneryDescription =
        "A wood-fired kettle house. Divert harvests into jars, keep the fire stoked, sell preserves for Gold.";
    [SerializeField] private int canneryCoinCost = 800;
    [SerializeField] private int canneryWoodCost = 300;

    [Header("Smokehouse Project (Pantry Economy Phase 2)")]
    [SerializeField] private string smokehouseTitle = "Build Smokehouse";
    [TextArea]
    [SerializeField] private string smokehouseDescription =
        "A wood-fired smoker. Smoke fish caught at the Lake into far pricier goods — the rare ones pay a fortune.";
    [SerializeField] private int smokehouseCoinCost = 1200;
    [SerializeField] private int smokehouseWoodCost = 450;

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
        if (FishingManager.Instance != null)
            FishingManager.Instance.OnPoleLevelChanged += OnCurrencyChanged;
        if (ResearchManager.Instance != null)
            ResearchManager.Instance.OnFeatureFlagUnlocked += OnFeatureFlagChanged;
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
        if (FishingManager.Instance != null)
            FishingManager.Instance.OnPoleLevelChanged -= OnCurrencyChanged;
        if (ResearchManager.Instance != null)
            ResearchManager.Instance.OnFeatureFlagUnlocked -= OnFeatureFlagChanged;
        BuildingState.OnBuildingBuilt -= OnBuildingBuilt;
        eventsSubscribed = false;
    }

    private void OnCurrencyChanged(int _) => MarkDirty();
    private void OnFeatureFlagChanged(string _) => MarkDirty();
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

        AddSectionHeader("Construction");
        BuildGreenhouseRow();
        BuildCanneryRow();
        BuildSmokehouseRow();

        AddSectionHeader("Tools");
        // Buy the first axe (Coins only) before any leveling exists; once owned, show the upgrade row.
        if (WoodcuttingManager.Instance != null && !WoodcuttingManager.Instance.HasAxe)
            BuildBuyAxeRow();
        else
            BuildAxeUpgradeRow();

        if (FishingManager.Instance != null)
        {
            if (!FishingManager.Instance.HasPole) BuildBuyPoleRow();
            else BuildPoleUpgradeRow();
        }
    }

    private void AddSectionHeader(string text)
    {
        Label header = new Label(text);
        header.AddToClassList("market-section-header");
        rowsList.Add(header);
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

    private void BuildCanneryRow()
    {
        var rm = ResearchManager.Instance;
        bool canneryUnlocked = rm == null || rm.IsFeatureUnlocked(Research.FeatureFlag.CanneryUnlocked);
        if (!canneryUnlocked && !BuildingState.IsBuilt(BuildingState.CanneryKey))
            return; // not researched yet — not offered at the Carpenter

        VisualElement row = new VisualElement();
        row.AddToClassList("market-row");

        VisualElement textBlock = new VisualElement();
        textBlock.AddToClassList("market-row-text");
        Label title = new Label(canneryTitle);
        title.AddToClassList("market-row-title");
        Label desc = new Label(canneryDescription);
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

        bool built = BuildingState.IsBuilt(BuildingState.CanneryKey);
        var cm = CurrencyManager.Instance;
        bool canAfford = cm != null && cm.CanAffordCoins(canneryCoinCost) && cm.CanAffordWood(canneryWoodCost);

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
            cost.text = $"{FormatCoinCost(canneryCoinCost)} + {canneryWoodCost} wood";
            row.RegisterCallback<ClickEvent>(_ =>
            {
                var c = CurrencyManager.Instance;
                if (c == null) return;
                if (!c.SpendCoins(canneryCoinCost)) return;
                if (!c.SpendWood(canneryWoodCost)) { c.AddCoins(canneryCoinCost); return; }
                BuildingState.MarkBuilt(BuildingState.CanneryKey);
                Debug.Log("[Carpenter] Cannery built.");
            });
            WirePressedFeedback(row, "market-row--pressed");
        }
        else
        {
            row.AddToClassList("market-row--cant-afford");
            status.text = "🔒 LOCKED";
            cost.text = $"{FormatCoinCost(canneryCoinCost)} + {canneryWoodCost} wood";
        }

        rowsList.Add(row);
    }

    private void BuildSmokehouseRow()
    {
        var rm = ResearchManager.Instance;
        bool smokehouseUnlocked = rm == null || rm.IsFeatureUnlocked(Research.FeatureFlag.SmokehouseUnlocked);
        if (!smokehouseUnlocked && !BuildingState.IsBuilt(BuildingState.SmokehouseKey))
            return; // not researched yet — not offered at the Carpenter

        VisualElement row = new VisualElement();
        row.AddToClassList("market-row");

        VisualElement textBlock = new VisualElement();
        textBlock.AddToClassList("market-row-text");
        Label title = new Label(smokehouseTitle);
        title.AddToClassList("market-row-title");
        Label desc = new Label(smokehouseDescription);
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

        bool built = BuildingState.IsBuilt(BuildingState.SmokehouseKey);
        var cm = CurrencyManager.Instance;
        bool canAfford = cm != null && cm.CanAffordCoins(smokehouseCoinCost) && cm.CanAffordWood(smokehouseWoodCost);

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
            cost.text = $"{FormatCoinCost(smokehouseCoinCost)} + {smokehouseWoodCost} wood";
            row.RegisterCallback<ClickEvent>(_ =>
            {
                var c = CurrencyManager.Instance;
                if (c == null) return;
                if (!c.SpendCoins(smokehouseCoinCost)) return;
                if (!c.SpendWood(smokehouseWoodCost)) { c.AddCoins(smokehouseCoinCost); return; }
                BuildingState.MarkBuilt(BuildingState.SmokehouseKey);
                Debug.Log("[Carpenter] Smokehouse built.");
            });
            WirePressedFeedback(row, "market-row--pressed");
        }
        else
        {
            row.AddToClassList("market-row--cant-afford");
            status.text = "🔒 LOCKED";
            cost.text = $"{FormatCoinCost(smokehouseCoinCost)} + {smokehouseWoodCost} wood";
        }

        rowsList.Add(row);
    }

    private void BuildBuyAxeRow()
    {
        var wm = WoodcuttingManager.Instance;
        if (wm == null) return;

        VisualElement row = new VisualElement();
        row.AddToClassList("market-row");

        VisualElement textBlock = new VisualElement();
        textBlock.AddToClassList("market-row-text");
        Label title = new Label("Buy Axe");
        title.AddToClassList("market-row-title");
        Label desc = new Label("Your first axe. Needed to fell trees in the Woods for Wood.");
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

        cost.text = FormatCoinCost(wm.FirstAxeCoinCost);
        if (wm.CanBuyAxe())
        {
            row.AddToClassList("market-row--buy");
            status.text = "BUY";
            row.RegisterCallback<ClickEvent>(_ => { WoodcuttingManager.Instance.TryBuyAxe(); });
            WirePressedFeedback(row, "market-row--pressed");
        }
        else
        {
            row.AddToClassList("market-row--cant-afford");
            status.text = "🔒 LOCKED";
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
        // Levels read 1-based to the player: a bought axe is "Lv 1", the last upgrade is MaxAxeLevel+1.
        Label title = new Label($"Upgrade Axe (Lv {wm.AxeLevel + 1}/{wm.MaxAxeLevel + 1})");
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

    private void BuildBuyPoleRow()
    {
        var fm = FishingManager.Instance;
        if (fm == null) return;

        VisualElement row = new VisualElement();
        row.AddToClassList("market-row");

        VisualElement textBlock = new VisualElement();
        textBlock.AddToClassList("market-row-text");
        Label title = new Label("Buy Fishing Pole");
        title.AddToClassList("market-row-title");
        Label desc = new Label("Your first pole. Needed to fish the Lake for Perch, Bass, and the rare Pike.");
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

        cost.text = FormatCoinCost(fm.FirstPoleCoinCost);
        if (fm.CanBuyPole())
        {
            row.AddToClassList("market-row--buy");
            status.text = "BUY";
            row.RegisterCallback<ClickEvent>(_ => { if (FishingManager.Instance != null) FishingManager.Instance.TryBuyPole(); });
            WirePressedFeedback(row, "market-row--pressed");
        }
        else
        {
            row.AddToClassList("market-row--cant-afford");
            status.text = "🔒 LOCKED";
        }

        rowsList.Add(row);
    }

    private void BuildPoleUpgradeRow()
    {
        var fm = FishingManager.Instance;
        if (fm == null) return;

        VisualElement row = new VisualElement();
        row.AddToClassList("market-row");

        VisualElement textBlock = new VisualElement();
        textBlock.AddToClassList("market-row-text");
        // Levels read 1-based to the player: a bought pole is "Lv 1".
        Label title = new Label($"Upgrade Pole (Lv {fm.PoleLevel + 1}/{fm.MaxPoleLevel + 1})");
        title.AddToClassList("market-row-title");
        Label desc = new Label("Bite faster and hook rarer fish.");
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

        bool maxed = fm.PoleLevel >= fm.MaxPoleLevel;
        if (maxed)
        {
            row.AddToClassList("market-row--owned");
            status.text = "✓ Max";
            cost.text = "";
        }
        else
        {
            cost.text = $"{FormatCoinCost(fm.NextUpgradeCoinCost())} + {fm.NextUpgradeWoodCost()} wood";
            if (fm.CanUpgradePole())
            {
                row.AddToClassList("market-row--buy");
                status.text = "UPGRADE";
                row.RegisterCallback<ClickEvent>(_ => { if (FishingManager.Instance != null) FishingManager.Instance.TryUpgradePole(); });
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
