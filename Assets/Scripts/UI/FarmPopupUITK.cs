using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class FarmPopupUITK : MonoBehaviour
{
    public static FarmPopupUITK Instance { get; private set; }

    [Header("Data")]
    [Tooltip("Grid Size upgrade (single, multi-level).")]
    [SerializeField] private UpgradeData gridSizeUpgrade;

    [Tooltip("Zone unlock upgrades in order: Zone 2, Zone 3, Zone 4. Zone 1 is implicit and always owned.")]
    [SerializeField] private UpgradeData[] zoneUnlockUpgrades;

    [Header("Templates")]
    [SerializeField] private VisualTreeAsset zoneGridTemplate;
    [SerializeField] private VisualTreeAsset zoneTileTemplate;
    [SerializeField] private VisualTreeAsset rowTemplate;
    [SerializeField] private VisualTreeAsset tileTemplate;

    private UIDocument document;
    private VisualElement root;
    private VisualElement popupRoot;
    private VisualElement backdrop;
    private Button closeButton;
    private ScrollView sectionList;

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

    private void OnDisable() => UnsubscribeEvents();

    private void TrySubscribeEvents()
    {
        if (eventsSubscribed) return;
        bool any = false;
        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.OnUpgradePurchased += OnUpgradeChanged;
            any = true;
        }
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged += OnCurrencyChanged;
            any = true;
        }
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += OnRunStateChanged;
            RunManager.Instance.OnRunEnded += OnRunStateChanged;
            any = true;
        }
        eventsSubscribed = any;
    }

    private void UnsubscribeEvents()
    {
        if (!eventsSubscribed) return;
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradePurchased -= OnUpgradeChanged;
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCoinsChanged -= OnCurrencyChanged;
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= OnRunStateChanged;
            RunManager.Instance.OnRunEnded -= OnRunStateChanged;
        }
        eventsSubscribed = false;
    }

    private void OnUpgradeChanged(string _) => MarkDirty();
    private void OnCurrencyChanged(int _) => MarkDirty();
    private void OnRunStateChanged() => MarkDirty();

    private void MarkDirty()
    {
        if (!isOpen || refreshPending || root == null) return;
        refreshPending = true;
        root.schedule.Execute(() =>
        {
            refreshPending = false;
            if (isOpen) RefreshAll();
        }).StartingIn(200);
    }

    private void CacheElements()
    {
        root = document.rootVisualElement;
        if (root == null) { Debug.LogError("[FarmPopupUITK] rootVisualElement is null"); return; }

        root.pickingMode = PickingMode.Ignore;

        popupRoot   = root.Q<VisualElement>("popup-root");
        backdrop    = root.Q<VisualElement>("backdrop");
        closeButton = root.Q<Button>("close-button");
        sectionList = root.Q<ScrollView>("section-list");
    }

    private void WireCallbacks()
    {
        if (backdrop != null)    backdrop.RegisterCallback<ClickEvent>(_ => Close());
        if (closeButton != null) closeButton.RegisterCallback<ClickEvent>(_ => Close());
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        TrySubscribeEvents();
        if (popupRoot != null)
        {
            popupRoot.style.display = DisplayStyle.Flex;
            popupRoot.schedule.Execute(() => popupRoot.AddToClassList("open")).StartingIn(0);
        }
        RefreshAll();
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
        }).StartingIn(260);
    }

    public void RefreshAll()
    {
        if (sectionList == null) return;
        sectionList.Clear();

        SpawnZoneGrid();
        if (gridSizeUpgrade != null) SpawnGridSizeRow(gridSizeUpgrade);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Zone grid (2×2, Zone 1 always owned)
    // ─────────────────────────────────────────────────────────────────────

    private void SpawnZoneGrid()
    {
        if (zoneGridTemplate == null || zoneTileTemplate == null) return;

        TemplateContainer grid = zoneGridTemplate.Instantiate();
        sectionList.Add(grid);

        bool inRun = RunManager.Instance != null && RunManager.Instance.IsRunActive;

        for (int z = 1; z <= 4; z++)
        {
            VisualElement cell = grid.Q<VisualElement>($"zone-{z}");
            if (cell == null) continue;
            cell.Clear();

            UpgradeData data = null;
            if (z >= 2 && zoneUnlockUpgrades != null && (z - 2) < zoneUnlockUpgrades.Length)
                data = zoneUnlockUpgrades[z - 2];

            SpawnZoneTile(cell, z, data, inRun);
        }
    }

    private void SpawnZoneTile(VisualElement parent, int zoneNumber, UpgradeData data, bool inRun)
    {
        TemplateContainer tile = zoneTileTemplate.Instantiate();
        parent.Add(tile);

        VisualElement tileRoot = tile.Q(className: "zone-tile") ?? tile.contentContainer;
        Label nameLabel   = tile.Q<Label>("zone-name");
        Label statusLabel = tile.Q<Label>("zone-status");
        Label costLabel   = tile.Q<Label>("zone-cost");

        if (nameLabel != null) nameLabel.text = $"Zone {zoneNumber}";

        bool unlocked = (zoneNumber == 1)
            || (data != null && SafePermLevel(data.upgradeID) >= 1);

        tileRoot.RemoveFromClassList("zone-tile--owned");
        tileRoot.RemoveFromClassList("zone-tile--buy");
        tileRoot.RemoveFromClassList("zone-tile--cant-afford");

        if (unlocked)
        {
            tileRoot.AddToClassList("zone-tile--owned");
            if (statusLabel != null) statusLabel.text = "✓ Owned";
            if (costLabel   != null) costLabel.text   = "";
            return;
        }

        if (data == null) return;

        int targetLevel = 1;
        int coinCost = data.GetCoinCost(targetLevel);
        bool canAfford = CurrencyManager.Instance != null && CurrencyManager.Instance.CanAffordCoins(coinCost);
        bool purchaseable = !inRun && canAfford;

        // Affordable & between runs → blue/buy; otherwise → gray/cant-afford.
        if (purchaseable) tileRoot.AddToClassList("zone-tile--buy");
        else              tileRoot.AddToClassList("zone-tile--cant-afford");

        if (statusLabel != null)
        {
            if (inRun)               statusLabel.text = "Between runs";
            else if (purchaseable)   statusLabel.text = "Unlock";
            else                     statusLabel.text = "Locked";
        }
        if (costLabel != null) costLabel.text = FormatCoinCost(coinCost);

        if (purchaseable)
        {
            string id = data.upgradeID;
            int capturedCost = coinCost;
            int capturedMax = data.maxLevel;
            tileRoot.RegisterCallback<ClickEvent>(_ =>
            {
                if (UpgradeManager.Instance != null)
                    UpgradeManager.Instance.PurchasePermanentUpgrade(id, capturedCost, capturedMax);
            });
            WirePressedFeedback(tileRoot, "zone-tile--pressed");
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Grid Size row (single multi-level upgrade beneath the zone grid)
    // ─────────────────────────────────────────────────────────────────────

    private void SpawnGridSizeRow(UpgradeData data)
    {
        if (rowTemplate == null) return;

        TemplateContainer row = rowTemplate.Instantiate();
        sectionList.Add(row);

        Label title = row.Q<Label>("row-title");
        Label desc  = row.Q<Label>("row-desc");
        VisualElement pipRow = row.Q<VisualElement>("row-pip-row");
        Label levelLabel = row.Q<Label>("row-level-label");
        VisualElement buyTileContainer = row.Q<VisualElement>("row-buy-tile");

        if (title != null) title.text = data.displayName;
        if (desc != null)  desc.text  = data.description;

        int permLevel = SafePermLevel(data.upgradeID);
        int max = Mathf.Max(1, data.maxLevel);
        bool inRun = RunManager.Instance != null && RunManager.Instance.IsRunActive;

        if (pipRow != null)
        {
            pipRow.Clear();
            for (int i = 1; i <= max; i++)
            {
                VisualElement pip = new VisualElement();
                pip.AddToClassList("row-pip");
                if (i <= permLevel) pip.AddToClassList("row-pip--perm");
                else pip.AddToClassList("row-pip--locked");
                pipRow.Add(pip);
            }
        }

        if (levelLabel != null)
            levelLabel.text = $"Level {permLevel} / {max}";

        if (buyTileContainer != null && tileTemplate != null)
        {
            buyTileContainer.Clear();
            if (permLevel >= max) return;

            int targetLevel = permLevel + 1;
            SpawnBuyTile(buyTileContainer, data, targetLevel, inRun);
        }
    }

    private void SpawnBuyTile(VisualElement parent, UpgradeData data, int targetLevel, bool inRun)
    {
        TemplateContainer tile = tileTemplate.Instantiate();
        parent.Add(tile);

        VisualElement tileRoot = tile.Q(className: "tile") ?? tile.contentContainer;
        Label lvlLabel    = tile.Q<Label>("tile-level");
        Label bonusLabel  = tile.Q<Label>("tile-bonus");
        Label footerLabel = tile.Q<Label>("tile-footer");

        if (lvlLabel != null)    lvlLabel.text = $"Buy Lv {targetLevel}";
        if (bonusLabel != null)  bonusLabel.text = data.GetBonusText(targetLevel);

        int cost = data.GetCoinCost(targetLevel);

        tileRoot.RemoveFromClassList("tile--buy");
        tileRoot.RemoveFromClassList("tile--cant-afford");
        tileRoot.AddToClassList("tile--buy");

        bool canAfford = CurrencyManager.Instance != null && CurrencyManager.Instance.CanAffordCoins(cost);
        bool purchaseable = !inRun && canAfford;
        if (!purchaseable) tileRoot.AddToClassList("tile--cant-afford");

        if (footerLabel != null)
            footerLabel.text = inRun ? "Between runs" : FormatCoinCost(cost);

        if (purchaseable)
        {
            string id = data.upgradeID;
            int capturedCost = cost;
            int capturedMax = data.maxLevel;
            tileRoot.RegisterCallback<ClickEvent>(_ =>
            {
                if (UpgradeManager.Instance != null)
                    UpgradeManager.Instance.PurchasePermanentUpgrade(id, capturedCost, capturedMax);
            });
            WirePressedFeedback(tileRoot, "tile--pressed");
        }
    }

    private static void WirePressedFeedback(VisualElement ve, string pressedClass)
    {
        ve.RegisterCallback<PointerDownEvent>(_ => ve.AddToClassList(pressedClass));
        ve.RegisterCallback<PointerUpEvent>(_ => ve.RemoveFromClassList(pressedClass));
        ve.RegisterCallback<PointerLeaveEvent>(_ => ve.RemoveFromClassList(pressedClass));
        ve.RegisterCallback<PointerCancelEvent>(_ => ve.RemoveFromClassList(pressedClass));
    }

    private static int SafePermLevel(string id)
        => UpgradeManager.Instance != null ? UpgradeManager.Instance.GetPermanentLevel(id) : 0;

    private static string FormatCoinCost(int cost)
    {
        if (cost >= 1_000_000) return $"{cost / 1_000_000f:0.#}M";
        if (cost >= 1_000)     return $"{cost / 1_000f:0.#}k";
        return cost.ToString();
    }
}
