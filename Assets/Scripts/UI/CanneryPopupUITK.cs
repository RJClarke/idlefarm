using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Cannery panel: intake toggle, firebox gauge + stoke/fill, jar slots, ready shelf,
/// in-building slot purchases. Lifecycle mirrors WoodRackPopupUITK. Rebuilds rows on a
/// 1s schedule while open (live countdowns) and on CanneryManager.OnChanged.
/// </summary>
[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(1000)]
public class CanneryPopupUITK : MonoBehaviour
{
    public static CanneryPopupUITK Instance { get; private set; }

    private UIDocument document;
    private VisualElement root, popupRoot, fuelFill;
    private Label headerLabel, fuelText;
    private Button closeButton, intakeToggle, stokeButton, fillButton, sellAllButton;
    private ScrollView slotsList, shelfList;

    private bool isOpen;
    private bool eventsSubscribed;
    private IVisualElementScheduledItem ticker;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        document = GetComponent<UIDocument>();
    }

    private void OnEnable() { CacheElements(); WireCallbacks(); TrySubscribeEvents(); }
    private void Start() { if (root == null) { CacheElements(); WireCallbacks(); } }
    private void OnDisable() => UnsubscribeEvents();

    private void TrySubscribeEvents()
    {
        if (eventsSubscribed) return;
        if (CanneryManager.Instance != null)
        {
            CanneryManager.Instance.OnChanged += OnCanneryChanged;
            eventsSubscribed = true;
        }
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnWoodChanged += OnInt;
            CurrencyManager.Instance.OnCoinsChanged += OnInt;
        }
    }

    private void UnsubscribeEvents()
    {
        if (CanneryManager.Instance != null)
            CanneryManager.Instance.OnChanged -= OnCanneryChanged;
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnWoodChanged -= OnInt;
            CurrencyManager.Instance.OnCoinsChanged -= OnInt;
        }
        eventsSubscribed = false;
    }

    private void OnCanneryChanged() { if (isOpen) Refresh(); }
    private void OnInt(int _) { if (isOpen) Refresh(); }

    private void CacheElements()
    {
        root = document.rootVisualElement;
        if (root == null) { Debug.LogError("[CanneryPopupUITK] rootVisualElement is null"); return; }
        root.pickingMode = PickingMode.Ignore;

        popupRoot     = root.Q<VisualElement>("popup-root");
        headerLabel   = root.Q<Label>("header-title");
        closeButton   = root.Q<Button>("close-button");
        intakeToggle  = root.Q<Button>("intake-toggle");
        fuelFill      = root.Q<VisualElement>("fuel-fill");
        fuelText      = root.Q<Label>("fuel-text");
        stokeButton   = root.Q<Button>("stoke-button");
        fillButton    = root.Q<Button>("fill-button");
        slotsList     = root.Q<ScrollView>("slots-list");
        shelfList     = root.Q<ScrollView>("shelf-list");
        sellAllButton = root.Q<Button>("sell-all");

        if (headerLabel != null) headerLabel.text = "Cannery";
    }

    private void WireCallbacks()
    {
        if (closeButton != null) closeButton.RegisterCallback<ClickEvent>(_ => Close());
        if (intakeToggle != null) intakeToggle.RegisterCallback<ClickEvent>(_ =>
        {
            var mgr = CanneryManager.Instance;
            if (mgr != null) mgr.SetIntakeOn(!mgr.IntakeOn);
        });
        if (stokeButton != null) stokeButton.RegisterCallback<ClickEvent>(_ =>
        {
            if (CanneryManager.Instance != null) CanneryManager.Instance.StokeToFinish();
        });
        if (fillButton != null) fillButton.RegisterCallback<ClickEvent>(_ =>
        {
            if (CanneryManager.Instance != null) CanneryManager.Instance.FillFurnace();
        });
        if (sellAllButton != null) sellAllButton.RegisterCallback<ClickEvent>(_ =>
        {
            if (CanneryManager.Instance != null) CanneryManager.Instance.SellAllJars();
        });
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
        ticker = root.schedule.Execute(() => { if (isOpen) Refresh(); }).Every(1000);
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        if (ticker != null) { ticker.Pause(); ticker = null; }
        if (popupRoot == null) return;
        popupRoot.RemoveFromClassList("open");
        popupRoot.schedule.Execute(() =>
        {
            if (isOpen) return;
            popupRoot.style.display = DisplayStyle.None;
            if (root != null) root.pickingMode = PickingMode.Ignore;
        }).StartingIn(260);
    }

    private static string FormatDuration(double seconds)
    {
        if (seconds <= 0) return "0m";
        var t = TimeSpan.FromSeconds(seconds);
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes:00}m";
        if (t.TotalMinutes >= 1) return $"{t.Minutes}m {t.Seconds:00}s";
        return $"{t.Seconds}s";
    }

    private void Refresh()
    {
        var mgr = CanneryManager.Instance;
        var cm = CurrencyManager.Instance;
        if (mgr == null || slotsList == null) return;
        var st = mgr.State;

        // Intake toggle
        if (intakeToggle != null)
        {
            intakeToggle.text = mgr.IntakeOn ? "ON" : "OFF";
            if (mgr.IntakeOn) intakeToggle.RemoveFromClassList("intake-toggle--off");
            else intakeToggle.AddToClassList("intake-toggle--off");
        }

        // Fuel gauge: fraction of capacity + burn-time at current load
        int cooking = ProcessingMath.CountCooking(st);
        double ratePerSec = ProcessingMath.BurnRatePerSecond(cooking, mgr.BaseBurnPerHour, mgr.PerSlotBurnPerHour);
        if (fuelFill != null)
            fuelFill.style.width = Length.Percent(Mathf.Clamp01((float)(st.fuelWood / mgr.FurnaceCapacity)) * 100f);
        if (fuelText != null)
        {
            string lasts = ratePerSec > 0 && st.fuelWood > 0
                ? $" — lasts {FormatDuration(st.fuelWood / ratePerSec)} at current load"
                : (st.fuelWood <= 0 ? " — fire is OUT" : "");
            fuelText.text = $"Fuel: {Mathf.FloorToInt((float)st.fuelWood)}/{mgr.FurnaceCapacity}{lasts}";
        }

        // Fuel buttons
        int stokeCost = mgr.StokeToFinishCost();
        int wood = cm != null ? cm.Wood : 0;
        if (stokeButton != null)
        {
            stokeButton.text = stokeCost > 0 ? $"Stoke to finish  −{stokeCost} wood" : "Stoke to finish  ✓";
            stokeButton.SetEnabled(stokeCost > 0 && wood > 0);
        }
        if (fillButton != null)
        {
            int space = mgr.FurnaceCapacity - Mathf.CeilToInt((float)st.fuelWood);
            int fillAmount = Mathf.Min(space, wood);
            fillButton.text = $"Fill furnace  −{Mathf.Max(0, fillAmount)} wood";
            fillButton.SetEnabled(fillAmount > 0);
        }

        RebuildSlotRows(mgr, st);
        RebuildShelfRows(mgr, st);
    }

    private void RebuildSlotRows(CanneryManager mgr, CanneryState st)
    {
        slotsList.Clear();
        bool fireOut = st.fuelWood <= 0;
        for (int i = 0; i < st.slots.Length; i++)
        {
            var s = st.slots[i];
            var row = new VisualElement();
            row.AddToClassList("slot-row");
            var label = new Label();
            label.AddToClassList("slot-row-label");
            var state = new Label();
            state.AddToClassList("slot-row-state");

            if (ProcessingMath.SlotIsEmpty(s))
            {
                row.AddToClassList("slot-row--empty");
                label.text = $"Slot {i + 1} — empty";
                state.text = "";
            }
            else if (ProcessingMath.SlotIsCooking(s))
            {
                row.AddToClassList(fireOut ? "slot-row--paused" : "slot-row--cooking");
                label.text = $"{s.cropName} ({s.unitsLoaded}/{s.unitsRequired})";
                state.text = fireOut ? "PAUSED — no fuel" : FormatDuration(s.cookSecondsRemaining);
            }
            else
            {
                label.text = $"{s.cropName} ({s.unitsLoaded}/{s.unitsRequired})";
                state.text = "loading…";
            }
            row.Add(label);
            row.Add(state);
            slotsList.Add(row);
        }

        // Buy-next-slot row (spec §5a) / research hint past the purchasable cap
        if (mgr.SlotsOwned < mgr.MaxPurchasableSlots)
        {
            var buyRow = new VisualElement();
            buyRow.AddToClassList("buy-slot-row");
            var t = new Label($"Add Slot {mgr.SlotsOwned + 1}");
            t.AddToClassList("slot-row-label");
            var c = new Label($"{mgr.NextSlotCoinCost()} coins + {mgr.NextSlotWoodCost()} wood");
            c.AddToClassList("slot-row-state");
            buyRow.Add(t);
            buyRow.Add(c);
            if (mgr.CanBuySlot())
                buyRow.RegisterCallback<ClickEvent>(_ => { if (CanneryManager.Instance != null) CanneryManager.Instance.TryBuySlot(); });
            else
                buyRow.AddToClassList("buy-slot-row--locked");
            slotsList.Add(buyRow);
        }
        else if (mgr.SlotsOwned < mgr.TotalMaxSlots)
        {
            var hint = new Label($"Slots {mgr.MaxPurchasableSlots + 1}–{mgr.TotalMaxSlots} require Research.");
            hint.AddToClassList("slot-row-state");
            slotsList.Add(hint);
        }
    }

    private void RebuildShelfRows(CanneryManager mgr, CanneryState st)
    {
        shelfList.Clear();
        int total = 0;
        for (int i = 0; i < st.readyJars.Count; i++)
        {
            var jar = st.readyJars[i];
            total += jar.value;
            var row = new VisualElement();
            row.AddToClassList("shelf-row");
            var label = new Label($"{jar.cropName} preserves");
            label.AddToClassList("slot-row-label");
            var sell = new Button { text = $"Sell +{jar.value}" };
            sell.AddToClassList("shelf-sell-btn");
            int index = i;
            sell.RegisterCallback<ClickEvent>(_ => { if (CanneryManager.Instance != null) CanneryManager.Instance.TrySellJar(index); });
            row.Add(label);
            row.Add(sell);
            shelfList.Add(row);
        }
        if (st.readyJars.Count == 0)
        {
            var empty = new Label("Nothing ready yet.");
            empty.AddToClassList("slot-row-state");
            shelfList.Add(empty);
        }
        if (sellAllButton != null)
        {
            sellAllButton.text = $"Sell All  +{total}";
            sellAllButton.SetEnabled(total > 0);
        }
    }
}
