using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MarketPopupUITK : MonoBehaviour
{
    public static MarketPopupUITK Instance { get; private set; }

    [Header("Data")]
    [Tooltip("Crop unlocks shown in the Crops section (already-owned crops are filtered out for now).")]
    [SerializeField] private UnlockData[] cropUnlocks;

    [Tooltip("Equipment unlocks shown in the Equipment section (all entries shown, owned/locked styled accordingly).")]
    [SerializeField] private UnlockData[] equipmentUnlocks;

    [Header("Templates")]
    [SerializeField] private VisualTreeAsset sectionTemplate;
    [SerializeField] private VisualTreeAsset rowTemplate;

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
            RunManager.Instance.OnRunStarted += OnRunStarted;
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
            RunManager.Instance.OnRunStarted -= OnRunStarted;
            RunManager.Instance.OnRunEnded -= OnRunStateChanged;
        }
        eventsSubscribed = false;
    }

    private void OnUpgradeChanged(string _) => MarkDirty();
    private void OnCurrencyChanged(int _) => MarkDirty();
    private void OnRunStateChanged() => MarkDirty();

    // Market is town-mode only — auto-close if a run starts while open.
    private void OnRunStarted() { if (isOpen) Close(); }

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
        if (root == null) { Debug.LogError("[MarketPopupUITK] rootVisualElement is null"); return; }

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

        // Crops — show all, owned ones styled as "Purchased" (no longer filtered out).
        List<UnlockData> crops = new List<UnlockData>();
        if (cropUnlocks != null)
        {
            foreach (UnlockData u in cropUnlocks)
            {
                if (u == null) continue;
                crops.Add(u);
            }
        }
        if (crops.Count > 0) SpawnSection("Crops", crops);

        // Equipment — show everything, styled by state.
        List<UnlockData> equipment = new List<UnlockData>();
        if (equipmentUnlocks != null)
        {
            foreach (UnlockData u in equipmentUnlocks)
            {
                if (u == null) continue;
                equipment.Add(u);
            }
        }
        if (equipment.Count > 0) SpawnSection("Equipment", equipment);
    }

    private void SpawnSection(string title, List<UnlockData> items)
    {
        if (sectionTemplate == null || rowTemplate == null) return;

        TemplateContainer section = sectionTemplate.Instantiate();
        sectionList.Add(section);

        Label header = section.Q<Label>("section-title");
        if (header != null) header.text = title;

        VisualElement rowsContainer = section.Q<VisualElement>("section-rows");
        if (rowsContainer == null) return;

        foreach (UnlockData data in items)
            SpawnRow(rowsContainer, data);
    }

    private void SpawnRow(VisualElement parent, UnlockData data)
    {
        TemplateContainer rowContainer = rowTemplate.Instantiate();
        parent.Add(rowContainer);

        VisualElement rowRoot = rowContainer.Q(className: "market-row") ?? rowContainer.contentContainer;
        VisualElement iconImg = rowContainer.Q<VisualElement>("row-icon");
        Label iconFallback    = rowContainer.Q<Label>("row-icon-fallback");
        Label titleLabel      = rowContainer.Q<Label>("row-title");
        Label descLabel       = rowContainer.Q<Label>("row-desc");
        Label statusLabel     = rowContainer.Q<Label>("row-status");
        Label costLabel       = rowContainer.Q<Label>("row-cost");

        if (titleLabel != null) titleLabel.text = data.displayName;

        Sprite spr = GetIconSprite(data);
        if (iconImg != null)
        {
            if (spr != null)
            {
                iconImg.style.backgroundImage = new StyleBackground(spr);
                iconImg.style.display = DisplayStyle.Flex;
                if (iconFallback != null) iconFallback.style.display = DisplayStyle.None;
            }
            else
            {
                iconImg.style.display = DisplayStyle.None;
                if (iconFallback != null)
                {
                    iconFallback.text = data.icon;
                    iconFallback.style.display = DisplayStyle.Flex;
                }
            }
        }

        rowRoot.RemoveFromClassList("market-row--owned");
        rowRoot.RemoveFromClassList("market-row--buy");
        rowRoot.RemoveFromClassList("market-row--cant-afford");

        bool owned       = IsOwned(data);
        bool prereqsOk   = data.MeetsPrerequisites();
        bool canAfford   = CurrencyManager.Instance != null && CurrencyManager.Instance.CanAffordCoins(data.coinCost);
        bool purchasable = !owned && prereqsOk && canAfford;

        if (owned)
        {
            rowRoot.AddToClassList("market-row--owned");
            if (descLabel != null)   descLabel.text   = data.unlockedMessage;
            if (statusLabel != null) statusLabel.text = "✓ Purchased";
            if (costLabel != null)   costLabel.text   = "";
        }
        else if (purchasable)
        {
            rowRoot.AddToClassList("market-row--buy");
            if (descLabel != null)   descLabel.text   = data.lockedDescription;
            if (statusLabel != null) statusLabel.text = "UNLOCK";
            if (costLabel != null)   costLabel.text   = FormatCoinCost(data.coinCost);
        }
        else
        {
            rowRoot.AddToClassList("market-row--cant-afford");
            if (!prereqsOk)
            {
                string missing = data.GetMissingPrerequisites();
                if (descLabel != null)   descLabel.text   = string.IsNullOrEmpty(missing) ? data.lockedDescription : $"Requires: {missing}";
                if (statusLabel != null) statusLabel.text = "🔒 LOCKED";
                if (costLabel != null)   costLabel.text   = FormatCoinCost(data.coinCost);
            }
            else
            {
                if (descLabel != null)   descLabel.text   = data.lockedDescription;
                if (statusLabel != null) statusLabel.text = "🔒 LOCKED";
                if (costLabel != null)   costLabel.text   = FormatCoinCost(data.coinCost);
            }
        }

        if (purchasable)
        {
            string id = data.unlockID;
            int cost = data.coinCost;
            rowRoot.RegisterCallback<ClickEvent>(_ =>
            {
                if (UpgradeManager.Instance != null)
                    UpgradeManager.Instance.PurchasePermanentUpgrade(id, cost);
            });
        }
    }

    private static Sprite GetIconSprite(UnlockData data)
    {
        if (data == null) return null;
        if (data.category == UnlockCategory.Crop && data.cropData != null)
            return data.cropData.seedPacketSprite;
        if (data.category == UnlockCategory.Equipment)
            return data.equipmentSprite;
        return null;
    }

    private static bool IsOwned(UnlockData data)
        => UpgradeManager.Instance != null && UpgradeManager.Instance.GetPermanentLevel(data.unlockID) > 0;

    private static string FormatCoinCost(int cost)
    {
        if (cost >= 1_000_000) return $"{cost / 1_000_000f:0.#}M";
        if (cost >= 1_000)     return $"{cost / 1_000f:0.#}k";
        return cost.ToString();
    }
}
