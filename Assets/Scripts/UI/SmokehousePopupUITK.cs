using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Smokehouse panel: firebox hearth (animated flame + log bed) with stoke/fill, raw-fish rows
/// (smoke / sell), a smoker slot GRID whose cells carry the per-slot state, and smoked-fish rows
/// (sell). Lifecycle mirrors CanneryPopupUITK — rebuilds on a 1s schedule while open (live
/// countdowns) and on manager/pantry change events, with a faster ticker that only cycles the
/// flame frames.
/// </summary>
[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(1000)]
public class SmokehousePopupUITK : MonoBehaviour
{
    public static SmokehousePopupUITK Instance { get; private set; }

    // Flame sheets are 6 frames; ~7fps reads as a lively fire without churning the panel.
    private const int FlameFrames = 6;
    private const long FlameFrameMs = 140;

    // The hearth fire is a strip of seamless tiles; enough to overflow the widest panel, clipped.
    private const int FireRowTiles = 13;

    private UIDocument document;
    private VisualElement root, popupRoot, fuelFill;
    private VisualElement hearth, hearthGlow, fireRow, logBed, slotsGrid, rawStrip, fxLayer;
    private Label headerLabel, fuelText, woodValue, fireboxHint, gridHint;
    private Button closeButton, stokeButton, fillButton;

    // Elements currently showing an animated flame, with the size-variant class each one uses
    // and the slot it belongs to (so the size can be re-evaluated without a rebuild).
    private readonly List<VisualElement> flameEls = new List<VisualElement>();
    private readonly List<string> flameSizes = new List<string>();
    private readonly List<int> flameSlots = new List<int>();

    // Live countdown labels, kept so ticks can update text in place.
    private readonly List<Label> cellTimers = new List<Label>();
    private readonly List<int> cellTimerSlots = new List<int>();

    // Shape of the grid as last built. Rebuilding only when this changes keeps buttons alive
    // between a press and its release — a 1s full rebuild silently ate taps on Collect/Unlock.
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
        woodValue   = root.Q<Label>("wood-value");
        closeButton = root.Q<Button>("close-button");
        hearth      = root.Q<VisualElement>("firebox-hearth");
        hearthGlow  = root.Q<VisualElement>("firebox-glow");
        fireRow     = root.Q<VisualElement>("firebox-firerow");
        logBed      = root.Q<VisualElement>("firebox-logbed");
        fireboxHint = root.Q<Label>("firebox-hint");
        fuelFill    = root.Q<VisualElement>("fuel-fill");
        fuelText    = root.Q<Label>("fuel-text");
        stokeButton = root.Q<Button>("stoke-button");
        fillButton  = root.Q<Button>("fill-button");
        rawStrip    = root.Q<VisualElement>("raw-strip");
        slotsGrid   = root.Q<VisualElement>("slots-grid");
        gridHint    = root.Q<Label>("grid-hint");
        fxLayer     = root.Q<VisualElement>("fx-layer");

