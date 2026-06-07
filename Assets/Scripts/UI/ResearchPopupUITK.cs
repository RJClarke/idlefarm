using System;
using System.Collections.Generic;
using System.Linq;
using Research;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(1000)]
public class ResearchPopupUITK : MonoBehaviour
{
    public static ResearchPopupUITK Instance { get; private set; }

    private UIDocument document;
    private VisualElement root;
    private VisualElement popupRoot;
    private Button closeButton;

    // Picker (now a sibling of popup-root, not a child)
    private VisualElement picker;
    private ScrollView pickerList;
    private Button pickerClose;
    private int pickerSlotIndex = -1;

    // Section order used in the single-scroll picker (top→bottom). Branches not in this list
    // still render, appended after these in alphabetical order.
    private static readonly string[] BranchOrder = { "helper", "plant", "soil", "animals", "equipment", "weather", "meta" };
    private static readonly System.Collections.Generic.Dictionary<string, string> BranchDisplay =
        new System.Collections.Generic.Dictionary<string, string>
        {
            { "soil",      "Soil" },
            { "helper",    "Helpers" },
            { "plant",     "Crops" },
            { "animals",   "Animals" },
            { "equipment", "Equipment" },
            { "weather",   "Weather" },
            { "meta",      "Meta" },
        };

