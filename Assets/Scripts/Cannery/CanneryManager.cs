using System;
using UnityEngine;

/// <summary>
/// Owns the Cannery: jar slots, the shared firebox, intake routing, slot purchases, and
/// jar sales. All decision/simulation math lives in ProcessingMath (EconomyCore); this
/// class is the Unity-side state holder + transaction layer, mirroring WoodcuttingManager.
/// Simulation is UtcNow-anchored so offline time catches up on load (spec §2, §11).
/// </summary>
public class CanneryManager : MonoBehaviour
{
    public static CanneryManager Instance { get; private set; }

    [Header("Firebox Tuning (spec §2)")]
    [Tooltip("Wood/hour burned while the fire is lit even with nothing cooking (waste).")]
    [SerializeField] private float baseBurnPerHour = 5f;
    [Tooltip("Additional wood/hour per cooking jar.")]
    [SerializeField] private float perSlotBurnPerHour = 20f;
    [SerializeField] private int furnaceCapacity = 1200;

    [Header("Jar Tuning (spec §4)")]
    [Tooltip("Jar value multiplier by tier (index 0 = tier 1). 2.5 / 2.65 / 2.8 = patience bonus.")]
    [SerializeField] private float[] tierMultipliers = { 2.5f, 2.65f, 2.8f };

    [Header("Slots (spec §5a: front-loaded curve)")]
    [SerializeField] private int startingSlots = 4;
    [Tooltip("Slots beyond this are research-gated (Phase 3), not purchasable.")]
    [SerializeField] private int maxPurchasableSlots = 20;
    [SerializeField] private int totalMaxSlots = 24;
    [Tooltip("Coin cost of the next slot, indexed by (slotsOwned - startingSlots).")]
    [SerializeField] private int[] slotCoinCosts = { 150, 250, 400, 600, 900, 1400, 2500, 4000, 6500, 10000, 15000, 22000, 32000, 45000, 60000, 80000 };
    [SerializeField] private int[] slotWoodCosts = { 40, 60, 90, 130, 180, 250, 400, 600, 900, 1300, 1800, 2500, 3400, 4500, 6000, 8000 };
    [Tooltip("Purchasable slots added per unlocked expansion research (Phase 3).")]
    [SerializeField] private int slotsPerExpansion = 1;

    [Header("Fuel Efficiency Research (spec §6)")]
    [Tooltip("Max fraction of per-slot burn that efficiency research can remove (keeps demand alive).")]
    [SerializeField] private float maxBurnReduction = 0.40f;

    [Header("UI")]
    [Tooltip("Icon shown on the bottom \"ready\" toast. A missing icon just renders text only.")]
    [SerializeField] private Sprite readyIcon;

    private readonly CanneryState state = new CanneryState();
    private bool intakeOn = true;
    private long lastSimUtcTicks;

    public event Action OnChanged; // durable change: intake, fuel, purchase, sale, jar finished, load

    public bool IsBuilt => BuildingState.IsBuilt(BuildingState.CanneryKey);
    public bool IntakeOn => intakeOn;
    public CanneryState State => state;
    public bool FireLit => IsBuilt && state.fuelWood > 0;
    public int FurnaceCapacity => furnaceCapacity;
    public float BaseBurnPerHour => baseBurnPerHour;
    public float PerSlotBurnPerHour => perSlotBurnPerHour;
    public int SlotsOwned => state.slots.Length;
    public int MaxPurchasableSlots => maxPurchasableSlots;
    public int TotalMaxSlots => totalMaxSlots;

    /// <summary>Coin-purchasable slot cap after research expansions (Phase 3).</summary>
    public int EffectiveMaxPurchasableSlots =>
        ProcessingMath.EffectiveSlotCap(maxPurchasableSlots, totalMaxSlots, ExpansionsUnlocked(), slotsPerExpansion);

