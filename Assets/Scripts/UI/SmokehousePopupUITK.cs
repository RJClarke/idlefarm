using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Smokehouse panel: firebox gauge + stoke/fill, raw-fish rows (smoke / sell), smoker slots,
/// smoked-fish rows (sell), in-building slot purchases. Lifecycle mirrors CanneryPopupUITK —
/// rebuilds on a 1s schedule while open (live countdowns) and on manager/pantry change events.
/// </summary>
[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(1000)]
public class SmokehousePopupUITK : MonoBehaviour
{
    public static SmokehousePopupUITK Instance { get; private set; }

    private UIDocument document;
    private VisualElement root, popupRoot, fuelFill;
    private Label headerLabel, fuelText;
    private Button closeButton, stokeButton, fillButton;
    private ScrollView rawList, slotsList, smokedList;

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
        if (SmokehouseManager.Instance != null)
        {
            SmokehouseManager.Instance.OnChanged += OnVoidChanged;
            eventsSubscribed = true;
        }
        if (PantryManager.Instance != null) PantryManager.Instance.OnChanged += OnVoidChanged;
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnWoodChanged += OnInt;
            CurrencyManager.Instance.OnCoinsChanged += OnInt;
        }
    }

    private void UnsubscribeEvents()
    {
        if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.OnChanged -= OnVoidChanged;
        if (PantryManager.Instance != null) PantryManager.Instance.OnChanged -= OnVoidChanged;
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnWoodChanged -= OnInt;
            CurrencyManager.Instance.OnCoinsChanged -= OnInt;
        }
        eventsSubscribed = false;
    }

    private void OnVoidChanged() { if (isOpen) Refresh(); }
    private void OnInt(int _) { if (isOpen) Refresh(); }

    private void CacheElements()
    {
        root = document.rootVisualElement;
        if (root == null) { Debug.LogError("[SmokehousePopupUITK] rootVisualElement is null"); return; }
        root.pickingMode = PickingMode.Ignore;

        popupRoot   = root.Q<VisualElement>("popup-root");
        headerLabel = root.Q<Label>("header-title");
        closeButton = root.Q<Button>("close-button");
        fuelFill    = root.Q<VisualElement>("fuel-fill");
        fuelText    = root.Q<Label>("fuel-text");
        stokeButton = root.Q<Button>("stoke-button");
        fillButton  = root.Q<Button>("fill-button");
        rawList     = root.Q<ScrollView>("raw-list");
        slotsList   = root.Q<ScrollView>("slots-list");
        smokedList  = root.Q<ScrollView>("smoked-list");

        if (headerLabel != null) headerLabel.text = "Smokehouse";
    }

    private void WireCallbacks()
    {
        if (closeButton != null) closeButton.RegisterCallback<ClickEvent>(_ => Close());
        if (stokeButton != null) stokeButton.RegisterCallback<ClickEvent>(_ =>
        {
            if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.StokeToFinish();
        });
        if (fillButton != null) fillButton.RegisterCallback<ClickEvent>(_ =>
        {
            if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.FillFurnace();
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
        var mgr = SmokehouseManager.Instance;
        var cm = CurrencyManager.Instance;
        if (mgr == null || slotsList == null) return;
        var st = mgr.State;

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

        RebuildRawRows(mgr);
        RebuildSlotRows(mgr, st);
        RebuildSmokedRows(mgr);
    }

    private void RebuildRawRows(SmokehouseManager mgr)
    {
        rawList.Clear();
        var pantry = PantryManager.Instance;
        bool anyEmptySlot = false;
        foreach (var s in mgr.State.slots) if (ProcessingMath.SlotIsEmpty(s)) { anyEmptySlot = true; break; }

        for (int tier = 1; tier <= FishTiers.Count; tier++)
        {
            int count = pantry != null ? pantry.GetRaw(tier) : 0;
            var row = new VisualElement();
            row.AddToClassList("fish-row");
            var label = new Label($"{FishTiers.Name(tier)}  ×{count}");
            label.AddToClassList("fish-row-label");
            var buttons = new VisualElement();
            buttons.AddToClassList("fish-row-buttons");

            int t = tier;
            var smoke = new Button { text = "Smoke" };
            smoke.AddToClassList("fish-btn");
            smoke.AddToClassList("fish-btn--smoke");
            smoke.SetEnabled(count > 0 && anyEmptySlot);
            smoke.RegisterCallback<ClickEvent>(_ => { if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.TryLoadFish(t); });

            var sell = new Button { text = $"Sell +{mgr.RawValue(tier)}" };
            sell.AddToClassList("fish-btn");
            sell.SetEnabled(count > 0);
            sell.RegisterCallback<ClickEvent>(_ => { if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.TrySellRaw(t); });

            buttons.Add(smoke);
            buttons.Add(sell);
            row.Add(label);
            row.Add(buttons);
            rawList.Add(row);
        }
    }

    private void RebuildSlotRows(SmokehouseManager mgr, CanneryState st)
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
            else
            {
                row.AddToClassList(fireOut ? "slot-row--paused" : "slot-row--cooking");
                label.text = s.cropName;
                state.text = fireOut ? "PAUSED — no fuel" : FormatDuration(s.cookSecondsRemaining);
            }
            row.Add(label);
            row.Add(state);
            slotsList.Add(row);
        }

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
                buyRow.RegisterCallback<ClickEvent>(_ => { if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.TryBuySlot(); });
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

    private void RebuildSmokedRows(SmokehouseManager mgr)
    {
        smokedList.Clear();
        var pantry = PantryManager.Instance;
        int totalCount = 0;
        for (int tier = 1; tier <= FishTiers.Count; tier++)
        {
            int count = pantry != null ? pantry.GetSmoked(tier) : 0;
            totalCount += count;
            var row = new VisualElement();
            row.AddToClassList("fish-row");
            var label = new Label($"{FishTiers.SmokedName(tier)}  ×{count}");
            label.AddToClassList("fish-row-label");
            int t = tier;
            var sell = new Button { text = $"Sell +{mgr.SmokedValue(tier)}" };
            sell.AddToClassList("fish-btn");
            sell.SetEnabled(count > 0);
            sell.RegisterCallback<ClickEvent>(_ => { if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.TrySellSmoked(t); });
            row.Add(label);
            row.Add(sell);
            smokedList.Add(row);
        }
        if (totalCount == 0)
        {
            var empty = new Label("Nothing smoked yet.");
            empty.AddToClassList("slot-row-state");
            smokedList.Add(empty);
        }
    }
}
