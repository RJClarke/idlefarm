using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class EquipmentPopupUITK : MonoBehaviour
{
    public static EquipmentPopupUITK Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private EquipmentRegistry registry;

    [Header("Templates")]
    [SerializeField] private VisualTreeAsset sectionTemplate;
    [SerializeField] private VisualTreeAsset lockedSectionTemplate;
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

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

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
            CurrencyManager.Instance.OnCoinsChanged += OnCoinsChanged;
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
            CurrencyManager.Instance.OnCoinsChanged -= OnCoinsChanged;
        eventsSubscribed = false;
    }

    private void OnUpgradeChanged(string _) => MarkDirty();
    private void OnCoinsChanged(int _) => MarkDirty();

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
        if (root == null) { Debug.LogError("[EquipmentPopupUITK] rootVisualElement is null"); return; }

        // Root must stay Ignore so the bottom-nav area (below popup-root) passes clicks
        // through to the uGUI BottomNav canvas underneath.
        root.pickingMode = PickingMode.Ignore;

        popupRoot   = root.Q<VisualElement>("popup-root");
        backdrop    = root.Q<VisualElement>("backdrop");
        closeButton = root.Q<Button>("close-button");
        sectionList = root.Q<ScrollView>("section-list");

        if (popupRoot == null)   Debug.LogWarning("[EquipmentPopupUITK] popup-root not found in UXML");
        if (backdrop == null)    Debug.LogWarning("[EquipmentPopupUITK] backdrop not found in UXML");
        if (sectionList == null) Debug.LogWarning("[EquipmentPopupUITK] section-list not found in UXML");
    }

    private void WireCallbacks()
    {
        if (backdrop != null)     backdrop.RegisterCallback<ClickEvent>(_ => Close());
        if (closeButton != null)  closeButton.RegisterCallback<ClickEvent>(_ => Close());
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

        if (registry == null)
        {
            Debug.LogWarning("[EquipmentPopupUITK] EquipmentRegistry not assigned");
            return;
        }

        foreach (EquipmentData eq in registry.equipment)
        {
            if (eq == null) continue;
            if (eq.IsUnlocked()) SpawnUnlockedSection(eq);
            else SpawnLockedSection(eq);
        }
    }

    private void SpawnUnlockedSection(EquipmentData eq)
    {
        if (sectionTemplate == null)
        {
            Debug.LogWarning("[EquipmentPopupUITK] sectionTemplate not assigned");
            return;
        }

        TemplateContainer section = sectionTemplate.Instantiate();
        sectionList.Add(section);

        Label nameLabel = section.Q<Label>("equipment-name");
        VisualElement iconImage = section.Q<VisualElement>("equipment-icon");
        VisualElement rowsContainer = section.Q<VisualElement>("rows-container");

        if (nameLabel != null) nameLabel.text = eq.displayName;
        if (iconImage != null && eq.iconSprite != null)
            iconImage.style.backgroundImage = new StyleBackground(eq.iconSprite);

        if (eq.uiUpgradeRows == null || rowsContainer == null) return;

        for (int i = 0; i < eq.uiUpgradeRows.Length; i++)
        {
            UpgradeData data = eq.uiUpgradeRows[i];
            if (data == null)
            {
                Debug.LogWarning($"[EquipmentPopupUITK] null upgrade slot at index {i} on '{eq.displayName}'");
                continue;
            }
            SpawnRow(rowsContainer, data);
        }
    }

    private void SpawnRow(VisualElement parent, UpgradeData data)
    {
        if (rowTemplate == null) return;
        TemplateContainer row = rowTemplate.Instantiate();
        parent.Add(row);

        Label title = row.Q<Label>("row-title");
        Label desc  = row.Q<Label>("row-desc");
        VisualElement tilesStrip = row.Q<VisualElement>("tiles-strip");

        if (title != null) title.text = data.displayName;
        if (desc != null)  desc.text  = data.description;

        if (tilesStrip == null) return;

        int max = Mathf.Max(1, data.maxLevel);
        for (int level = 1; level <= max; level++)
            SpawnTile(tilesStrip, data, level);
    }

    private void SpawnTile(VisualElement parent, UpgradeData data, int level)
    {
        if (tileTemplate == null) return;

        TemplateContainer tile = tileTemplate.Instantiate();
        parent.Add(tile);

        VisualElement tileRoot = tile.Q(className: "tile") ?? tile.contentContainer;
        Label lvlLabel   = tile.Q<Label>("tile-level");
        Label bonusLabel = tile.Q<Label>("tile-bonus");
        Label footerLabel = tile.Q<Label>("tile-footer");

        if (lvlLabel != null) lvlLabel.text = $"Level {level}";
        if (bonusLabel != null) bonusLabel.text = data.GetBonusText(level);

        int permLevel = UpgradeManager.Instance != null
            ? UpgradeManager.Instance.GetPermanentLevel(data.upgradeID)
            : 0;
        int cost = data.GetCoinCost(level);

        tileRoot.RemoveFromClassList("tile--bought");
        tileRoot.RemoveFromClassList("tile--affordable");
        tileRoot.RemoveFromClassList("tile--cant-afford");

        if (level <= permLevel)
        {
            tileRoot.AddToClassList("tile--bought");
            if (footerLabel != null) footerLabel.text = "✓";
        }
        else if (level == permLevel + 1 &&
                 CurrencyManager.Instance != null &&
                 CurrencyManager.Instance.CanAffordCoins(cost))
        {
            tileRoot.AddToClassList("tile--affordable");
            if (footerLabel != null) footerLabel.text = FormatCost(cost);
            UpgradeData capturedData = data;
            int capturedLevel = level;
            int capturedCost = cost;
            tileRoot.RegisterCallback<ClickEvent>(_ => OnTileClicked(capturedData, capturedLevel, capturedCost));
        }
        else
        {
            tileRoot.AddToClassList("tile--cant-afford");
            if (footerLabel != null) footerLabel.text = FormatCost(cost);
        }
    }

    private void OnTileClicked(UpgradeData data, int level, int cost)
    {
        if (UpgradeManager.Instance == null) return;
        int currentLevel = UpgradeManager.Instance.GetPermanentLevel(data.upgradeID);
        if (level != currentLevel + 1) return;
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.CanAffordCoins(cost)) return;

        if (UpgradeManager.Instance.PurchasePermanentUpgrade(data.upgradeID, cost))
            RefreshAll();
    }

    private void SpawnLockedSection(EquipmentData eq)
    {
        if (lockedSectionTemplate == null)
        {
            Debug.LogWarning("[EquipmentPopupUITK] lockedSectionTemplate not assigned");
            return;
        }

        TemplateContainer section = lockedSectionTemplate.Instantiate();
        sectionList.Add(section);

        Label nameLabel = section.Q<Label>("equipment-name");
        VisualElement iconImage = section.Q<VisualElement>("equipment-icon");

        if (nameLabel != null) nameLabel.text = eq.displayName;
        if (iconImage != null && eq.iconSprite != null)
            iconImage.style.backgroundImage = new StyleBackground(eq.iconSprite);
    }

    private static string FormatCost(int cost)
    {
        if (cost >= 1_000_000) return $"{cost / 1_000_000f:0.#}M";
        if (cost >= 1_000)     return $"{cost / 1_000f:0.#}k";
        return cost.ToString();
    }
}