    private int ExpansionsUnlocked()
    {
        var rm = ResearchManager.Instance;
        if (rm == null) return 0;
        int n = 0;
        if (rm.IsFeatureUnlocked(Research.FeatureFlag.CanneryExpansion1)) n++;
        if (rm.IsFeatureUnlocked(Research.FeatureFlag.CanneryExpansion2)) n++;
        if (rm.IsFeatureUnlocked(Research.FeatureFlag.CanneryExpansion3)) n++;
        if (rm.IsFeatureUnlocked(Research.FeatureFlag.CanneryExpansion4)) n++;
        return n;
    }

    private float EffectivePerSlotBurn()
    {
        float bonus = ResearchManager.Instance != null
            ? ResearchManager.Instance.GetBonus(Research.StatKey.CanneryBurnEfficiency) : 0f;
        return ProcessingMath.EffectiveBurnPerSlot(perSlotBurnPerHour, bonus, maxBurnReduction);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureSlotArray(startingSlots);
    }

    private void Start()
    {
        if (ResearchManager.Instance != null)
            ResearchManager.Instance.OnFeatureFlagUnlocked += HandleFeatureFlag;
    }

    private void OnDestroy()
    {
        if (ResearchManager.Instance != null)
            ResearchManager.Instance.OnFeatureFlagUnlocked -= HandleFeatureFlag;
    }

    // Refresh the open popup's Buy-Slot row when an expansion research unlocks.
    private void HandleFeatureFlag(string _) => OnChanged?.Invoke();

    private void Update()
    {
        if (!IsBuilt) return;
        long now = DateTime.UtcNow.Ticks;
        if (lastSimUtcTicks == 0) { lastSimUtcTicks = now; return; }
        double elapsed = (now - lastSimUtcTicks) / (double)TimeSpan.TicksPerSecond;
        if (elapsed < 0) { lastSimUtcTicks = now; return; } // forward-only clock
        if (elapsed < 0.25) return;                          // throttle
        lastSimUtcTicks = now;
        int finished = ProcessingMath.Simulate(state, elapsed, baseBurnPerHour, EffectivePerSlotBurn());
        if (finished > 0)
        {
            Debug.Log($"[Cannery] {finished} jar(s) finished.");
            // Bottom plank toast — same class of event as the Smokehouse's "ready" notice.
            ToastManager.ShowCatch(readyIcon, "Preserves ready!");
            OnChanged?.Invoke();
        }
    }

    private void EnsureSlotArray(int count)
    {
        int target = Mathf.Clamp(count, startingSlots, totalMaxSlots);
        if (state.slots.Length >= target) return;
        var next = new CannerySlot[target];
        for (int i = 0; i < target; i++)
            next[i] = i < state.slots.Length && state.slots[i] != null ? state.slots[i] : new CannerySlot();
        state.slots = next;
    }

    public void SetIntakeOn(bool on)
    {
        if (intakeOn == on) return;
        intakeOn = on;
        OnChanged?.Invoke();
    }

    // Barn Processing: each level shortens cook time (spec §4.2 — speed, deliberately not burn rate,
    // to avoid double-dipping the research fuel-efficiency knob). Applied to required cook time at
    // slot-load, not to Update()'s burn simulation, so fuel consumption timing is untouched.
    private static float BarnProcessingSpeedMultiplier()
        => FarmSkillsManager.ProcessingSpeedMultiplier; // per-level bonus + Lv 25 capstone

    private float MultiplierForTier(int tier)
    {
        int idx = Mathf.Clamp(tier, 1, 3) - 1;
        if (tierMultipliers == null || tierMultipliers.Length == 0) return 2.5f;
        return tierMultipliers[Mathf.Min(idx, tierMultipliers.Length - 1)];
    }

