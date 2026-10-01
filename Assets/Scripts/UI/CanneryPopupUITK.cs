using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Cannery panel: firebox hearth (animated full-width fire + log bed), a strip of the cannable
/// crops you can bulk-load, the harvest-intake switch, and a scrolling rack with one JAR per slot
/// the building can ever own. The jar is the cell — always drawn, filling with preserve as crops
/// go in — so the panel reads as shelves of product rather than a list.
///
/// Structure mirrors SmokehousePopupUITK: the grid only rebuilds when its shape changes (a 1s full
/// rebuild eats taps), finished jars stay parked in the cell they cooked in, and collecting plays
/// a "+1" burst. Selling lives in the Inventory.
/// </summary>
[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(1000)]
public class CanneryPopupUITK : MonoBehaviour
{
    public static CanneryPopupUITK Instance { get; private set; }

    private const int FlameFrames = 6;
    private const long FlameFrameMs = 140;
    private const int FireRowTiles = 17;   // enough to overflow the wider panel; hearth clips it

    [Header("Data")]
    [Tooltip("Source for the cannable crop list shown above the rack.")]
    [SerializeField] private CropDatabase cropDatabase;

    private UIDocument document;
    private VisualElement root, popupRoot, fuelFill;
    private VisualElement hearth, fireRow, logBed, jarsGrid, cropStrip, fxLayer;
    private Label headerLabel, fuelText, woodValue, fireboxHint, gridHint;
    private Button closeButton, intakeToggle, stokeButton, fillButton;

    private readonly List<VisualElement> flameEls = new List<VisualElement>();
    private readonly List<string> flameSizes = new List<string>();
    private readonly List<int> flameSlots = new List<int>();
    private readonly List<Label> cellLabels = new List<Label>();
    private readonly List<int> cellLabelSlots = new List<int>();

    private string gridSignature;
    private string fireRowVariant;
    private int flameFrame;

