using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Inventory mega-list: every resource the player owns in one panel — currencies, wood/compost,
/// collected crop/egg stacks, and pantry goods — with bulk-sell rows where a sell price exists
/// (Reputation Phase 1, spec §5.3). Crops/eggs sell for Coins; wood mirrors the rack (Cash,
/// in-run only); compost's sell button ships disabled. Live-updates off manager events while
/// open. Lifecycle mirrors WoodRackPopupUITK.
/// </summary>
[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(1000)]
public class InventoryPopupUITK : MonoBehaviour
{
    public static InventoryPopupUITK Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private CropDatabase cropDatabase;

    private UIDocument document;
    private VisualElement root;
    private VisualElement popupRoot;
    private Button closeButton;
    private VisualElement backdrop;
    private ScrollView list;

    // One expanded sell panel at a time; the whole list rebuilds on any change while open.
    private string expandedRowId;

    private bool isOpen;
    private bool eventsSubscribed;
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

    private void CacheElements()
    {
        root = document.rootVisualElement;
        if (root == null) { Debug.LogError("[InventoryPopupUITK] rootVisualElement is null"); return; }

        root.pickingMode = PickingMode.Ignore;
        popupRoot   = root.Q<VisualElement>("popup-root");
        backdrop    = root.Q<VisualElement>("backdrop");
        closeButton = root.Q<Button>("close-button");
        list        = root.Q<ScrollView>("inventory-list");
    }

    private void WireCallbacks()
    {
        if (closeButton != null) closeButton.RegisterCallback<ClickEvent>(_ => Close());
        if (backdrop != null) backdrop.RegisterCallback<ClickEvent>(_ => Close());
    }