    private bool isOpen;
    private bool eventsSubscribed;
    private bool refreshPending;
    private IVisualElementScheduledItem refreshTicker;
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
        bool any = false;
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged += OnCurrencyChanged;
            CurrencyManager.Instance.OnGemsChanged  += OnCurrencyChanged;
            any = true;
        }
        if (ResearchManager.Instance != null)
        {
            ResearchManager.Instance.OnSlotUnlocked     += OnSlotUnlocked;
            ResearchManager.Instance.OnSlotStateChanged += OnSlotStateChanged;
            ResearchManager.Instance.OnResearchLeveledUp += OnLeveledUp;
            any = true;
        }
        eventsSubscribed = any;
    }

    private void UnsubscribeEvents()
    {
        if (!eventsSubscribed) return;
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged -= OnCurrencyChanged;
            CurrencyManager.Instance.OnGemsChanged  -= OnCurrencyChanged;
        }
        if (ResearchManager.Instance != null)
        {
            ResearchManager.Instance.OnSlotUnlocked     -= OnSlotUnlocked;
            ResearchManager.Instance.OnSlotStateChanged -= OnSlotStateChanged;
            ResearchManager.Instance.OnResearchLeveledUp -= OnLeveledUp;
        }
        eventsSubscribed = false;
    }

    private void OnCurrencyChanged(int _) => MarkDirty();
    private void OnSlotUnlocked(int _) => MarkDirty();
    private void OnSlotStateChanged(int _) => MarkDirty();
    private void OnLeveledUp(string _, int __) => MarkDirty();

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
        if (root == null) { Debug.LogError("[ResearchPopupUITK] rootVisualElement is null"); return; }
        root.pickingMode = PickingMode.Ignore;

        popupRoot   = root.Q<VisualElement>("popup-root");
        closeButton = root.Q<Button>("close-button");

        picker      = root.Q<VisualElement>("picker");
        pickerList  = root.Q<ScrollView>("picker-list");
        pickerClose = root.Q<Button>("picker-close");
    }

    private void WireCallbacks()
    {
        if (closeButton != null) closeButton.RegisterCallback<ClickEvent>(_ => Close());
        if (pickerClose != null) pickerClose.RegisterCallback<ClickEvent>(_ => ClosePicker());
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
        // Tick the popup once a second so countdowns update without waiting on events.
        refreshTicker?.Pause();
        refreshTicker = root.schedule.Execute(TickRefresh).Every(1000);
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        ClosePicker();
        refreshTicker?.Pause();
        refreshTicker = null;
        if (popupRoot == null) return;
        popupRoot.RemoveFromClassList("open");
        popupRoot.schedule.Execute(() =>
        {
            if (isOpen) return;
            popupRoot.style.display = DisplayStyle.None;
            if (root != null) root.pickingMode = PickingMode.Ignore;
        }).StartingIn(260);
    }

    private void TickRefresh()
    {
        if (!isOpen) return;
        Refresh();
    }

    private void Refresh()
    {
        if (root == null) return;
        for (int i = 0; i < ResearchManager.SlotCount; i++) RenderSlot(i);
    }

    private void RenderSlot(int slotIndex)
    {
        VisualElement card = root.Q<VisualElement>($"slot-{slotIndex}");
        if (card == null) return;

        card.Clear();
        card.RemoveFromClassList("slot-card--unlocked-empty");
        card.RemoveFromClassList("slot-card--locked");
        card.RemoveFromClassList("slot-card--affordable");

        ResearchManager mgr = ResearchManager.Instance;
        if (mgr == null) return;

        bool unlocked = mgr.IsSlotUnlocked(slotIndex);
        if (!unlocked) { RenderLockedSlot(card, slotIndex, mgr); return; }

        var state = mgr.GetSlot(slotIndex);
        if (state == null || state.IsIdle) { RenderEmptySlot(card, slotIndex); return; }

        RenderActiveSlot(card, slotIndex, mgr, state);
    }

    private void RenderLockedSlot(VisualElement card, int slotIndex, ResearchManager mgr)
    {
        var def = mgr.GetSlotDef(slotIndex);
        Label statusLabel = new Label("Locked"); statusLabel.AddToClassList("slot-status");
        Label actionLabel = new Label(); actionLabel.AddToClassList("slot-action");
        switch (def.unlockType)
        {
            case ResearchManager.SlotUnlockType.Coins: actionLabel.text = $"{def.costAmount} coins to unlock"; break;
            case ResearchManager.SlotUnlockType.Gems:  actionLabel.text = $"{def.costAmount} gems to unlock"; break;
            case ResearchManager.SlotUnlockType.Research: actionLabel.text = "Research to unlock"; break;
        }
        bool canAfford = mgr.CanUnlockSlot(slotIndex);
        card.AddToClassList(canAfford ? "slot-card--affordable" : "slot-card--locked");
        if (canAfford)
        {
            int captured = slotIndex;
            card.RegisterCallback<ClickEvent>(_ => ResearchManager.Instance?.TryUnlockSlot(captured));
            WirePressedFeedback(card, "slot-card--pressed");
        }
        card.Add(statusLabel); card.Add(actionLabel);
    }

    private void RenderEmptySlot(VisualElement card, int slotIndex)
    {
        card.AddToClassList("slot-card--unlocked-empty");
        Label statusLabel = new Label("No Active Research"); statusLabel.AddToClassList("slot-status");
        Label actionLabel = new Label("Tap to assign");      actionLabel.AddToClassList("slot-action");
        int captured = slotIndex;
        card.RegisterCallback<ClickEvent>(_ => OpenPicker(captured));
        WirePressedFeedback(card, "slot-card--pressed");
        card.Add(statusLabel); card.Add(actionLabel);
    }

    private void RenderActiveSlot(VisualElement card, int slotIndex, ResearchManager mgr, ResearchSlotState state)
    {
        var rd = mgr.GetResearch(state.activeResearchID);
        if (rd == null) { CancelSlotAndRefresh(slotIndex); return; }

        Label nameLabel = new Label($"{rd.displayName} — L{state.currentLevel + 1}/{rd.MaxLevel}");
        nameLabel.AddToClassList("slot-card__active-name");

        int nextLevel = state.currentLevel + 1;
        float secsForLevel = mgr.GetSecondsForLevel(rd, nextLevel);
        double elapsed = (DateTime.UtcNow.Ticks - state.startUtcTicks) / (double)TimeSpan.TicksPerSecond;
        float progress = secsForLevel <= 0 ? 0f : Mathf.Clamp01((float)(elapsed / secsForLevel));
        double remaining = Math.Max(0, secsForLevel - elapsed);

        VisualElement bar = new VisualElement(); bar.AddToClassList("slot-card__active-progress");
        VisualElement fill = new VisualElement(); fill.AddToClassList("slot-card__active-progress-fill");
        fill.style.width = new StyleLength(new Length(progress * 100f, LengthUnit.Percent));
        bar.Add(fill);

        Label timer = new Label(FormatActiveTimer(remaining));
        timer.AddToClassList("slot-card__active-timer");

        // Boost indicator (Plan 2 — element exists so Plan 2 can flip it on without re-touching this code)
        Label boost = new Label();
        boost.AddToClassList("slot-card__boost");
        if (state.boostMultiplier > 1.0f && state.boostExpiresUtcTicks > DateTime.UtcNow.Ticks)
        {
            double boostLeft = (state.boostExpiresUtcTicks - DateTime.UtcNow.Ticks) / (double)TimeSpan.TicksPerSecond;
            boost.text = $"{state.boostMultiplier:F0}x — {FormatRemaining(boostLeft)} left";
            boost.AddToClassList("slot-card__boost--active");
        }

        Label boostBtn = new Label("⚡ Boost (Compost)");
        boostBtn.AddToClassList("slot-card__cancel");
        boostBtn.style.color = new StyleColor(new Color(0.55f, 0.78f, 0.39f));
        int capturedSlotBoost = slotIndex;
        boostBtn.RegisterCallback<ClickEvent>(_ =>
        {
            if (CompostBoostModalUITK.Instance != null)
                CompostBoostModalUITK.Instance.Open(capturedSlotBoost);
        });

        Label cancel = new Label("Cancel ↩"); cancel.AddToClassList("slot-card__cancel");
        int captured = slotIndex;
        cancel.RegisterCallback<ClickEvent>(_ => CancelSlotAndRefresh(captured));

        card.Add(nameLabel); card.Add(bar); card.Add(timer); card.Add(boost); card.Add(boostBtn); card.Add(cancel);
    }

    private void CancelSlotAndRefresh(int slotIndex)
    {
        ResearchManager.Instance?.CancelResearch(slotIndex);
        Refresh();
    }

    /// <summary>Compact "1.5 days" style — used in picker rows (limited space).</summary>
    private static string FormatRemaining(double secs)
    {
        if (secs >= 86400) return $"{secs/86400:F1} days";
        if (secs >= 3600)  return $"{secs/3600:F1} hr";
        if (secs >= 60)    return $"{secs/60:F0} min";
        return $"{secs:F0} sec";
    }

    /// <summary>Detailed "2d 14h 32m 15s" — used in active slot countdown. Always shows all four units.</summary>
    private static string FormatActiveTimer(double secs)
    {
        if (secs < 0) secs = 0;
        long total = (long)secs;
        long days = total / 86400; total -= days * 86400;
        long hrs  = total / 3600;  total -= hrs  * 3600;
        long mins = total / 60;    total -= mins * 60;
        long s    = total;
        return $"{days}d {hrs}h {mins}m {s}s";
    }

    // ───────── Picker ─────────

    private void OpenPicker(int slotIndex)
    {
        pickerSlotIndex = slotIndex;
        if (picker != null) picker.style.display = DisplayStyle.Flex;
        RebuildPickerList();
    }

    private void ClosePicker()
    {
        pickerSlotIndex = -1;
        if (picker != null) picker.style.display = DisplayStyle.None;
    }

    private void RebuildPickerList()
    {
        if (pickerList == null) return;
        pickerList.Clear();
        var mgr = ResearchManager.Instance;
        if (mgr == null) return;

        // Group visible researches by branch
        var byBranch = mgr.AllResearches()
            .Where(rd => mgr.IsResearchVisible(rd.researchID))
            .GroupBy(rd => rd.branchID)
            .ToDictionary(g => g.Key, g => g.OrderBy(rd => rd.displayName).ToList());

        // Render branches in defined order, then any extras alphabetically
        var orderedBranches = new System.Collections.Generic.List<string>();
        foreach (var b in BranchOrder) if (byBranch.ContainsKey(b)) orderedBranches.Add(b);
        foreach (var b in byBranch.Keys.OrderBy(s => s))
            if (!orderedBranches.Contains(b)) orderedBranches.Add(b);

        bool first = true;
        foreach (var branch in orderedBranches)
        {
            var section = new VisualElement(); section.AddToClassList("picker-section");
            if (first) section.AddToClassList("picker-section--first");
            first = false;

            string displayName = BranchDisplay.TryGetValue(branch, out var d) ? d : branch;
            var title = new Label(displayName); title.AddToClassList("picker-section__title");
            var count = new Label($"{byBranch[branch].Count} research"); count.AddToClassList("picker-section__count");
            section.Add(title); section.Add(count);
            pickerList.Add(section);

            foreach (var rd in byBranch[branch])
                pickerList.Add(BuildPickerRow(rd));
        }
    }

    private VisualElement BuildPickerRow(ResearchData rd)
    {
        var mgr = ResearchManager.Instance;
        int curLevel = mgr.GetCurrentLevel(rd.researchID);
        bool isMaxed = curLevel >= rd.MaxLevel;
        int nextLevel = isMaxed ? rd.MaxLevel : curLevel + 1;
        int cost = isMaxed ? 0 : mgr.GetCostForLevel(rd, nextLevel);
        float secs = isMaxed ? 0f : mgr.GetSecondsForLevel(rd, nextLevel);
        bool canAfford = !isMaxed && CurrencyManager.Instance != null && CurrencyManager.Instance.CanAffordCoins(cost);

        var row = new VisualElement(); row.AddToClassList("picker-row");

        var textCol = new VisualElement(); textCol.AddToClassList("picker-row__text-col");
        var name = new Label($"{rd.displayName} — L{curLevel}/{rd.MaxLevel}");
        name.AddToClassList("picker-row__name");
        var desc = new Label(string.IsNullOrEmpty(rd.description) ? "" : rd.description);
        desc.AddToClassList("picker-row__desc");
        textCol.Add(name); textCol.Add(desc);

        var metaCol = new VisualElement(); metaCol.AddToClassList("picker-row__meta-col");
        var costLbl = new Label(isMaxed ? "Complete" : $"{cost} coins");
        costLbl.AddToClassList("picker-row__cost");
        var timeLbl = new Label(isMaxed ? "Maxed" : FormatRemaining(secs));
        timeLbl.AddToClassList("picker-row__time");
        metaCol.Add(costLbl); metaCol.Add(timeLbl);

        row.Add(textCol); row.Add(metaCol);

        if (isMaxed)
        {
            row.AddToClassList("picker-row--complete");
        }
        else if (!canAfford)
        {
            row.AddToClassList("picker-row--disabled");
            costLbl.AddToClassList("picker-row__cost--unaffordable");
        }
        else
        {
            string capturedID = rd.researchID;
            int capturedSlot = pickerSlotIndex;
            row.RegisterCallback<ClickEvent>(_ =>
            {
                if (ResearchManager.Instance != null && ResearchManager.Instance.TryAssignResearch(capturedSlot, capturedID))
                    ClosePicker();
            });
            WirePressedFeedback(row, "slot-card--pressed");
        }

        return row;
    }

    private static void WirePressedFeedback(VisualElement ve, string pressedClass)
    {
        ve.RegisterCallback<PointerDownEvent>(_ => ve.AddToClassList(pressedClass));
        ve.RegisterCallback<PointerUpEvent>(_ => ve.RemoveFromClassList(pressedClass));
        ve.RegisterCallback<PointerLeaveEvent>(_ => ve.RemoveFromClassList(pressedClass));
        ve.RegisterCallback<PointerCancelEvent>(_ => ve.RemoveFromClassList(pressedClass));
    }
}
