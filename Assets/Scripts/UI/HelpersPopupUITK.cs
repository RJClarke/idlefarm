using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class HelpersPopupUITK : MonoBehaviour
{
    public static HelpersPopupUITK Instance { get; private set; }

    [Header("Data")]
    [Tooltip("UpgradeData for the helper_slot_unlock capacity upgrade.")]
    [SerializeField] private UpgradeData slotUpgrade;

    [Tooltip("Speed UpgradeData entries (Move, Task, Planting, Watering, Harvesting).")]
    [SerializeField] private UpgradeData[] speedUpgrades;

    [Header("Templates")]
    [SerializeField] private VisualTreeAsset slotSectionTemplate;
    [SerializeField] private VisualTreeAsset speedRowTemplate;
    [SerializeField] private VisualTreeAsset speedTileTemplate;

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
            CurrencyManager.Instance.OnMoneyChanged += OnCurrencyChanged;
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
        {
            CurrencyManager.Instance.OnCoinsChanged -= OnCurrencyChanged;
            CurrencyManager.Instance.OnMoneyChanged -= OnCurrencyChanged;
        }
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
        if (root == null) { Debug.LogError("[HelpersPopupUITK] rootVisualElement is null"); return; }

        // Root stays Ignore so clicks in the bottom-nav gutter pass through.
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

        SpawnSlotSection();

        if (speedUpgrades != null)
        {
            foreach (UpgradeData up in speedUpgrades)
                if (up != null) SpawnSpeedRow(up);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Slot section
    // ─────────────────────────────────────────────────────────────────────

    private void SpawnSlotSection()
    {
        if (slotSectionTemplate == null) return;

        TemplateContainer section = slotSectionTemplate.Instantiate();
        sectionList.Add(section);

        Label header = section.Q<Label>("slot-header");
        VisualElement iconRow = section.Q<VisualElement>("slot-icon-row");
        VisualElement buyTileContainer = section.Q<VisualElement>("slot-buy-tile");

        int max = HelperManager.MAX_HELPER_SLOTS;
        bool inRun = RunManager.Instance != null && RunManager.Instance.IsRunActive;

        int permLevel    = SafePermLevel("helper_slot_unlock");
        int currentLevel = SafeCurrentLevel("helper_slot_unlock");
        int permUnlocked    = Mathf.Min(max, permLevel + 1);
        int currentUnlocked = Mathf.Min(max, currentLevel + 1);

        if (header != null)
            header.text = $"Helpers: {currentUnlocked} / {max}";

        if (iconRow != null)
        {
            iconRow.Clear();
            for (int i = 1; i <= max; i++)
            {
                VisualElement icon = new VisualElement();
                icon.AddToClassList("slot-icon");
                if (i <= permUnlocked) icon.AddToClassList("slot-icon--perm");
                else if (i <= currentUnlocked) icon.AddToClassList("slot-icon--rented");
                else icon.AddToClassList("slot-icon--locked");
                iconRow.Add(icon);
            }
        }

        if (buyTileContainer != null && slotUpgrade != null && speedTileTemplate != null)
        {
            buyTileContainer.Clear();
            // Slot has its own ceiling: MAX-1 levels worth of upgrades (level 0 = 1 helper, level 3 = 4).
            int slotMaxLevel = Mathf.Min(slotUpgrade.maxLevel, max - 1);

            // Maxed out — no CTA.
            if (currentLevel >= slotMaxLevel) return;

            int targetLevel = currentLevel + 1;
            SpawnBuyTile(
                buyTileContainer,
                slotUpgrade,
                targetLevel,
                permLevel,
                inRun,
                topLine: inRun ? $"Rent Slot {targetLevel + 1}" : $"Unlock Slot {targetLevel + 1}",
                middleLine: "+1 helper");
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Speed rows — left side (title/desc/pips), right side (single buy tile)
    // ─────────────────────────────────────────────────────────────────────

    private void SpawnSpeedRow(UpgradeData data)
    {
        if (speedRowTemplate == null) return;

        TemplateContainer row = speedRowTemplate.Instantiate();
        sectionList.Add(row);

        Label title = row.Q<Label>("row-title");
        Label desc  = row.Q<Label>("row-desc");
        VisualElement pipRow = row.Q<VisualElement>("row-pip-row");
        Label levelLabel = row.Q<Label>("row-level-label");
        VisualElement buyTileContainer = row.Q<VisualElement>("row-buy-tile");

        if (title != null) title.text = data.displayName;
        if (desc != null)  desc.text  = data.description;

        int permLevel    = SafePermLevel(data.upgradeID);
        int currentLevel = SafeCurrentLevel(data.upgradeID);
        int max = Mathf.Max(1, data.maxLevel);
        bool inRun = RunManager.Instance != null && RunManager.Instance.IsRunActive;

        // Pip row — same color scheme as slot icons.
        if (pipRow != null)
        {
            pipRow.Clear();
            for (int i = 1; i <= max; i++)
            {
                VisualElement pip = new VisualElement();
                pip.AddToClassList("row-pip");
                if (i <= permLevel) pip.AddToClassList("row-pip--perm");
                else if (i <= currentLevel) pip.AddToClassList("row-pip--rented");
                else pip.AddToClassList("row-pip--locked");
                pipRow.Add(pip);
            }
        }

        if (levelLabel != null)
            levelLabel.text = $"Level {currentLevel} / {max}";

        if (buyTileContainer != null && speedTileTemplate != null)
        {
            buyTileContainer.Clear();
            if (currentLevel >= max) return; // maxed — no CTA

            int targetLevel = currentLevel + 1;
            SpawnBuyTile(
                buyTileContainer,
                data,
                targetLevel,
                permLevel,
                inRun,
                topLine: inRun ? $"Rent Lv {targetLevel}" : $"Buy Lv {targetLevel}",
                middleLine: data.GetBonusText(targetLevel));
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Shared single buy/rent tile
    // ─────────────────────────────────────────────────────────────────────

    private void SpawnBuyTile(
        VisualElement parent,
        UpgradeData data,
        int targetLevel,
        int permLevel,
        bool inRun,
        string topLine,
        string middleLine)
    {
        TemplateContainer tile = speedTileTemplate.Instantiate();
        parent.Add(tile);

        VisualElement tileRoot = tile.Q(className: "tile") ?? tile.contentContainer;
        Label lvlLabel    = tile.Q<Label>("tile-level");
        Label bonusLabel  = tile.Q<Label>("tile-bonus");
        Label footerLabel = tile.Q<Label>("tile-footer");

        if (lvlLabel != null)   lvlLabel.text = topLine;
        if (bonusLabel != null) bonusLabel.text = middleLine;

        // Clear any leftover state classes from a prior bind.
        tileRoot.RemoveFromClassList("tile--buy");
        tileRoot.RemoveFromClassList("tile--rent");
        tileRoot.RemoveFromClassList("tile--cant-afford");

        if (inRun)
        {
            // Rent path — Money, green accent.
            tileRoot.AddToClassList("tile--rent");
            int cost = data.GetMoneyCost(targetLevel);
            bool can = CurrencyManager.Instance != null && CurrencyManager.Instance.CanAffordMoney(cost);
            if (!can) tileRoot.AddToClassList("tile--cant-afford");

            if (footerLabel != null)
            {
                string formatted = UpgradeManager.Instance != null
                    ? UpgradeManager.Instance.FormatMoneyCost(cost) : cost.ToString();
                footerLabel.text = "$" + formatted;
            }

            if (can)
            {
                string id = data.upgradeID;
                int capturedCost = cost;
                tileRoot.RegisterCallback<ClickEvent>(_ =>
                {
                    if (UpgradeManager.Instance != null)
                        UpgradeManager.Instance.PurchaseTemporaryUpgrade(id, capturedCost);
                });
            }
        }
        else
        {
            // Buy path — Coins, blue accent.
            tileRoot.AddToClassList("tile--buy");
            int cost = data.GetCoinCost(targetLevel);
            bool can = CurrencyManager.Instance != null && CurrencyManager.Instance.CanAffordCoins(cost);
            if (!can) tileRoot.AddToClassList("tile--cant-afford");

            if (footerLabel != null) footerLabel.text = FormatCoinCost(cost);

            if (can)
            {
                string id = data.upgradeID;
                int capturedCost = cost;
                tileRoot.RegisterCallback<ClickEvent>(_ =>
                {
                    if (UpgradeManager.Instance != null)
                        UpgradeManager.Instance.PurchasePermanentUpgrade(id, capturedCost);
                });
            }
        }
    }

    private static int SafePermLevel(string id)
        => UpgradeManager.Instance != null ? UpgradeManager.Instance.GetPermanentLevel(id) : 0;

    private static int SafeCurrentLevel(string id)
        => UpgradeManager.Instance != null ? UpgradeManager.Instance.GetCurrentLevel(id) : 0;

    private static string FormatCoinCost(int cost)
    {
        if (cost >= 1_000_000) return $"{cost / 1_000_000f:0.#}M";
        if (cost >= 1_000)     return $"{cost / 1_000f:0.#}k";
        return cost.ToString();
    }
}