        if (headerLabel != null) headerLabel.text = "Smokehouse";
    }

    private void WireCallbacks()
    {
        if (closeButton != null) closeButton.RegisterCallback<ClickEvent>(_ => Close());
        // Tap outside the card (on the dim backdrop) closes the popup, like the other modals.
        root.Q<VisualElement>("backdrop")?.RegisterCallback<ClickEvent>(_ => Close());
        if (stokeButton != null) stokeButton.RegisterCallback<ClickEvent>(_ =>
        {
            if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.StokeToFinish();
        });
        if (fillButton != null) fillButton.RegisterCallback<ClickEvent>(_ =>
        {
            if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.FillFurnace();
        });
        // The hearth itself is the primary "put logs on the fire" affordance.
        if (hearth != null) hearth.RegisterCallback<ClickEvent>(_ =>
        {
            if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.FillFurnace();
        });
        // Escape hatch for finished fish that have no free cell to sit in.
        if (gridHint != null) gridHint.RegisterCallback<ClickEvent>(_ =>
        {
            if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.CollectAll();
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
        // Separate, faster tick: swaps flame frames only, so the fire animates without
        // rebuilding rows 7 times a second.
        flameTicker = root.schedule.Execute(AdvanceFlames).Every(FlameFrameMs);
        OnboardingTutorials.OnMenuOpened("tip_smokehouse"); // one-time how-to (new players)
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

    // Maps a fish tier (1 Perch / 2 Bass / 3 Northern Pike) to its USS icon modifier class. The
    // per-tier class carries the sprite via background-image in the USS, matching the icon pattern
    // used elsewhere (e.g. WoodRack's .wood-icon). Smoked rows use the cured art so the two
    // shelves are told apart by the icon, not just the word "Smoked".
    private static string FishIconClass(int tier, bool smoked)
    {
        switch (Mathf.Clamp(tier, 1, FishTiers.Count))
        {
            case 1:  return smoked ? "fish-icon--perch-smoked" : "fish-icon--perch";
            case 2:  return smoked ? "fish-icon--bass-smoked"  : "fish-icon--bass";
            default: return smoked ? "fish-icon--pike-smoked"  : "fish-icon--pike";
        }
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
        if (mgr == null || slotsGrid == null) return;
        var st = mgr.State;

        int wood = cm != null ? cm.Wood : 0;

        // Wood on hand, in the title bar — every button below spends it.
        if (woodValue != null)
        {
            woodValue.text = cm != null ? $"{cm.Wood:N0} / {cm.MaxWood:N0}" : "0";
            woodValue.EnableInClassList("wood-strip-value--low", wood <= 0);
        }

        int cooking = ProcessingMath.CountCooking(st);
        double ratePerSec = ProcessingMath.BurnRatePerSecond(cooking, mgr.BaseBurnPerHour, mgr.PerSlotBurnPerHour);
        bool lit = st.fuelWood > 0;

        if (fuelFill != null)
            fuelFill.style.width = Length.Percent(Mathf.Clamp01((float)(st.fuelWood / mgr.FurnaceCapacity)) * 100f);
        if (fuelText != null)
        {
            // Floor the display but branch on the real value, so "0" never claims a burn time.
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
            // Zero cost means two very different things: nothing is cooking, or the fire already
            // holds enough wood to see the current batch through.
            stokeButton.text = stokeCost > 0 ? $"Stoke to finish  −{stokeCost} wood"
                             : cooking > 0   ? "Fuel covers it"
                                             : "Nothing smoking";
            stokeButton.SetEnabled(stokeCost > 0 && wood > 0);
        }
        if (fillButton != null)
        {
            int space = mgr.FurnaceCapacity - Mathf.CeilToInt((float)st.fuelWood);
            int fillAmount = Mathf.Min(space, wood);
            fillButton.text = $"Fill furnace  −{Mathf.Max(0, fillAmount)} wood";
            fillButton.SetEnabled(fillAmount > 0);
        }

        RebuildRawStrip(mgr);
        RebuildGrid(mgr, st, ratePerSec, lit);
    }

    // ── Firebox hearth ───────────────────────────────────────────────────

    private void RebuildHearth(SmokehouseManager mgr, CanneryState st, int wood, bool lit)
    {
        if (hearth == null) return;
        hearth.EnableInClassList("firebox-hearth--out", !lit);

        // A bed of fire across the whole hearth. Height of the blaze tracks how full the furnace
        // is, so the firebox answers "how much wood is in there" at a glance.
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
                bool variantChanged = fireRowVariant != variant;
                if (variantChanged)
                {
                    for (int i = 0; i < fireRow.childCount; i++)
                        ClearFrameClasses(fireRow[i], fireRowVariant);
                    fireRowVariant = variant;
                }
                for (int i = 0; i < fireRow.childCount; i++)
                    ApplyFrame(fireRow[i], fireRowVariant, flameFrame);
            }
        }

        // Log bed: one socket per sixth of capacity. Empty sockets are the "put logs here" slot.
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

    /// <summary>
    /// Raw stock as three big tappable fish. Tapping one loads it straight into the smoker —
    /// this strip is an input, not a shop; raw fish are sold from the Inventory.
    /// </summary>
    private void RebuildRawStrip(SmokehouseManager mgr)
    {
        rawStrip.Clear();
        var pantry = PantryManager.Instance;
        bool anyEmptySlot = false;
        foreach (var s in mgr.State.slots) if (ProcessingMath.SlotIsEmpty(s)) { anyEmptySlot = true; break; }

        for (int tier = 1; tier <= FishTiers.Count; tier++)
        {
            int count = pantry != null ? pantry.GetRaw(tier) : 0;
            bool loadable = count > 0 && anyEmptySlot;

            var item = new VisualElement();
            item.AddToClassList("raw-fish");
            // Every fish always shows, so the set reads as a checklist. Fade whenever a tap would
            // do nothing — no stock, or no free cell to put it in — rather than looking live and
            // silently ignoring the press.
            item.EnableInClassList("raw-fish--none", !loadable);

            var icon = new VisualElement();
            icon.AddToClassList("raw-fish-icon");
            icon.AddToClassList(FishIconClass(tier, false));
            icon.pickingMode = PickingMode.Ignore;

            var countLabel = new Label($"x{count}");
            countLabel.AddToClassList("raw-fish-count");
            countLabel.pickingMode = PickingMode.Ignore;

            item.Add(icon);
            item.Add(countLabel);

            if (loadable)
            {
                int t = tier;
                // Press: shrink + fade while held. Release: the fish goes into the smoker.
                item.RegisterCallback<PointerDownEvent>(_ => item.AddToClassList("raw-fish--pressed"));
                item.RegisterCallback<PointerUpEvent>(_ => item.RemoveFromClassList("raw-fish--pressed"));
                item.RegisterCallback<PointerLeaveEvent>(_ => item.RemoveFromClassList("raw-fish--pressed"));
                item.RegisterCallback<PointerCaptureOutEvent>(_ => item.RemoveFromClassList("raw-fish--pressed"));
                item.RegisterCallback<ClickEvent>(_ =>
                {
                    item.RemoveFromClassList("raw-fish--pressed");
                    if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.TryLoadFish(t);
                });
            }
            rawStrip.Add(item);
        }
    }

    // ── Smoker grid ──────────────────────────────────────────────────────

    /// <summary>
    /// One cell per slot the building can ever have. Owned slots show their contents; the rest
    /// show how they are unlocked (coins while under the purchasable cap, Research beyond it).
    /// Finished fish have already left their slot (ProcessingMath.Simulate frees it), so they are
    /// back-filled into the earliest free cells as collectable "Smoked" cells.
    /// </summary>
    private void RebuildGrid(SmokehouseManager mgr, CanneryState st, double ratePerSec, bool lit)
    {
        // Countdowns change every tick but the grid's SHAPE rarely does. Tearing the cells down
        // each second destroyed the button under the player's finger, so taps on Collect/Unlock
        // were dropped whenever a rebuild landed between press and release.
        string signature = GridSignature(mgr, st, lit);
        if (signature == gridSignature && slotsGrid.childCount > 0)
        {
            UpdateGridInPlace(st, ratePerSec);
            return;
        }
        gridSignature = signature;

        slotsGrid.Clear();
        flameEls.Clear();
        flameSizes.Clear();
        flameSlots.Clear();
        cellTimers.Clear();
        cellTimerSlots.Clear();

        int owned = mgr.SlotsOwned;
        int total = Mathf.Max(owned, mgr.TotalMaxSlots);
        // Purchasable cap AFTER research expansions, so unlocked slots actually become buyable.
        int purchasable = mgr.EffectiveMaxPurchasableSlots;
        int shownReady = 0;

        for (int i = 0; i < total; i++)
        {
            VisualElement cell;
            if (i < owned)
            {
                var s = i < st.slots.Length ? st.slots[i] : null;
                // A finished fish stays in the cell it cooked in, and a collected cell just goes
                // empty. The rack is a physical rack, not a list that re-sorts itself.
                int readyIdx = mgr.ReadyIndexForSlot(i);
                if (readyIdx >= 0)
                {
                    cell = BuildReadyCell(mgr.ReadyAt(readyIdx), readyIdx);
                    shownReady++;
                }
                else if (!ProcessingMath.SlotIsEmpty(s)) cell = BuildBusyCell(s, i, ratePerSec, lit);
                else cell = BuildEmptyCell();
            }
            else cell = BuildLockedCell(mgr, i, i < purchasable);

            slotsGrid.Add(cell);
        }

        // Legacy jars saved before slotIndex existed have no cell of their own; give them an out.
        int stranded = mgr.ReadyCount - shownReady;
        if (gridHint != null)
        {
            if (stranded > 0)
            {
                gridHint.text = $"{stranded} finished with no cell — tap to collect";
                gridHint.style.display = DisplayStyle.Flex;
            }
            // No blanket research note any more — each gated cell has its own research now.
            else gridHint.style.display = DisplayStyle.None;
        }
    }

    /// <summary>One icon + amount line inside a locked cell's price block.</summary>
    private static VisualElement CostLine(string iconClass, string text, bool unaffordable = false)
    {
        var line = new VisualElement();
        line.AddToClassList("cell-cost-line");
        var icon = new VisualElement();
        icon.AddToClassList("cell-cost-icon");
        icon.AddToClassList(iconClass);
        var label = new Label(text);
        label.AddToClassList("cell-cost-text");
        label.AddToClassList("cell-cost-text--short");
        if (unaffordable) label.AddToClassList("cell-cost-text--unaffordable");
        line.Add(icon);
        line.Add(label);
        return line;
    }

    /// <summary>
    /// Everything that changes which ELEMENTS a cell needs — never the countdown value. Affordability
    /// is folded in as booleans rather than raw balances so a ticking coin count doesn't force a
    /// rebuild mid-tap.
    /// </summary>
    private string GridSignature(SmokehouseManager mgr, CanneryState st, bool lit)
    {
        var cm = CurrencyManager.Instance;
        var sb = new System.Text.StringBuilder();
        sb.Append(mgr.SlotsOwned).Append('|')
          .Append(mgr.EffectiveMaxPurchasableSlots).Append('|')
          .Append(mgr.TotalMaxSlots).Append('|')
          .Append(mgr.ReadyCount).Append('|')
          .Append(lit ? 1 : 0).Append('|')
          .Append(mgr.CanBuySlot() ? 1 : 0);

        for (int i = 0; i < st.slots.Length; i++)
        {
            var s = st.slots[i];
            sb.Append('|');
            if (mgr.ReadyIndexForSlot(i) >= 0) sb.Append('r');
            else if (ProcessingMath.SlotIsEmpty(s)) sb.Append('e');
            else sb.Append(s.tier).Append(ProcessingMath.SlotIsCooking(s) ? 'c' : 'l');
        }
        // Red-vs-normal price text on each locked-but-purchasable cell.
        for (int i = mgr.SlotsOwned; i < mgr.EffectiveMaxPurchasableSlots; i++)
        {
            sb.Append('|')
              .Append(cm != null && cm.Coins >= mgr.SlotCoinCostAt(i) ? 1 : 0)
              .Append(cm != null && cm.Wood >= mgr.SlotWoodCostAt(i) ? 1 : 0);
        }
        return sb.ToString();
    }

    /// <summary>Tick update for an unchanged grid: countdowns and flame sizes only.</summary>
    private void UpdateGridInPlace(CanneryState st, double ratePerSec)
    {
        for (int i = 0; i < cellTimers.Count; i++)
        {
            int slot = cellTimerSlots[i];
            if (slot < 0 || slot >= st.slots.Length) continue;
            cellTimers[i].text = FormatDuration(st.slots[slot].cookSecondsRemaining);
        }

        // Fuel drains as we watch, so a cell's "will it finish" flame can shrink without any
        // structural change.
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

    private VisualElement NewCell(params string[] extraClasses)
    {
        var cell = new VisualElement();
        cell.AddToClassList("slot-cell");
        for (int i = 0; i < extraClasses.Length; i++)
            if (!string.IsNullOrEmpty(extraClasses[i])) cell.AddToClassList(extraClasses[i]);
        return cell;
    }

    /// <summary>A slot holding a fish: cooking (live flame + countdown) or stalled (cold coal + red clock).</summary>
    private VisualElement BuildBusyCell(CannerySlot s, int slotIndex, double ratePerSec, bool lit)
    {
        var cell = NewCell();

        var fish = new VisualElement();
        fish.AddToClassList("cell-fish");
        fish.AddToClassList(FishIconClass(s.tier, false));

        var status = new VisualElement();
        status.AddToClassList("cell-status");

        var timer = new Label();
        timer.AddToClassList("cell-timer");

        if (lit)
        {
            // Flame size = will the fuel outlast the work left? A big flame literally means
            // "there is enough wood on the fire to finish this fish".
            string size = FlameSizeFor(s.cookSecondsRemaining, ratePerSec);
            status.AddToClassList(size);
            ApplyFrame(status, size, flameFrame);
            flameEls.Add(status);
            flameSizes.Add(size);
            flameSlots.Add(slotIndex);
            timer.text = FormatDuration(s.cookSecondsRemaining);
        }
        else
        {
            status.AddToClassList("cell-status--unlit");
            timer.AddToClassList("cell-timer--paused");
            timer.text = FormatDuration(s.cookSecondsRemaining);
        }

        cellTimers.Add(timer);
        cellTimerSlots.Add(slotIndex);

        cell.Add(fish);
        cell.Add(status);
        cell.Add(timer);
        return cell;
    }

    /// <summary>
    /// Picks the flame variant by how much of this fish's remaining cook the current fuel covers.
    /// Swap this for a plain fuel-percentage if you'd rather the flame track the furnace, not the job.
    /// </summary>
    private string FlameSizeFor(double cookSecondsRemaining, double ratePerSec)
    {
        var mgr = SmokehouseManager.Instance;
        if (mgr == null || ratePerSec <= 0) return "flame--large";
        double burnSecondsLeft = mgr.State.fuelWood / ratePerSec;
        if (cookSecondsRemaining <= 0) return "flame--large";
        double coverage = burnSecondsLeft / cookSecondsRemaining;
        if (coverage >= 1.0) return "flame--large";   // enough wood to finish
        if (coverage >= 0.5) return "flame--medium";  // more than half way
        if (coverage >= 0.2) return "flame--small";   // running short
        return "flame--ember";                        // about to stall
    }

    /// <summary>A finished fish waiting to be banked into the Pantry.</summary>
    private VisualElement BuildReadyCell(ReadyJar jar, int readyIndex)
    {
        var cell = NewCell("slot-cell--ready");

        var fish = new VisualElement();
        fish.AddToClassList("cell-fish");
        fish.AddToClassList(FishIconClass(jar != null ? jar.tier : 1, true));

        var collect = new Button { text = "Collect" };
        collect.AddToClassList("cell-btn");
        collect.AddToClassList("cell-btn--collect");
        int idx = readyIndex;
        collect.RegisterCallback<ClickEvent>(_ =>
        {
            var m = SmokehouseManager.Instance;
            if (m == null) return;
            var j = m.ReadyAt(idx);
            int tier = j != null ? j.tier : 1;
            // Grab the cell's position before collecting — the rebuild removes this element.
            Rect from = cell.worldBound;
            if (m.TryCollect(idx)) PlayCollectFx(from, tier);
        });

        cell.Add(fish);
        cell.Add(collect);
        return cell;
    }

    private VisualElement BuildEmptyCell()
    {
        var cell = NewCell("slot-cell--empty");
        var label = new Label("Empty");
        label.AddToClassList("cell-empty-label");
        cell.Add(label);
        return cell;
    }

    /// <summary>A slot that isn't owned yet — bought with coins + wood, or gated behind Research.</summary>
    private VisualElement BuildLockedCell(SmokehouseManager mgr, int index, bool purchasable)
    {
        var cell = NewCell("slot-cell--locked");

        var lockIcon = new VisualElement();
        lockIcon.AddToClassList("cell-lock");

        var costRow = new VisualElement();
        costRow.AddToClassList("cell-cost-row");

        var button = new Button { text = "Unlock" };
        button.AddToClassList("cell-btn");

        // Only the very next slot is buyable; the ones past it just show what they'll cost.
        bool isNext = index == mgr.SlotsOwned;

        if (purchasable)
        {
            // Tint whichever price you can't meet — an unaffordable slot is the usual reason
            // Unlock does nothing when tapped.
            var cm = CurrencyManager.Instance;
            int coinCost = mgr.SlotCoinCostAt(index);
            int woodCost = mgr.SlotWoodCostAt(index);
            costRow.Add(CostLine("cell-cost-icon--coins", $"{coinCost:N0}", cm != null && cm.Coins < coinCost));
            costRow.Add(CostLine("cell-cost-icon--wood", $"{woodCost:N0}", cm != null && cm.Wood < woodCost));

            button.SetEnabled(isNext && mgr.CanBuySlot());
            button.RegisterCallback<ClickEvent>(_ =>
            {
                if (SmokehouseManager.Instance != null) SmokehouseManager.Instance.TryBuySlot();
            });
        }
        else
        {
            costRow.Add(CostLine("cell-cost-icon--research", "Research"));
            // Research-gated slots can't be bought here, so the button becomes a signpost:
            // it hands you off to the Research screen where the expansion is unlocked.
            button.text = "Research";
            button.SetEnabled(ResearchPopupUITK.Instance != null);
            button.RegisterCallback<ClickEvent>(_ =>
            {
                if (ResearchPopupUITK.Instance == null) return;
                Close();
                ResearchPopupUITK.Instance.Open();
            });
        }

        cell.Add(lockIcon);
        cell.Add(costRow);
        cell.Add(button);
        return cell;
    }

    /// <summary>
    /// Floats a "+1" and the collected item up out of the cell it came from. Anchored by the
    /// cell's world rect because the cell itself is gone by the time this runs.
    /// </summary>
    private void PlayCollectFx(Rect fromWorld, int tier)
    {
        if (fxLayer == null) return;
        var layer = fxLayer.worldBound;

        var fx = new VisualElement();
        fx.AddToClassList("collect-fx");
        fx.pickingMode = PickingMode.Ignore;

        var icon = new VisualElement();
        icon.AddToClassList("collect-fx-icon");
        icon.AddToClassList(FishIconClass(tier, true));

        var label = new Label("+1");
        label.AddToClassList("collect-fx-label");

        fx.Add(icon);
        fx.Add(label);

        fx.style.left = fromWorld.center.x - layer.x - 70f; // 70 = half the fx width
        fx.style.top  = fromWorld.center.y - layer.y - 28f;
        fxLayer.Add(fx);

        // One frame at the start state so the transition has something to animate from.
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

    // Frame classes are `<variant>-<index>`; swap by removing the previous index and adding the new.
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