    /// <summary>
    /// Mid-run diversion (spec §4a Tier 1): route one harvested unit into a jar. Returns true
    /// if diverted (caller must then SKIP the normal cash+coin payouts). Value basis is the
    /// crop's BASE harvestValue — deterministic, unaffected by in-run multipliers (knob choice).
    /// </summary>
    public bool TryIntake(CropData crop)
    {
        // Only crops explicitly flagged as cannable are diverted; the rest pay out normally.
        if (crop == null || !crop.canBeCanned || !IsBuilt || !intakeOn) return false;
        // Same search as a manual load, so auto-intake can't overwrite a cell that still holds
        // an uncollected jar.
        int idx = FindManualLoadSlot(crop);
        if (idx < 0) return false;

        var s = state.slots[idx];
        if (ProcessingMath.SlotIsEmpty(s))
        {
            int tier = Mathf.Clamp(crop.canneryTier, 1, 3);
            s.cropId = crop.name;
            s.cropName = crop.cropName;
            s.tier = tier;
            s.unitsRequired = ProcessingMath.UnitsRequiredForTier(tier);
            s.unitsLoaded = 0;
            s.cookSecondsRemaining = 0;
            s.jarValue = ProcessingMath.JarValue(crop.harvestValue, tier, MultiplierForTier(tier));
        }
        s.unitsLoaded++;
        if (s.unitsLoaded >= s.unitsRequired)
            s.cookSecondsRemaining = ProcessingMath.CookHoursForTier(s.tier) * 3600.0 / BarnProcessingSpeedMultiplier();
        OnChanged?.Invoke();
        return true;
    }

    // ── Fuel ─────────────────────────────────────────────────────────────