    private bool isOpen;
    private bool eventsSubscribed;
    private IVisualElementScheduledItem ticker, flameTicker;
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
            CanneryManager.Instance.OnChanged += OnVoidChanged;
            eventsSubscribed = true;
        }
        if (ItemInventoryManager.Instance != null) ItemInventoryManager.Instance.OnChanged += OnVoidChanged;
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnWoodChanged += OnInt;
            CurrencyManager.Instance.OnCoinsChanged += OnInt;
        }
    }

    private void UnsubscribeEvents()
    {
        if (CanneryManager.Instance != null) CanneryManager.Instance.OnChanged -= OnVoidChanged;
        if (ItemInventoryManager.Instance != null) ItemInventoryManager.Instance.OnChanged -= OnVoidChanged;
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
        if (root == null) { Debug.LogError("[CanneryPopupUITK] rootVisualElement is null"); return; }
        root.pickingMode = PickingMode.Ignore;

        popupRoot     = root.Q<VisualElement>("popup-root");
        headerLabel   = root.Q<Label>("header-title");
        woodValue     = root.Q<Label>("wood-value");
        closeButton   = root.Q<Button>("close-button");
        hearth        = root.Q<VisualElement>("firebox-hearth");
        fireRow       = root.Q<VisualElement>("firebox-firerow");
        logBed        = root.Q<VisualElement>("firebox-logbed");
        fireboxHint   = root.Q<Label>("firebox-hint");
        fuelFill      = root.Q<VisualElement>("fuel-fill");
        fuelText      = root.Q<Label>("fuel-text");
        stokeButton   = root.Q<Button>("stoke-button");
        fillButton    = root.Q<Button>("fill-button");
        cropStrip     = root.Q<VisualElement>("crop-strip");
        intakeToggle  = root.Q<Button>("intake-toggle");
        jarsGrid      = root.Q<VisualElement>("jars-grid");
        gridHint      = root.Q<Label>("grid-hint");
        fxLayer       = root.Q<VisualElement>("fx-layer");

        if (headerLabel != null) headerLabel.text = "Cannery";
    }

    private void WireCallbacks()
    {
        if (closeButton != null) closeButton.RegisterCallback<ClickEvent>(_ => Close());
        root.Q<VisualElement>("backdrop")?.RegisterCallback<ClickEvent>(_ => Close());
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
        if (hearth != null) hearth.RegisterCallback<ClickEvent>(_ =>
        {
            if (CanneryManager.Instance != null) CanneryManager.Instance.FillFurnace();
        });
        if (gridHint != null) gridHint.RegisterCallback<ClickEvent>(_ =>
        {
            if (CanneryManager.Instance != null) CanneryManager.Instance.CollectAllJars();
        });
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        TrySubscribeEvents();
        gridSignature = null;               // force a full build on open
        if (root != null) root.pickingMode = PickingMode.Position;
        if (popupRoot != null)
        {
            popupRoot.style.display = DisplayStyle.Flex;
            popupRoot.schedule.Execute(() => popupRoot.AddToClassList("open")).StartingIn(0);
        }
        Refresh();
        ticker = root.schedule.Execute(() => { if (isOpen) Refresh(); }).Every(1000);
        flameTicker = root.schedule.Execute(AdvanceFlames).Every(FlameFrameMs);
        OnboardingTutorials.OnMenuOpened("tip_cannery"); // one-time how-to (new players)
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        if (ticker != null) { ticker.Pause(); ticker = null; }
        if (flameTicker != null) { flameTicker.Pause(); flameTicker = null; }
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
        if (mgr == null || jarsGrid == null) return;
        var st = mgr.State;

        int wood = cm != null ? cm.Wood : 0;
        if (woodValue != null)
        {
            woodValue.text = cm != null ? $"{cm.Wood:N0} / {cm.MaxWood:N0}" : "0";
            woodValue.EnableInClassList("wood-strip-value--low", wood <= 0);
        }

        if (intakeToggle != null)
            intakeToggle.EnableInClassList("intake-toggle--off", !mgr.IntakeOn);

        int cooking = ProcessingMath.CountCooking(st);
        double ratePerSec = ProcessingMath.BurnRatePerSecond(cooking, mgr.BaseBurnPerHour, mgr.PerSlotBurnPerHour);
        bool lit = st.fuelWood > 0;

        if (fuelFill != null)
            fuelFill.style.width = Length.Percent(Mathf.Clamp01((float)(st.fuelWood / mgr.FurnaceCapacity)) * 100f);
        if (fuelText != null)
        {
            int shown = Mathf.FloorToInt((float)st.fuelWood);
            string lasts = !lit
                ? " — fire is OUT"
                : (ratePerSec > 0 ? $" — lasts {FormatDuration(st.fuelWood / ratePerSec)} at current load" : "");
            fuelText.text = $"Fuel: {shown}/{mgr.FurnaceCapacity}{lasts}";
        }

        RebuildHearth(mgr, st, wood, lit);

        int stokeCost = mgr.StokeToFinishCost();
        if (stokeButton != null)
        {
            stokeButton.text = stokeCost > 0 ? $"Stoke to finish  −{stokeCost} wood"
                             : cooking > 0   ? "Fuel covers it"
                                             : "Nothing cooking";
            stokeButton.SetEnabled(stokeCost > 0 && wood > 0);
        }
        if (fillButton != null)
        {
            int space = mgr.FurnaceCapacity - Mathf.CeilToInt((float)st.fuelWood);
            int fillAmount = Mathf.Min(space, wood);
            fillButton.text = $"Fill furnace  −{Mathf.Max(0, fillAmount)} wood";
            fillButton.SetEnabled(fillAmount > 0);
        }

        RebuildCropStrip(mgr);
        RebuildGrid(mgr, st, ratePerSec, lit);
    }

    // ── Firebox hearth ───────────────────────────────────────────────────

    private void RebuildHearth(CanneryManager mgr, CanneryState st, int wood, bool lit)
    {
        if (hearth == null) return;
        hearth.EnableInClassList("firebox-hearth--out", !lit);

        if (fireRow != null)
        {
            if (fireRow.childCount != FireRowTiles)
            {
                fireRow.Clear();
                for (int i = 0; i < FireRowTiles; i++)
                {
                    var tile = new VisualElement();
                    tile.AddToClassList("firerow-tile");
                    fireRow.Add(tile);
                }
            }
            fireRow.style.display = lit ? DisplayStyle.Flex : DisplayStyle.None;
            if (lit)
            {
                float ratio = mgr.FurnaceCapacity > 0
                    ? Mathf.Clamp01((float)(st.fuelWood / mgr.FurnaceCapacity)) : 0f;
                string variant = ratio >= 0.75f ? "firerow--roaring"
                               : ratio >= 0.45f ? "firerow--strong"
                               : ratio >= 0.15f ? "firerow--low"
                                                : "firerow--embers";
                if (fireRowVariant != variant)
                {
                    for (int i = 0; i < fireRow.childCount; i++) ClearFrameClasses(fireRow[i], fireRowVariant);
                    fireRowVariant = variant;
                }
                for (int i = 0; i < fireRow.childCount; i++) ApplyFrame(fireRow[i], fireRowVariant, flameFrame);
            }
        }

        if (logBed != null)
        {
            const int Sockets = 6;
            float ratio = mgr.FurnaceCapacity > 0
                ? Mathf.Clamp01((float)(st.fuelWood / mgr.FurnaceCapacity)) : 0f;
            int filled = Mathf.CeilToInt(ratio * Sockets);
            if (logBed.childCount != Sockets)
            {
                logBed.Clear();
                for (int i = 0; i < Sockets; i++)
                {
                    var socket = new VisualElement();
                    socket.AddToClassList("log-socket");
                    logBed.Add(socket);
                }
            }
            for (int i = 0; i < Sockets; i++)
                logBed[i].EnableInClassList("log-socket--filled", i < filled);
        }

        if (fireboxHint != null)
        {
            bool full = st.fuelWood >= mgr.FurnaceCapacity;
            fireboxHint.text = wood <= 0 ? "No wood — go chop some"
                             : full      ? "Furnace full"
                             : !lit      ? "Fire is out — tap to add logs"
                                         : "Tap to add logs";
        }
    }

    // ── Crop strip ───────────────────────────────────────────────────────

    /// <summary>
    /// The cannable crops, bare like the Smokehouse fish. Big number = how many you're holding,
    /// small number = how many one tap drops into a jar.
    /// </summary>
    private void RebuildCropStrip(CanneryManager mgr)
    {
        if (cropStrip == null) return;
        cropStrip.Clear();
        if (cropDatabase == null || cropDatabase.allCrops == null) return;
        var inv = ItemInventoryManager.Instance;

        // Cheapest tier first, so the strip reads left-to-right as quick jam → slow sauce.
        var cannable = new List<CropData>();
        foreach (var c in cropDatabase.allCrops)
            if (c != null && c.canBeCanned) cannable.Add(c);
        cannable.Sort((a, b) => a.canneryTier.CompareTo(b.canneryTier));

        foreach (var crop in cannable)
        {
            int held = inv != null ? inv.GetCrop(crop.cropName) : 0;
            int batch = mgr.UnitsNeededFor(crop);
            bool loadable = mgr.CanBulkLoad(crop);

            var item = new VisualElement();
            item.AddToClassList("crop-item");
            item.EnableInClassList("crop-item--none", !loadable);

            var icon = new VisualElement();
            icon.AddToClassList("crop-icon");
            // The harvested product, not the plant it came off — same sprite the Inventory uses.
            var sprite = crop.cropSprite != null ? crop.cropSprite : crop.harvestableSprite;
            if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite);
            icon.pickingMode = PickingMode.Ignore;

            var countLabel = new Label($"x{held:N0}");
            countLabel.AddToClassList("crop-count");
            countLabel.pickingMode = PickingMode.Ignore;

            // How many units this tap actually drops in — it tops up a part-filled jar, so the
            // number is contextual rather than a fixed "per jar" figure.
            var batchLabel = new Label($"+{batch}");
            batchLabel.AddToClassList("crop-batch");
            batchLabel.pickingMode = PickingMode.Ignore;

            item.Add(icon);
            item.Add(countLabel);
            item.Add(batchLabel);

            if (loadable)
            {
                var c = crop;
                item.RegisterCallback<PointerDownEvent>(_ => item.AddToClassList("crop-item--pressed"));
                item.RegisterCallback<PointerUpEvent>(_ => item.RemoveFromClassList("crop-item--pressed"));
                item.RegisterCallback<PointerLeaveEvent>(_ => item.RemoveFromClassList("crop-item--pressed"));
                item.RegisterCallback<PointerCaptureOutEvent>(_ => item.RemoveFromClassList("crop-item--pressed"));
                item.RegisterCallback<ClickEvent>(_ =>
                {
                    item.RemoveFromClassList("crop-item--pressed");
                    if (CanneryManager.Instance != null) CanneryManager.Instance.TryBulkLoad(c);
                });
            }
            cropStrip.Add(item);
        }
    }

    // ── Jar rack ─────────────────────────────────────────────────────────

    private string GridSignature(CanneryManager mgr, CanneryState st, bool lit)
    {
        var cm = CurrencyManager.Instance;
        var sb = new System.Text.StringBuilder();
        sb.Append(mgr.SlotsOwned).Append('|')
          .Append(mgr.EffectiveMaxPurchasableSlots).Append('|')
          .Append(mgr.TotalMaxSlots).Append('|')
          .Append(lit ? 1 : 0).Append('|')
          .Append(mgr.CanBuySlot() ? 1 : 0);

        for (int i = 0; i < st.slots.Length; i++)
        {
            var s = st.slots[i];
            sb.Append('|');
            if (mgr.ReadyIndexForSlot(i) >= 0) sb.Append('r');
            else if (ProcessingMath.SlotIsEmpty(s)) sb.Append('e');
            else
            {
                // Fill level is part of the SHAPE here: the jar's contents redraw as units land.
                sb.Append(s.tier).Append(ProcessingMath.SlotIsCooking(s) ? 'c' : 'l')
                  .Append(s.unitsLoaded).Append('/').Append(s.unitsRequired);
            }
        }
        for (int i = mgr.SlotsOwned; i < mgr.EffectiveMaxPurchasableSlots; i++)
            sb.Append('|')
              .Append(cm != null && cm.Coins >= mgr.SlotCoinCostAt(i) ? 1 : 0)
              .Append(cm != null && cm.Wood >= mgr.SlotWoodCostAt(i) ? 1 : 0);
        return sb.ToString();
    }

    private void UpdateGridInPlace(CanneryState st, double ratePerSec)
    {
        for (int i = 0; i < cellLabels.Count; i++)
        {
            int slot = cellLabelSlots[i];
            if (slot < 0 || slot >= st.slots.Length) continue;
            var s = st.slots[slot];
            if (ProcessingMath.SlotIsCooking(s)) cellLabels[i].text = FormatDuration(s.cookSecondsRemaining);
        }
        for (int i = 0; i < flameEls.Count; i++)
        {
            int slot = flameSlots[i];
            if (slot < 0 || slot >= st.slots.Length) continue;
            string size = FlameSizeFor(st.slots[slot].cookSecondsRemaining, ratePerSec);
            if (size != flameSizes[i])
            {
                ClearFrameClasses(flameEls[i], flameSizes[i]);
                flameEls[i].RemoveFromClassList(flameSizes[i]);
                flameEls[i].AddToClassList(size);
                flameSizes[i] = size;
            }
            ApplyFrame(flameEls[i], size, flameFrame);
        }
    }

    /// <summary>
    /// One jar per slot the Cannery can ever own, in fixed order. A finished jar stays parked in
    /// its own cell until collected and a collected cell simply goes empty — the rack never sorts.
    /// </summary>
    private void RebuildGrid(CanneryManager mgr, CanneryState st, double ratePerSec, bool lit)
    {
        string signature = GridSignature(mgr, st, lit);
        if (signature == gridSignature && jarsGrid.childCount > 0)
        {
            UpdateGridInPlace(st, ratePerSec);
            return;
        }
        gridSignature = signature;

        jarsGrid.Clear();
        flameEls.Clear();
        flameSizes.Clear();
        flameSlots.Clear();
        cellLabels.Clear();
        cellLabelSlots.Clear();

        int owned = mgr.SlotsOwned;
        int total = Mathf.Max(owned, mgr.TotalMaxSlots);
        int purchasable = mgr.EffectiveMaxPurchasableSlots;
        int shownParked = 0;

        for (int i = 0; i < total; i++)
        {
            VisualElement cell;
            if (i < owned)
            {
                var s = i < st.slots.Length ? st.slots[i] : null;
                int readyIdx = mgr.ReadyIndexForSlot(i);
                if (readyIdx >= 0) { cell = BuildFinishedJar(mgr, readyIdx, i); shownParked++; }
                else if (!ProcessingMath.SlotIsEmpty(s)) cell = BuildWorkingJar(s, i, ratePerSec, lit);
                else cell = BuildEmptyJar();
            }
            else cell = BuildLockedJar(mgr, i, i < purchasable);
            jarsGrid.Add(cell);
        }

        int parked = 0;
        for (int i = 0; i < mgr.State.readyJars.Count; i++)
            if (mgr.State.readyJars[i] != null && mgr.State.readyJars[i].slotIndex >= 0) parked++;
        int stranded = parked - shownParked;

        if (gridHint != null)
        {
            if (stranded > 0)
            {
                gridHint.text = $"{stranded} finished with no cell — tap to collect";
                gridHint.style.display = DisplayStyle.Flex;
            }
            // No blanket research note any more — each gated jar has its own research now.
            else gridHint.style.display = DisplayStyle.None;
        }
    }

    // Preserve fills the glass in quarters, so a jar reads as "a quarter full" at a glance
    // instead of as a smooth meter. Any contents at all show at least one quarter.
    private const float JarFillMaxPercent = 47f;   // 9%..59% of the sprite, inset to the glass wall

    /// <summary>A jar figure: glass sprite with the preserve painted inside it.</summary>
    private static VisualElement JarFigure(Color fill, float fraction)
    {
        var figure = new VisualElement();
        figure.AddToClassList("jar-figure");

        var glass = new VisualElement();
        glass.AddToClassList("jar-glass");
        glass.pickingMode = PickingMode.Ignore;

        int quarter = fraction <= 0f ? 0 : Mathf.Clamp(Mathf.CeilToInt(fraction * 4f), 1, 4);
        var liquid = new VisualElement();
        liquid.AddToClassList("jar-fill");
        liquid.pickingMode = PickingMode.Ignore;
        liquid.style.height = Length.Percent(quarter / 4f * JarFillMaxPercent);
        liquid.style.backgroundColor = quarter > 0 ? fill : new Color(0, 0, 0, 0);

        figure.Add(glass);    // bottle art
        figure.Add(liquid);   // preserve, painted inside the glass
        return figure;
    }

    /// <summary>One icon + amount line inside a locked jar's price block.</summary>
    private static VisualElement CostLine(string iconClass, string text, bool unaffordable)
    {
        var line = new VisualElement();
        line.AddToClassList("jar-cost");
        var icon = new VisualElement();
        icon.AddToClassList("jar-cost-icon");
        icon.AddToClassList(iconClass);
        var label = new Label(text);
        label.AddToClassList("jar-cost-text");
        if (unaffordable) label.AddToClassList("jar-cost-text--unaffordable");
        line.Add(icon);
        line.Add(label);
        return line;
    }

    private VisualElement NewCell(params string[] extra)
    {
        var cell = new VisualElement();
        cell.AddToClassList("jar-cell");
        for (int i = 0; i < extra.Length; i++)
            if (!string.IsNullOrEmpty(extra[i])) cell.AddToClassList(extra[i]);
        return cell;
    }

    /// <summary>The fixed-height strip under a jar. Every state fills this instead of growing
    /// the cell, which is what keeps the jars on a shelf aligned.</summary>
    private static VisualElement NewFooter(VisualElement cell)
    {
        var footer = new VisualElement();
        footer.AddToClassList("jar-footer");
        cell.Add(footer);
        return footer;
    }

    /// <summary>Filling or cooking: jar shows its contents, plus a fire and a clock underneath.</summary>
    private VisualElement BuildWorkingJar(CannerySlot s, int slotIndex, double ratePerSec, bool lit)
    {
        var cell = NewCell();
        var crop = FindCrop(s.cropId);
        Color fill = crop != null ? crop.jarFillColor : new Color(0.78f, 0.28f, 0.30f, 1f);
        float fraction = s.unitsRequired > 0 ? (float)s.unitsLoaded / s.unitsRequired : 0f;

        cell.Add(JarFigure(fill, fraction));

        var status = new VisualElement();
        status.AddToClassList("jar-status");

        var label = new Label();
        label.AddToClassList("jar-label");

        // Three distinct states, and only two of them involve the fire:
        //   cooking + lit  → live flame and a countdown
        //   cooking + out  → cold coals and a red, stopped clock
        //   still filling  → just the count; no fire has been asked for yet, so no fire glyph
        bool cookingNow = ProcessingMath.SlotIsCooking(s);
        bool showStatus = cookingNow;
        if (cookingNow && lit)
        {
            string size = FlameSizeFor(s.cookSecondsRemaining, ratePerSec);
            status.AddToClassList(size);
            ApplyFrame(status, size, flameFrame);
            flameEls.Add(status);
            flameSizes.Add(size);
            flameSlots.Add(slotIndex);
            label.text = FormatDuration(s.cookSecondsRemaining);
        }
        else if (cookingNow)
        {
            status.AddToClassList("jar-status--unlit");
            label.AddToClassList("jar-label--paused");
            label.text = FormatDuration(s.cookSecondsRemaining);
        }
        else
        {
            label.AddToClassList("jar-label--muted");
            label.text = $"{s.unitsLoaded}/{s.unitsRequired}";
        }

        cellLabels.Add(label);
        cellLabelSlots.Add(slotIndex);

        var footer = NewFooter(cell);
        if (showStatus) footer.Add(status);
        else footer.Add(StatusSpacer());  // hold the row so the count sits where a timer would
        footer.Add(label);
        return cell;
    }

    /// <summary>Invisible stand-in for the fire glyph, so a filling jar's count doesn't jump up
    /// into the space a timer would occupy.</summary>
    private static VisualElement StatusSpacer()
    {
        var spacer = new VisualElement();
        spacer.AddToClassList("jar-status");
        spacer.pickingMode = PickingMode.Ignore;
        return spacer;
    }

    /// <summary>A finished jar waiting to be taken off the rack.</summary>
    private VisualElement BuildFinishedJar(CanneryManager mgr, int readyIndex, int slotIndex)
    {
        var cell = NewCell();
        var jar = mgr.ReadyAt(readyIndex);
        var crop = FindCrop(jar != null ? jar.sourceId : null);
        Color fill = crop != null ? crop.jarFillColor : new Color(0.78f, 0.28f, 0.30f, 1f);

        cell.Add(JarFigure(fill, 1f));

        var collect = new Button { text = "Collect" };
        collect.AddToClassList("jar-collect");
        int idx = readyIndex;
        collect.RegisterCallback<ClickEvent>(_ =>
        {
            var m = CanneryManager.Instance;
            if (m == null) return;
            Rect from = cell.worldBound;
            if (m.TryCollectJar(idx)) PlayCollectFx(from);
        });
        NewFooter(cell).Add(collect);
        return cell;
    }

    private VisualElement BuildEmptyJar()
    {
        var cell = NewCell();
        cell.Add(JarFigure(new Color(0, 0, 0, 0), 0f));
        var label = new Label("Empty");
        label.AddToClassList("jar-label");
        label.AddToClassList("jar-label--muted");
        NewFooter(cell).Add(label);
        return cell;
    }

    private VisualElement BuildLockedJar(CanneryManager mgr, int index, bool purchasable)
    {
        var cell = NewCell("jar-cell--locked");
        // Jar still present, just faded (USS) — the card behind it carries the price's contrast.
        cell.Add(JarFigure(new Color(0, 0, 0, 0), 0f));

        var cost = new VisualElement();
        cost.AddToClassList("jar-cost-overlay");

        var button = new Button();
        button.AddToClassList("jar-buy");

        if (purchasable)
        {
            // Slots cost coins AND wood; showing only half the price would mislead.
            var cm = CurrencyManager.Instance;
            int coinCost = mgr.SlotCoinCostAt(index);
            int woodCost = mgr.SlotWoodCostAt(index);
            cost.Add(CostLine("jar-cost-icon--coins", $"{coinCost:N0}", cm != null && cm.Coins < coinCost));
            cost.Add(CostLine("jar-cost-icon--wood", $"{woodCost:N0}", cm != null && cm.Wood < woodCost));

            button.text = "Unlock";
            button.SetEnabled(index == mgr.SlotsOwned && mgr.CanBuySlot());
            button.RegisterCallback<ClickEvent>(_ =>
            {
                if (CanneryManager.Instance != null) CanneryManager.Instance.TryBuySlot();
            });
        }
        else
        {
            cost.Add(CostLine("jar-cost-icon--research", "Research", false));
            button.text = "Research";
            button.SetEnabled(ResearchPopupUITK.Instance != null);
            button.RegisterCallback<ClickEvent>(_ =>
            {
                if (ResearchPopupUITK.Instance == null) return;
                Close();
                ResearchPopupUITK.Instance.Open();
            });
        }

        cell.Add(cost);
        NewFooter(cell).Add(button);
        return cell;
    }

    /// <summary>CropData behind a slot's stored cropId (the CropData asset name).</summary>
    private CropData FindCrop(string cropId)
    {
        if (string.IsNullOrEmpty(cropId) || cropDatabase == null || cropDatabase.allCrops == null) return null;
        foreach (var c in cropDatabase.allCrops)
            if (c != null && c.name == cropId) return c;
        return null;
    }

    private string FlameSizeFor(double cookSecondsRemaining, double ratePerSec)
    {
        var mgr = CanneryManager.Instance;
        if (mgr == null || ratePerSec <= 0) return "flame--large";
        double burnSecondsLeft = mgr.State.fuelWood / ratePerSec;
        if (cookSecondsRemaining <= 0) return "flame--large";
        double coverage = burnSecondsLeft / cookSecondsRemaining;
        if (coverage >= 1.0) return "flame--large";
        if (coverage >= 0.5) return "flame--medium";
        if (coverage >= 0.2) return "flame--small";
        return "flame--ember";
    }

    /// <summary>Floats a "+1 jar" out of the cell it came from. See feedback_collect_animations.</summary>
    private void PlayCollectFx(Rect fromWorld)
    {
        if (fxLayer == null) return;
        var layer = fxLayer.worldBound;

        var fx = new VisualElement();
        fx.AddToClassList("collect-fx");
        fx.pickingMode = PickingMode.Ignore;

        var icon = new VisualElement();
        icon.AddToClassList("collect-fx-icon");
        var label = new Label("+1");
        label.AddToClassList("collect-fx-label");
        fx.Add(icon);
        fx.Add(label);

        fx.style.left = fromWorld.center.x - layer.x - 70f;
        fx.style.top  = fromWorld.center.y - layer.y - 28f;
        fxLayer.Add(fx);

        fx.schedule.Execute(() => fx.AddToClassList("collect-fx--fly")).StartingIn(16);
        fx.schedule.Execute(() => fx.RemoveFromHierarchy()).StartingIn(850);
    }

    // ── Flame frame cycling ──────────────────────────────────────────────

    private void AdvanceFlames()
    {
        if (!isOpen) return;
        flameFrame = (flameFrame + 1) % FlameFrames;
        for (int i = 0; i < flameEls.Count; i++)
            ApplyFrame(flameEls[i], flameSizes[i], flameFrame);
        if (fireRow != null && !string.IsNullOrEmpty(fireRowVariant)
            && fireRow.style.display == DisplayStyle.Flex)
            for (int i = 0; i < fireRow.childCount; i++)
                ApplyFrame(fireRow[i], fireRowVariant, flameFrame);
    }

    private static void ApplyFrame(VisualElement el, string variant, int frame)
    {
        if (el == null || string.IsNullOrEmpty(variant)) return;
        string want = variant + "-" + frame;
        if (el.userData as string == want) return;
        ClearFrameClasses(el, variant);
        el.AddToClassList(want);
        el.userData = want;
    }

    private static void ClearFrameClasses(VisualElement el, string variant)
    {
        if (el == null || string.IsNullOrEmpty(variant)) return;
        for (int i = 0; i < FlameFrames; i++) el.RemoveFromClassList(variant + "-" + i);
        el.userData = null;
    }
}
