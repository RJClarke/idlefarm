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

    // Research detail modal (built in code, layered above the picker).
    private VisualElement detailPanel;

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

        // Lock icon above the "Locked" label.
        var lockIcon = new VisualElement(); lockIcon.AddToClassList("slot-lock-icon");
        lockIcon.pickingMode = PickingMode.Ignore;
        card.Add(lockIcon);

        Label statusLabel = new Label("Locked"); statusLabel.AddToClassList("slot-status");
        card.Add(statusLabel);

        if (def.unlockType == ResearchManager.SlotUnlockType.Research)
        {
            Label actionLabel = new Label("Research to unlock"); actionLabel.AddToClassList("slot-action");
            card.Add(actionLabel);
        }
        else
        {
            // Currency cost: amount + currency icon + "to unlock".
            var row = new VisualElement(); row.AddToClassList("slot-action-row");
            row.pickingMode = PickingMode.Ignore;
            Label amount = new Label(def.costAmount.ToString()); amount.AddToClassList("slot-action");
            var costIcon = new VisualElement();
            costIcon.AddToClassList(def.unlockType == ResearchManager.SlotUnlockType.Gems
                ? "slot-cost-icon--gem" : "slot-cost-icon--coin");
            costIcon.pickingMode = PickingMode.Ignore;
            Label suffix = new Label("to unlock"); suffix.AddToClassList("slot-action");
            row.Add(amount); row.Add(costIcon); row.Add(suffix);
            card.Add(row);
        }

        // Per design: all locked slots share the same locked look (the gems/coins slot no longer
        // gets the bright "affordable" highlight); affordable slots stay clickable.
        card.AddToClassList("slot-card--locked");
        if (mgr.CanUnlockSlot(slotIndex))
        {
            int captured = slotIndex;
            card.RegisterCallback<ClickEvent>(_ => ResearchManager.Instance?.TryUnlockSlot(captured));
            WirePressedFeedback(card, "slot-card--pressed");
        }
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

        Label nameLabel = new Label($"{rd.displayName} — {state.currentLevel + 1}/{rd.MaxLevel}");
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
        CloseDetail();
        pickerSlotIndex = -1;
        if (picker != null) picker.style.display = DisplayStyle.None;
    }

    // ── Research detail modal ────────────────────────

    private void OpenResearchDetail(string researchID, int slotIndex, VisualElement rowDot)
    {
        var mgr = ResearchManager.Instance;
        if (mgr == null || root == null) return;
        var rd = mgr.GetResearch(researchID);
        if (rd == null) return;

        // Opening details counts as viewing → clear the NEW badge.
        if (NewContentTracker.Instance != null)
            NewContentTracker.Instance.MarkSeen(NewContentTracker.ResearchId(researchID));
        if (rowDot != null && rowDot.parent != null) rowDot.RemoveFromHierarchy();

        int curLevel = mgr.GetCurrentLevel(researchID);
        bool isMaxed = curLevel >= rd.MaxLevel;
        int nextLevel = isMaxed ? rd.MaxLevel : curLevel + 1;
        int fullCost = isMaxed ? 0 : mgr.GetCostForLevel(rd, nextLevel);
        float fullSecs = isMaxed ? 0f : mgr.GetSecondsForLevel(rd, nextLevel);
        float partial = isMaxed ? 0f : mgr.GetPartialSecs(researchID);
        bool isPaidPaused = partial > 0f;
        int cost = isPaidPaused ? 0 : fullCost;
        float secs = Mathf.Max(0f, fullSecs - partial);
        bool canAfford = isPaidPaused || (CurrencyManager.Instance != null && CurrencyManager.Instance.CanAffordCoins(cost));
        bool isActive = FindActiveSlotFor(researchID) != null;
        bool canStart = !isActive && !isMaxed && canAfford && slotIndex >= 0;

        CloseDetail();

        detailPanel = new VisualElement { name = "research-detail" };
        detailPanel.style.position = Position.Absolute;
        detailPanel.style.top = 0; detailPanel.style.bottom = 0;
        detailPanel.style.left = 0; detailPanel.style.right = 0;
        detailPanel.style.alignItems = Align.Center;
        detailPanel.style.justifyContent = Justify.Center;
        detailPanel.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
        detailPanel.RegisterCallback<ClickEvent>(_ => CloseDetail()); // backdrop tap closes
        root.Add(detailPanel);

        var card = new VisualElement();
        card.RegisterCallback<ClickEvent>(e => e.StopPropagation()); // don't close when tapping the card
        card.style.width = Length.Percent(82);
        card.style.maxWidth = 720;
        card.style.paddingLeft = 26; card.style.paddingRight = 26;
        card.style.paddingTop = 22; card.style.paddingBottom = 22;
        card.style.backgroundColor = new Color(0.16f, 0.14f, 0.11f, 1f);
        SetAllRadius(card, 18);
        SetAllBorder(card, 2, new Color(1f, 0.84f, 0f, 0.85f));
        detailPanel.Add(card);

        // Top-right X close button — reuses the shared wood-cross asset via the .close-button class.
        var xBtn = new Button(() => CloseDetail());
        xBtn.AddToClassList("close-button");
        xBtn.style.position = Position.Absolute;
        xBtn.style.top = 8;
        xBtn.style.right = 8;
        xBtn.style.width = 56;
        xBtn.style.height = 56;
        card.Add(xBtn);

        var title = new Label(rd.displayName);
        title.style.color = new Color(1f, 0.9f, 0.65f);
        title.style.fontSize = 34;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.whiteSpace = WhiteSpace.Normal;
        title.style.marginRight = 68; // keep long titles clear of the X
        card.Add(title);

        var level = new Label(isMaxed ? $"Level {curLevel} / {rd.MaxLevel} — Maxed" : $"Level {curLevel} / {rd.MaxLevel}");
        level.style.color = new Color(1f, 1f, 1f, 0.6f);
        level.style.fontSize = 20;
        level.style.marginTop = 2;
        card.Add(level);

        string boost = BuildPickerRowSubtext(rd, curLevel, nextLevel, isMaxed);
        if (!string.IsNullOrEmpty(boost))
        {
            var boostLbl = new Label(boost);
            boostLbl.style.color = new Color(0.7f, 0.95f, 0.7f);
            boostLbl.style.fontSize = 24;
            boostLbl.style.marginTop = 12;
            boostLbl.style.whiteSpace = WhiteSpace.Normal;
            card.Add(boostLbl);
        }

        var info = new Label(isMaxed
            ? "Fully researched"
            : (isActive ? "Currently researching…"
                        : $"Cost: {(isPaidPaused ? "Paid" : cost + " coins")}     Time: {FormatRemaining(secs)}"));
        info.style.color = new Color(1f, 1f, 1f, 0.92f);
        info.style.fontSize = 24;
        info.style.marginTop = 16;
        card.Add(info);

        var buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        buttons.style.justifyContent = Justify.FlexEnd;
        buttons.style.marginTop = 22;
        card.Add(buttons);

        var closeBtn = new Button(() => CloseDetail()) { text = "Close" };
        StyleDetailButton(closeBtn, new Color(0.3f, 0.28f, 0.24f), Color.white);
        buttons.Add(closeBtn);

        if (canStart)
        {
            string capturedID = researchID;
            int capturedSlot = slotIndex;
            var researchBtn = new Button(() =>
            {
                if (ResearchManager.Instance != null && ResearchManager.Instance.TryAssignResearch(capturedSlot, capturedID))
                {
                    CloseDetail();
                    ClosePicker();
                }
            }) { text = isPaidPaused ? "Resume" : "Research" };
            StyleDetailButton(researchBtn, new Color(0.2f, 0.55f, 0.25f), Color.white);
            buttons.Add(researchBtn);
        }
        else if (!isMaxed && !isActive && !canAfford)
        {
            var cant = new Label("Not enough coins");
            cant.style.color = new Color(0.95f, 0.5f, 0.4f);
            cant.style.fontSize = 22;
            cant.style.unityTextAlign = TextAnchor.MiddleCenter;
            cant.style.marginRight = 12;
            buttons.Insert(0, cant);
        }
    }

    private void CloseDetail()
    {
        if (detailPanel != null && detailPanel.parent != null) detailPanel.RemoveFromHierarchy();
        detailPanel = null;
    }

    private static void StyleDetailButton(Button b, Color bg, Color fg)
    {
        b.style.backgroundColor = bg;
        b.style.color = fg;
        b.style.fontSize = 24;
        b.style.unityFontStyleAndWeight = FontStyle.Bold;
        b.style.paddingLeft = 24; b.style.paddingRight = 24;
        b.style.paddingTop = 12; b.style.paddingBottom = 12;
        b.style.marginLeft = 10;
        SetAllRadius(b, 12);
        b.style.borderTopWidth = 0; b.style.borderBottomWidth = 0;
        b.style.borderLeftWidth = 0; b.style.borderRightWidth = 0;
    }

    private static void SetAllRadius(VisualElement el, float r)
    {
        el.style.borderTopLeftRadius = r; el.style.borderTopRightRadius = r;
        el.style.borderBottomLeftRadius = r; el.style.borderBottomRightRadius = r;
    }

    private static void SetAllBorder(VisualElement el, float w, Color c)
    {
        el.style.borderTopWidth = w; el.style.borderBottomWidth = w;
        el.style.borderLeftWidth = w; el.style.borderRightWidth = w;
        el.style.borderTopColor = c; el.style.borderBottomColor = c;
        el.style.borderLeftColor = c; el.style.borderRightColor = c;
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
        int fullCost = isMaxed ? 0 : mgr.GetCostForLevel(rd, nextLevel);
        float fullSecs = isMaxed ? 0f : mgr.GetSecondsForLevel(rd, nextLevel);
        float partial = isMaxed ? 0f : mgr.GetPartialSecs(rd.researchID);
        bool isPaidPaused = partial > 0f;
        int cost = isPaidPaused ? 0 : fullCost; // already paid — resume free
        float secs = Mathf.Max(0f, fullSecs - partial);
        bool canAfford = !isMaxed && (isPaidPaused || (CurrencyManager.Instance != null && CurrencyManager.Instance.CanAffordCoins(cost)));

        ResearchSlotState activeSlot = FindActiveSlotFor(rd.researchID);
        bool isActive = activeSlot != null;

        var row = new VisualElement(); row.AddToClassList("picker-row");

        var textCol = new VisualElement(); textCol.AddToClassList("picker-row__text-col");

        var nameRow = new VisualElement();
        nameRow.style.flexDirection = FlexDirection.Row;
        nameRow.style.alignItems = Align.Center;

        bool isNewResearch = NewContentTracker.Instance != null &&
                             NewContentTracker.Instance.IsNew(NewContentTracker.ResearchId(rd.researchID));
        VisualElement inlineDot = null;
        if (isNewResearch)
        {
            inlineDot = MakeInlineNewDot();
            nameRow.Add(inlineDot); // sits just left of the name; spacing flows with text length
        }

        var name = new Label($"{rd.displayName} — {curLevel}/{rd.MaxLevel}");
        name.AddToClassList("picker-row__name");
        nameRow.Add(name);

        var desc = new Label(BuildPickerRowSubtext(rd, curLevel, nextLevel, isMaxed));
        desc.AddToClassList("picker-row__desc");
        textCol.Add(nameRow); textCol.Add(desc);

        var metaCol = new VisualElement(); metaCol.AddToClassList("picker-row__meta-col");
        var costLbl = new Label(isMaxed ? "Complete" : (isPaidPaused ? "Paid" : $"{cost} coins"));
        costLbl.AddToClassList("picker-row__cost");
        string timeText = isMaxed ? "Maxed" : (isPaidPaused ? FormatActiveTimer(secs) : FormatRemaining(secs));
        var timeLbl = new Label(timeText);
        timeLbl.AddToClassList("picker-row__time");
        metaCol.Add(costLbl); metaCol.Add(timeLbl);

        row.Add(textCol); row.Add(metaCol);

        if (isActive)
        {
            row.AddToClassList("picker-row--active");
            costLbl.AddToClassList("picker-row__cost--active");
            timeLbl.AddToClassList("picker-row__time--active");
            costLbl.text = "ACTIVE";
            UpdateActiveRowTime(timeLbl, rd, activeSlot);
            timeLbl.schedule.Execute(() => UpdateActiveRowTime(timeLbl, rd, activeSlot)).Every(1000);
        }
        else if (isMaxed)
        {
            row.AddToClassList("picker-row--complete");
        }
        else if (isPaidPaused)
        {
            timeLbl.AddToClassList("picker-row__time--resume");
        }
        else if (!canAfford)
        {
            row.AddToClassList("picker-row--disabled");
            costLbl.AddToClassList("picker-row__cost--unaffordable");
        }

        // Tap any row to open its detail popup — that's where research is actually started.
        // Opening details also clears the NEW badge.
        {
            string capturedID = rd.researchID;
            int capturedSlot = pickerSlotIndex;
            VisualElement capturedDot = inlineDot;
            row.RegisterCallback<ClickEvent>(_ => OpenResearchDetail(capturedID, capturedSlot, capturedDot));
            WirePressedFeedback(row, "slot-card--pressed");
        }

        return row;
    }

    /// <summary>Larger inline NEW dot that sits just left of the research name.</summary>
    private static VisualElement MakeInlineNewDot()
    {
        var dot = new VisualElement { name = "new-dot-inline" };
        dot.pickingMode = PickingMode.Ignore;
        dot.style.flexShrink = 0;
        dot.style.width = 20;
        dot.style.height = 20;
        dot.style.marginRight = 10;
        dot.style.backgroundColor = new Color(0.92f, 0.22f, 0.22f);
        dot.style.borderTopLeftRadius = 10;
        dot.style.borderTopRightRadius = 10;
        dot.style.borderBottomLeftRadius = 10;
        dot.style.borderBottomRightRadius = 10;
        dot.style.borderTopWidth = 2;
        dot.style.borderBottomWidth = 2;
        dot.style.borderLeftWidth = 2;
        dot.style.borderRightWidth = 2;
        var b = new Color(1f, 1f, 1f, 0.92f);
        dot.style.borderTopColor = b;
        dot.style.borderBottomColor = b;
        dot.style.borderLeftColor = b;
        dot.style.borderRightColor = b;
        return dot;
    }

    private static string BuildPickerRowSubtext(ResearchData rd, int curLevel, int nextLevel, bool isMaxed)
    {
        if (isMaxed) return "";
        if (rd.IsBinary || rd.bonusPerLevel <= 0f)
            return string.IsNullOrEmpty(rd.description) ? "" : rd.description;
        float pct = rd.bonusPerLevel * 100f;
        return $"Upgrade to Level {nextLevel}: +{pct.ToString("0.##")}%";
    }

    private ResearchSlotState FindActiveSlotFor(string researchID)
    {
        var mgr = ResearchManager.Instance;
        if (mgr == null || string.IsNullOrEmpty(researchID)) return null;
        for (int i = 0; i < ResearchManager.SlotCount; i++)
        {
            var s = mgr.GetSlot(i);
            if (s != null && !s.IsIdle && s.activeResearchID == researchID) return s;
        }
        return null;
    }

    private void UpdateActiveRowTime(Label timeLbl, ResearchData rd, ResearchSlotState state)
    {
        if (timeLbl == null || state == null || rd == null) return;
        var mgr = ResearchManager.Instance;
        if (mgr == null) return;
        int nextLevel = state.currentLevel + 1;
        float secsForLevel = mgr.GetSecondsForLevel(rd, nextLevel);
        double elapsed = (DateTime.UtcNow.Ticks - state.startUtcTicks) / (double)TimeSpan.TicksPerSecond;
        double remaining = Math.Max(0, secsForLevel - elapsed);
        bool boosted = state.boostMultiplier > 1.0f && state.boostExpiresUtcTicks > DateTime.UtcNow.Ticks;
        timeLbl.text = boosted
            ? $"{FormatActiveTimer(remaining)} ⚡ {state.boostMultiplier:F0}x"
            : FormatActiveTimer(remaining);
    }

    private static void WirePressedFeedback(VisualElement ve, string pressedClass)
    {
        ve.RegisterCallback<PointerDownEvent>(_ => ve.AddToClassList(pressedClass));
        ve.RegisterCallback<PointerUpEvent>(_ => ve.RemoveFromClassList(pressedClass));
        ve.RegisterCallback<PointerLeaveEvent>(_ => ve.RemoveFromClassList(pressedClass));
        ve.RegisterCallback<PointerCancelEvent>(_ => ve.RemoveFromClassList(pressedClass));
    }
}
