using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(1000)]
public class ShopPopupUITK : MonoBehaviour
{
    public enum Section { Plants, Equipment }

    private static readonly Dictionary<Section, ShopPopupUITK> instances = new Dictionary<Section, ShopPopupUITK>();

    [Header("Identity")]
    [SerializeField] private Section section = Section.Plants;
    [SerializeField] private string headerTitle = "Plants";

    [Header("Data")]
    [SerializeField] private UnlockData[] unlocks;

    [Header("Templates")]
    [SerializeField] private VisualTreeAsset rowTemplate;

    private UIDocument document;
    private VisualElement root;
    private VisualElement popupRoot;
    private VisualElement backdrop;
    private Button closeButton;
    private Label headerLabel;
    private ScrollView rowsList;

    private bool isOpen;
    private bool eventsSubscribed;
    private bool refreshPending;

    public bool IsOpen => isOpen;
    public Section ShopSection => section;

    public static bool TryOpen(Section sec)
    {
        if (instances.TryGetValue(sec, out ShopPopupUITK inst) && inst != null)
        {
            inst.Open();
            return true;
        }
        return false;
    }

    private void Awake()
    {
        document = GetComponent<UIDocument>();
        instances[section] = this;
    }

    private void OnDestroy()
    {
        if (instances.TryGetValue(section, out ShopPopupUITK inst) && inst == this)
            instances.Remove(section);
    }

    private void OnEnable()
    {
        CacheElements();
        WireCallbacks();
        TrySubscribeEvents();
    }

    private void Start()
    {
        // Belt-and-suspenders: if OnEnable ran before UIDocument's OnEnable, root was null.
        // Start always runs after all OnEnables, so we can re-attempt.
        if (root == null)
        {
            CacheElements();
            WireCallbacks();
        }
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
        if (ResearchManager.Instance != null)
        {
            ResearchManager.Instance.OnFeatureFlagUnlocked += OnFeatureFlagUnlocked;
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
        if (ResearchManager.Instance != null)
            ResearchManager.Instance.OnFeatureFlagUnlocked -= OnFeatureFlagUnlocked;
        eventsSubscribed = false;
    }

    private void OnFeatureFlagUnlocked(string _) => MarkDirty();

    private void OnUpgradeChanged(string _) => MarkDirty();
    private void OnCurrencyChanged(int _) => MarkDirty();
    private void OnRunStateChanged() => MarkDirty();
    private void OnRunStarted() { if (isOpen) Close(); }

    private void MarkDirty()
    {
        if (!isOpen || refreshPending || root == null) return;
        refreshPending = true;
        root.schedule.Execute(() =>
        {
            refreshPending = false;
            if (isOpen) Refresh();
        }).StartingIn(200);
    }

    private void CacheElements()
    {
        root = document.rootVisualElement;
        if (root == null) { Debug.LogError("[ShopPopupUITK] rootVisualElement is null"); return; }

        root.pickingMode = PickingMode.Ignore;

        popupRoot   = root.Q<VisualElement>("popup-root");
        backdrop    = root.Q<VisualElement>("backdrop");
        closeButton = root.Q<Button>("close-button");
        headerLabel = root.Q<Label>("header-title");
        rowsList    = root.Q<ScrollView>("rows-list");

        if (headerLabel != null && !string.IsNullOrEmpty(headerTitle))
            headerLabel.text = headerTitle;
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
        if (rowsList == null || rowTemplate == null) return;
        rowsList.Clear();
        if (unlocks == null) return;
        for (int i = 0; i < unlocks.Length; i++)
        {
            UnlockData u = unlocks[i];
            if (u == null) continue;
            // Gate by required research feature flag (e.g. Compost Bay needs "composting_basics")
            if (!string.IsNullOrEmpty(u.requiredFeatureFlag))
            {
                if (ResearchManager.Instance == null) continue;
                if (!ResearchManager.Instance.IsFeatureUnlocked(u.requiredFeatureFlag)) continue;
            }
            SpawnRow(rowsList, u);
        }
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
            string missing = prereqsOk ? null : data.GetMissingPrerequisites();
            if (descLabel != null)   descLabel.text   = string.IsNullOrEmpty(missing) ? data.lockedDescription : $"Requires: {missing}";
            if (statusLabel != null) statusLabel.text = "🔒 LOCKED";
            if (costLabel != null)   costLabel.text   = FormatCoinCost(data.coinCost);
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
            WirePressedFeedback(rowRoot, "market-row--pressed");
        }
    }

    private static void WirePressedFeedback(VisualElement ve, string pressedClass)
    {
        ve.RegisterCallback<PointerDownEvent>(_ => ve.AddToClassList(pressedClass));
        ve.RegisterCallback<PointerUpEvent>(_ => ve.RemoveFromClassList(pressedClass));
        ve.RegisterCallback<PointerLeaveEvent>(_ => ve.RemoveFromClassList(pressedClass));
        ve.RegisterCallback<PointerCancelEvent>(_ => ve.RemoveFromClassList(pressedClass));
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