    /// <summary>Move wood from the player's stock into the furnace, clamped to capacity.</summary>
    public bool TryAddFuel(int amount)
    {
        var cm = CurrencyManager.Instance;
        if (!IsBuilt || cm == null || amount <= 0) return false;
        int space = Mathf.Max(0, furnaceCapacity - Mathf.CeilToInt((float)state.fuelWood));
        int toAdd = Mathf.Min(amount, Mathf.Min(space, cm.Wood));
        if (toAdd <= 0) return false;
        if (!cm.SpendWood(toAdd)) return false;
        state.fuelWood += toAdd;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Wood still needed (beyond current fuel) so the current batch finishes.</summary>
    public int StokeToFinishCost()
        => Mathf.CeilToInt((float)ProcessingMath.WoodToFinishLoaded(state, baseBurnPerHour, EffectivePerSlotBurn()));

    public void StokeToFinish() => TryAddFuel(StokeToFinishCost());

    public void FillFurnace() => TryAddFuel(furnaceCapacity - Mathf.CeilToInt((float)state.fuelWood));

    // ── Slot purchase (in-building, spec §5) ─────────────────────────────

    private int NextSlotCostIndex() => SlotsOwned - startingSlots;

    /// <summary>Coin price of the slot at <paramref name="slotIndex"/>, so every locked jar can show
    /// its own price. Clamps to the last table entry past the end.</summary>
    public int SlotCoinCostAt(int slotIndex)
    {
        int i = slotIndex - startingSlots;
        if (i < 0 || slotCoinCosts.Length == 0) return int.MaxValue;
        return slotCoinCosts[Mathf.Min(i, slotCoinCosts.Length - 1)];
    }

    /// <summary>Wood price of the slot at <paramref name="slotIndex"/>. See <see cref="SlotCoinCostAt"/>.</summary>
    public int SlotWoodCostAt(int slotIndex)
    {
        int i = slotIndex - startingSlots;
        if (i < 0 || slotWoodCosts.Length == 0) return int.MaxValue;
        return slotWoodCosts[Mathf.Min(i, slotWoodCosts.Length - 1)];
    }

    public int NextSlotCoinCost()
    {
        int i = NextSlotCostIndex();
        if (i < 0 || slotCoinCosts.Length == 0) return int.MaxValue;
        return slotCoinCosts[Mathf.Min(i, slotCoinCosts.Length - 1)];
    }

    public int NextSlotWoodCost()
    {
        int i = NextSlotCostIndex();
        if (i < 0 || slotWoodCosts.Length == 0) return int.MaxValue;
        return slotWoodCosts[Mathf.Min(i, slotWoodCosts.Length - 1)];
    }

    public bool CanBuySlot()
    {
        var cm = CurrencyManager.Instance;
        if (cm == null || !IsBuilt) return false;
        return ProcessingMath.CanBuySlot(SlotsOwned, EffectiveMaxPurchasableSlots,
            cm.Coins, NextSlotCoinCost(), cm.Wood, NextSlotWoodCost());
    }

    public bool TryBuySlot()
    {
        if (!CanBuySlot()) return false;
        var cm = CurrencyManager.Instance;
        int coinCost = NextSlotCoinCost();
        int woodCost = NextSlotWoodCost();
        if (!cm.SpendCoins(coinCost)) return false;
        if (!cm.SpendWood(woodCost)) { cm.AddCoins(coinCost); return false; } // refund like axe upgrade
        EnsureSlotArray(SlotsOwned + 1);
        Debug.Log($"[Cannery] Slot purchased → {SlotsOwned} slots.");
        OnChanged?.Invoke();
        return true;
    }

    // ── Selling (Gold only, spec §1) ─────────────────────────────────────

    // A finished jar is first PARKED in the cell it cooked in (slotIndex >= 0) and only becomes
    // sellable stock once collected (slotIndex < 0). Selling — which lives in the Inventory now —
    // therefore only ever sees collected jars, and can't sell one out from under the rack.

    private bool IsCollected(ReadyJar j) => j != null && j.slotIndex < 0;

    /// <summary>Collected jars on the shelf — the sellable stock the Inventory reports.</summary>
    public int ReadyJarCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < state.readyJars.Count; i++) if (IsCollected(state.readyJars[i])) n++;
            return n;
        }
    }

    /// <summary>Maps a collected-jar index onto the backing list, or -1.</summary>
    private int CollectedToBacking(int collectedIndex)
    {
        int n = 0;
        for (int i = 0; i < state.readyJars.Count; i++)
        {
            if (!IsCollected(state.readyJars[i])) continue;
            if (n == collectedIndex) return i;
            n++;
        }
        return -1;
    }

    /// <summary>Coin value of the collected jar at <paramref name="index"/>, or 0 if out of range.
    /// Lets the Inventory preview a multi-jar sale without exposing the mutable jar list.</summary>
    public int JarValueAt(int index)
    {
        int b = CollectedToBacking(index);
        return b < 0 ? 0 : state.readyJars[b].value;
    }

    public bool TrySellJar(int index)
    {
        var cm = CurrencyManager.Instance;
        if (cm == null) return false;
        int b = CollectedToBacking(index);
        if (b < 0) return false;
        int value = state.readyJars[b].value;
        state.readyJars.RemoveAt(b);
        cm.AddCoins(value);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Sells every COLLECTED jar; returns total Coins gained. Parked jars are untouched.</summary>
    public int SellAllJars()
    {
        var cm = CurrencyManager.Instance;
        if (cm == null) return 0;
        int total = 0;
        for (int i = state.readyJars.Count - 1; i >= 0; i--)
        {
            if (!IsCollected(state.readyJars[i])) continue;
            total += state.readyJars[i].value;
            state.readyJars.RemoveAt(i);
        }
        if (total <= 0) return 0;
        cm.AddCoins(total);
        Debug.Log($"[Cannery] Sold all jars for {total} coins.");
        OnChanged?.Invoke();
        return total;
    }

    // ── Rack (jars parked in their cell until collected) ──────────────────

    /// <summary>
    /// Ready-shelf index of the finished jar still parked in <paramref name="slotIndex"/>, or -1.
    /// A finished jar keeps the cell it cooked in so the rack never re-flows.
    /// </summary>
    public int ReadyIndexForSlot(int slotIndex)
    {
        if (slotIndex < 0) return -1;
        for (int i = 0; i < state.readyJars.Count; i++)
            if (state.readyJars[i] != null && state.readyJars[i].slotIndex == slotIndex) return i;
        return -1;
    }

    public ReadyJar ReadyAt(int index)
        => index >= 0 && index < state.readyJars.Count ? state.readyJars[index] : null;

    /// <summary>Takes a finished jar off the rack onto the shelf, freeing its cell.</summary>
    public bool TryCollectJar(int readyIndex)
    {
        var j = ReadyAt(readyIndex);
        if (j == null || j.slotIndex < 0) return false;
        j.slotIndex = -1;
        RollDoubleJar(j);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Processing skill milestones (Lv 5-20): a collected jar may come with a twin,
    /// added straight to the shelf (slotIndex -1) so it never occupies a cell.</summary>
    private void RollDoubleJar(ReadyJar j)
    {
        if (!FarmSkillsManager.RollMilestone(FarmSkillTrack.Processing)) return;
        state.readyJars.Add(new ReadyJar
        {
            cropName = j.cropName, value = j.value, tier = j.tier, sourceId = j.sourceId, slotIndex = -1,
        });
    }

    /// <summary>Collects every parked jar. Returns how many moved to the shelf.</summary>
    public int CollectAllJars()
    {
        int n = 0;
        // Snapshot the count: RollDoubleJar appends twins, which are already collected anyway.
        int count = state.readyJars.Count;
        for (int i = 0; i < count; i++)
            if (state.readyJars[i] != null && state.readyJars[i].slotIndex >= 0)
            { state.readyJars[i].slotIndex = -1; RollDoubleJar(state.readyJars[i]); n++; }
        if (n > 0) OnChanged?.Invoke();
        return n;
    }

    // ── Manual bulk loading (spec §4a: dump a stack of crops straight into a jar) ──

    /// <summary>
    /// Cell a manual load of <paramref name="crop"/> would go into: a part-filled jar of the same
    /// crop first, then the first cell that is neither cooking nor holding an uncollected jar.
    /// </summary>
    public int FindManualLoadSlot(CropData crop)
    {
        if (crop == null) return -1;
        for (int i = 0; i < state.slots.Length; i++)
        {
            var s = state.slots[i];
            if (!ProcessingMath.SlotIsEmpty(s) && s.cropId == crop.name
                && s.unitsLoaded < s.unitsRequired) return i;
        }
        for (int i = 0; i < state.slots.Length; i++)
            if (ProcessingMath.SlotIsEmpty(state.slots[i]) && ReadyIndexForSlot(i) < 0) return i;
        return -1;
    }

    /// <summary>How many crop units one manual load would consume (tops up a partial jar).</summary>
    public int UnitsNeededFor(CropData crop)
    {
        if (crop == null || !crop.canBeCanned) return 0;
        int idx = FindManualLoadSlot(crop);
        if (idx < 0) return ProcessingMath.UnitsRequiredForTier(Mathf.Clamp(crop.canneryTier, 1, 3));
        var s = state.slots[idx];
        if (ProcessingMath.SlotIsEmpty(s))
            return ProcessingMath.UnitsRequiredForTier(Mathf.Clamp(crop.canneryTier, 1, 3));
        return Mathf.Max(0, s.unitsRequired - s.unitsLoaded);
    }

    /// <summary>True if the player has the crops and a cell to put them in.</summary>
    public bool CanBulkLoad(CropData crop)
    {
        if (crop == null || !crop.canBeCanned || !IsBuilt) return false;
        var inv = ItemInventoryManager.Instance;
        if (inv == null) return false;
        if (FindManualLoadSlot(crop) < 0) return false;
        int need = UnitsNeededFor(crop);
        return need > 0 && inv.GetCrop(crop.cropName) >= need;
    }

    /// <summary>
    /// Spends a jar's worth of crops from the player's stock and drops them in, starting the cook
    /// if that fills the jar. Deliberately all-or-nothing so a tap never half-spends a stack.
    /// </summary>
    public bool TryBulkLoad(CropData crop)
    {
        if (!CanBulkLoad(crop)) return false;
        var inv = ItemInventoryManager.Instance;
        int idx = FindManualLoadSlot(crop);
        int need = UnitsNeededFor(crop);
        if (!inv.TrySpendCrop(crop.cropName, need)) return false;

        var s = state.slots[idx];
        if (ProcessingMath.SlotIsEmpty(s))
        {
            int tier = Mathf.Clamp(crop.canneryTier, 1, 3);
            s.cropId = crop.name;
            s.cropName = crop.cropName;
            s.tier = tier;
            s.unitsRequired = ProcessingMath.UnitsRequiredForTier(tier);
            s.unitsLoaded = 0;
            s.cookSecondsRemaining = 0;
            s.jarValue = ProcessingMath.JarValue(crop.harvestValue, tier, MultiplierForTier(tier));
        }
        s.unitsLoaded = Mathf.Min(s.unitsRequired, s.unitsLoaded + need);
        if (s.unitsLoaded >= s.unitsRequired && s.cookSecondsRemaining <= 0)
            s.cookSecondsRemaining = ProcessingMath.CookHoursForTier(s.tier) * 3600.0 / BarnProcessingSpeedMultiplier();
        Debug.Log($"[Cannery] Bulk-loaded {need} {crop.cropName} into slot {idx} ({s.unitsLoaded}/{s.unitsRequired}).");
        OnChanged?.Invoke();
        return true;
    }

    // ── Save / load (SaveManager post-construction assignment pattern) ───

    public void CaptureTo(GameData d)
    {
        d.canneryIntakeOn = intakeOn;
        d.canneryFuelWood = state.fuelWood;
        d.cannerySlots = state.slots;
        d.canneryReadyJars = state.readyJars.ToArray();
        d.canneryLastSimUtcTicks = lastSimUtcTicks != 0 ? lastSimUtcTicks : DateTime.UtcNow.Ticks;
    }

    public void LoadFrom(GameData d)
    {
        intakeOn = d.canneryIntakeOn;
        state.fuelWood = Math.Max(0, d.canneryFuelWood);
        state.slots = d.cannerySlots != null && d.cannerySlots.Length > 0 ? d.cannerySlots : new CannerySlot[0];
        for (int i = 0; i < state.slots.Length; i++)
            if (state.slots[i] == null) state.slots[i] = new CannerySlot();
        EnsureSlotArray(startingSlots);
        state.readyJars.Clear();
        if (d.canneryReadyJars != null)
            foreach (var j in d.canneryReadyJars)
                if (j != null) state.readyJars.Add(j);

        // Jars saved before slotIndex existed can deserialize with a bogus cell claim. Anything
        // out of range, or a second jar claiming a cell, is treated as already collected.
        var claimed = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < state.readyJars.Count; i++)
        {
            var j = state.readyJars[i];
            if (j.slotIndex < 0) continue;
            if (j.slotIndex >= state.slots.Length || !claimed.Add(j.slotIndex)) j.slotIndex = -1;
        }

        // Offline catch-up: burn/cook through the away time, then re-anchor.
        long now = DateTime.UtcNow.Ticks;
        if (IsBuilt && d.canneryLastSimUtcTicks > 0 && now > d.canneryLastSimUtcTicks)
        {
            double elapsed = (now - d.canneryLastSimUtcTicks) / (double)TimeSpan.TicksPerSecond;
            int finished = ProcessingMath.Simulate(state, elapsed, baseBurnPerHour, EffectivePerSlotBurn());
            if (finished > 0)
                ToastManager.ShowCatch(readyIcon, $"{finished} jar(s) finished while you were away!");
        }
        lastSimUtcTicks = now;
        OnChanged?.Invoke();
    }
}