    private void TrySubscribeEvents()
    {
        if (eventsSubscribed || CurrencyManager.Instance == null) return;
        var cm = CurrencyManager.Instance;
        cm.OnCoinsChanged   += OnAnyChanged;
        cm.OnGemsChanged    += OnAnyChanged;
        cm.OnMoneyChanged   += OnAnyChanged;
        cm.OnWoodChanged    += OnAnyChanged;
        cm.OnCompostChanged += OnAnyChanged;
        if (ItemInventoryManager.Instance != null)
            ItemInventoryManager.Instance.OnChanged += OnInvChanged;
        if (PantryManager.Instance != null)
            PantryManager.Instance.OnChanged += OnInvChanged;
        eventsSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!eventsSubscribed || CurrencyManager.Instance == null) { eventsSubscribed = false; return; }
        var cm = CurrencyManager.Instance;
        cm.OnCoinsChanged   -= OnAnyChanged;
        cm.OnGemsChanged    -= OnAnyChanged;
        cm.OnMoneyChanged   -= OnAnyChanged;
        cm.OnWoodChanged    -= OnAnyChanged;
        cm.OnCompostChanged -= OnAnyChanged;
        if (ItemInventoryManager.Instance != null)
            ItemInventoryManager.Instance.OnChanged -= OnInvChanged;
        if (PantryManager.Instance != null)
            PantryManager.Instance.OnChanged -= OnInvChanged;
        eventsSubscribed = false;
    }

    private void OnAnyChanged(int _) { if (isOpen) BuildRows(); }
    private void OnInvChanged()      { if (isOpen) BuildRows(); }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        TrySubscribeEvents();
        if (root != null) root.pickingMode = PickingMode.Position;
        BuildRows();
        if (popupRoot != null)
        {
            popupRoot.style.display = DisplayStyle.Flex;
            popupRoot.schedule.Execute(() => popupRoot.AddToClassList("open")).StartingIn(0);
        }
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        expandedRowId = null;
        if (popupRoot == null) return;
        popupRoot.RemoveFromClassList("open");
        popupRoot.schedule.Execute(() =>
        {
            if (isOpen) return;
            popupRoot.style.display = DisplayStyle.None;
            if (root != null) root.pickingMode = PickingMode.Ignore;
        }).StartingIn(260);
    }

    private void BuildRows()
    {
        if (list == null) return;
        list.Clear();

        var cm = CurrencyManager.Instance;
        var inv = ItemInventoryManager.Instance;
        if (cm == null) return;

        AddSection("Currencies");
        AddPlainRow("inv-icon--coins", "Coins", cm.Coins);
        AddPlainRow("inv-icon--gems", "Gems", cm.Gems);
        AddPlainRow("inv-icon--money", "Cash", cm.Money);

        AddSection("Resources");
        // Wood sells for Cash at the rack's price — in-run only, exactly like the rack.
        int woodPrice = WoodRackPopupUITK.Instance != null ? WoodRackPopupUITK.Instance.CashPricePerWood : 0;
        bool inRun = RunManager.Instance != null && RunManager.Instance.IsRunActive;
        AddSellRow("wood", "inv-icon--wood", "Wood", cm.Wood,
            enabled: inRun && woodPrice > 0 && cm.Wood > 0,
            payoutLabel: n => $"+{WoodcuttingMath.SellValue(n, woodPrice)} Cash",
            onSell: n => { if (cm.SpendWood(n)) cm.AddMoney(WoodcuttingMath.SellValue(n, woodPrice)); },
            valueTextOverride: $"{cm.Wood:N0} / {cm.MaxWood:N0}");
        // Compost has no sell price in v1 — the button ships visible but disabled (user decision).
        AddSellRow("compost", "inv-icon--compost", "Compost", cm.Compost,
            enabled: false, payoutLabel: null, onSell: null);

        if (inv != null)
        {
            bool anyHarvest = inv.Eggs > 0;
            if (cropDatabase != null)
                foreach (CropData crop in cropDatabase.allCrops)
                    if (crop != null && inv.GetCrop(crop.cropName) > 0) { anyHarvest = true; break; }

            if (anyHarvest)
            {
                AddSection("Harvest");
                if (cropDatabase != null)
                    foreach (CropData crop in cropDatabase.allCrops)
                    {
                        if (crop == null) continue;
                        int count = inv.GetCrop(crop.cropName);
                        if (count <= 0) continue;
                        int per = inv.RawCoinValue(crop);
                        string cropName = crop.cropName;
                        AddSellRow("crop:" + cropName, "inv-icon--coins", cropName, count,
                            enabled: true,
                            payoutLabel: n => $"+{per * n} Coins",
                            onSell: n => { if (inv.TrySpendCrop(cropName, n)) cm.AddCoins(per * n); });
                    }
                if (inv.Eggs > 0)
                    AddSellRow("eggs", "inv-icon--coins", "Eggs", inv.Eggs,
                        enabled: true,
                        payoutLabel: n => $"+{inv.EggSellCoins * n} Coins",
                        onSell: n => { if (inv.TrySpendEggs(n)) cm.AddCoins(inv.EggSellCoins * n); });
            }
        }

        var pantry = PantryManager.Instance;
        bool anyPantry = pantry != null && (pantry.TotalRaw > 0 || pantry.TotalSmoked > 0);
        int jarCount = CanneryManager.Instance != null ? CanneryManager.Instance.ReadyJarCount : 0;
        if (anyPantry || jarCount > 0)
        {
            AddSection("Pantry");
            if (pantry != null)
                for (int tier = 1; tier <= FishTiers.Count; tier++)
                {
                    if (pantry.GetRaw(tier) > 0)
                        AddPlainRow("inv-icon--money", FishTiers.Name(tier), pantry.GetRaw(tier));
                    if (pantry.GetSmoked(tier) > 0)
                        AddPlainRow("inv-icon--money", FishTiers.SmokedName(tier), pantry.GetSmoked(tier));
                }
            if (jarCount > 0)
                AddPlainRow("inv-icon--money", "Jars", jarCount);
        }
    }

    private void AddSection(string title)
    {
        var lbl = new Label(title);
        lbl.AddToClassList("inv-section");
        list.Add(lbl);
    }

    private void AddPlainRow(string iconClass, string name, int value)
    {
        var row = new VisualElement(); row.AddToClassList("inv-row");
        var icon = new VisualElement(); icon.AddToClassList("inv-icon"); icon.AddToClassList(iconClass);
        icon.pickingMode = PickingMode.Ignore;
        var nameLbl = new Label(name); nameLbl.AddToClassList("inv-name");
        var valLbl = new Label(value.ToString("N0")); valLbl.AddToClassList("inv-value");
        row.Add(icon); row.Add(nameLbl); row.Add(valLbl);
        list.Add(row);
    }

    private void AddSellRow(string rowId, string iconClass, string name, int count,
        bool enabled, System.Func<int, string> payoutLabel, System.Action<int> onSell, string valueTextOverride = null)
    {
        var row = new VisualElement(); row.AddToClassList("inv-row");
        var icon = new VisualElement(); icon.AddToClassList("inv-icon"); icon.AddToClassList(iconClass);
        icon.pickingMode = PickingMode.Ignore;
        var nameLbl = new Label(name); nameLbl.AddToClassList("inv-name");
        var valLbl = new Label(valueTextOverride ?? count.ToString("N0")); valLbl.AddToClassList("inv-value");
        var sellBtn = new Button(() =>
        {
            expandedRowId = expandedRowId == rowId ? null : rowId;
            BuildRows();
        }) { text = "Sell" };
        sellBtn.AddToClassList("inv-sell-btn");
        sellBtn.SetEnabled(enabled);
        row.Add(icon); row.Add(nameLbl); row.Add(valLbl); row.Add(sellBtn);
        list.Add(row);

        if (expandedRowId != rowId || !enabled || onSell == null) return;

        // Inline sell panel: slider + quick amounts + confirm (spec §5.3).
        var panel = new VisualElement(); panel.AddToClassList("inv-sell-panel");
        var slider = new SliderInt(1, Mathf.Max(1, count)) { value = Mathf.Max(1, count / 2) };
        slider.AddToClassList("inv-sell-slider");
        var quickRow = new VisualElement(); quickRow.AddToClassList("inv-quick-row");
        var confirm = new Button();
        confirm.AddToClassList("inv-confirm-btn");

        void SyncConfirm()
        {
            int n = Mathf.Clamp(slider.value, 1, count);
            confirm.text = payoutLabel != null ? $"Sell {n}  ({payoutLabel(n)})" : $"Sell {n}";
        }
        slider.RegisterValueChangedCallback(_ => SyncConfirm());

        void AddQuick(string label, System.Func<int> pick)
        {
            var b = new Button(() => { slider.value = Mathf.Clamp(pick(), 1, count); SyncConfirm(); }) { text = label };
            b.AddToClassList("inv-quick-btn");
            quickRow.Add(b);
        }
        AddQuick("10", () => 10);
        AddQuick("Half", () => count / 2);
        AddQuick("Max", () => count);

        confirm.clicked += () =>
        {
            int n = Mathf.Clamp(slider.value, 1, count);
            onSell(n);
            expandedRowId = null;
            // Manager OnChanged rebuilds; this is the fallback for wood (Currency int events).
            BuildRows();
        };

        SyncConfirm();
        panel.Add(slider); panel.Add(quickRow); panel.Add(confirm);
        list.Add(panel);
    }
}
