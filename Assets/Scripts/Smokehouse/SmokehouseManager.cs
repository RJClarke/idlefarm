using System;
using UnityEngine;

// The Smokehouse reuses the Phase 1 firebox core as the generic processing model. These aliases keep
// the code readable without renaming the shipped Cannery* types (whose names appear in save fields).
using ProcessingState = CanneryState;
using ProcessingSlot = CannerySlot;

/// <summary>
/// Owns the Smokehouse: fish slots over the shared firebox, fuel, slot purchases, and raw/smoked
/// fish sales (spec §3). Fish come from the Pantry; smoked output returns to the Pantry as counts.
/// All firebox math is ProcessingMath (shared with the Cannery); this is the Unity-side state +
/// transaction layer, mirroring CanneryManager. UtcNow-anchored → offline catch-up for free.
/// </summary>
public class SmokehouseManager : MonoBehaviour
{
    public static SmokehouseManager Instance { get; private set; }

    [Header("Firebox Tuning (spec §2)")]
    [Tooltip("Wood burned per hour with nothing cooking — idling the fire is meant to hurt.")]
    [SerializeField] private float baseBurnPerHour = 15f;
    [SerializeField] private float perSlotBurnPerHour = 20f;
    [SerializeField] private int furnaceCapacity = 1600;

    [Header("Fish Tables (index 0 = tier 1 Perch). Spec §3.")]
    [Tooltip("Raw fish sell value (Gold).")]
    [SerializeField] private int[] rawValue = { 100, 400, 2000 };
    [Tooltip("Smoked fish sell value (Gold).")]
    [SerializeField] private int[] smokedValue = { 300, 1400, 5000 };
    [Tooltip("Hours to smoke one fish of this tier.")]
    [SerializeField] private int[] smokeHours = { 4, 8, 12 };

    [Header("Slots (spec §5a: front-loaded; last 2 research-gated)")]
    [SerializeField] private int startingSlots = 1;
    [Tooltip("Slots beyond this are research-gated (Phase 3), not purchasable.")]
    [SerializeField] private int maxPurchasableSlots = 6;
    [SerializeField] private int totalMaxSlots = 8;
    [SerializeField] private int[] slotCoinCosts = { 600, 1500, 4000, 9000, 18000 };
    [SerializeField] private int[] slotWoodCosts = { 120, 300, 700, 1400, 2600 };
    [Tooltip("Purchasable slots added per unlocked expansion research (Phase 3).")]
    [SerializeField] private int slotsPerExpansion = 1;

    [Header("Fuel Efficiency Research (spec §6)")]
    [Tooltip("Max fraction of per-slot burn that efficiency research can remove (keeps demand alive).")]
    [SerializeField] private float maxBurnReduction = 0.40f;

    [Header("UI")]
    [Tooltip("Icon shown on the bottom \"ready\" toast. A missing icon just renders text only.")]
    [SerializeField] private Sprite readyIcon;

    private readonly ProcessingState state = new ProcessingState();
    private long lastSimUtcTicks;

    public event Action OnChanged;

    public bool IsBuilt => BuildingState.IsBuilt(BuildingState.SmokehouseKey);
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
        if (rm.IsFeatureUnlocked(Research.FeatureFlag.SmokehouseExpansion1)) n++;
        if (rm.IsFeatureUnlocked(Research.FeatureFlag.SmokehouseExpansion2)) n++;
        return n;
    }

    private float EffectivePerSlotBurn()
    {
        float bonus = ResearchManager.Instance != null
            ? ResearchManager.Instance.GetBonus(Research.StatKey.SmokehouseBurnEfficiency) : 0f;
        return ProcessingMath.EffectiveBurnPerSlot(perSlotBurnPerHour, bonus, maxBurnReduction);
    }

    public int RawValue(int tier) => rawValue[TierIdx(tier)];
    public int SmokedValue(int tier) => smokedValue[TierIdx(tier)];
    public int SmokeHours(int tier) => smokeHours[TierIdx(tier)];

    // Barn Processing: each level shortens smoke time (spec §4.2 — speed, not burn rate). Applied
    // to required cook time at slot-load, not to Update()'s burn simulation.
    private static float BarnProcessingSpeedMultiplier()
        => FarmSkillsManager.ProcessingSpeedMultiplier; // per-level bonus + Lv 25 capstone

    private static int TierIdx(int tier) => Mathf.Clamp(tier, 1, FishTiers.Count) - 1;

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
        if (elapsed < 0) { lastSimUtcTicks = now; return; }
        if (elapsed < 0.25) return;
        lastSimUtcTicks = now;
        int finished = ProcessingMath.Simulate(state, elapsed, baseBurnPerHour, EffectivePerSlotBurn());
        if (finished > 0)
        {
            Debug.Log($"[Smokehouse] {finished} fish finished smoking.");
            // Bottom plank toast, matching fish catches — gathering/processing events read as
            // world events down there, while the top parchment is for progression notices.
            ToastManager.ShowCatch(readyIcon, "Smoked fish ready!");
            OnChanged?.Invoke();
        }
    }

    private void EnsureSlotArray(int count)
    {
        int target = Mathf.Clamp(count, startingSlots, totalMaxSlots);
        if (state.slots.Length >= target) return;
        var next = new ProcessingSlot[target];
        for (int i = 0; i < target; i++)
            next[i] = i < state.slots.Length && state.slots[i] != null ? state.slots[i] : new ProcessingSlot();
        state.slots = next;
    }

    /// <summary>Move every finished good out of the ready shelf into Pantry smoked counts.</summary>
    private int DrainFinishedToPantry()
    {
        if (state.readyJars.Count == 0) return 0;
        int n = state.readyJars.Count;
        if (PantryManager.Instance != null)
            for (int i = 0; i < n; i++)
                BankSmoked(state.readyJars[i].tier);
        state.readyJars.Clear();
        return n;
    }

    /// <summary>Bank one finished fish into the Pantry — two on a Processing-skill double batch.</summary>
    private static void BankSmoked(int tier)
    {
        PantryManager.Instance.AddSmoked(tier);
        if (FarmSkillsManager.RollMilestone(FarmSkillTrack.Processing))
            PantryManager.Instance.AddSmoked(tier);
    }

    // ── Ready shelf (collected by tapping a finished cell in the smoker grid) ──

    /// <summary>Finished fish waiting to be collected out of the smoker.</summary>
    public int ReadyCount => state.readyJars.Count;

    /// <summary>The finished fish at <paramref name="index"/>, or null if out of range.</summary>
    public ReadyJar ReadyAt(int index)
        => index >= 0 && index < state.readyJars.Count ? state.readyJars[index] : null;

    /// <summary>
    /// Ready-shelf index of the finished fish still parked in <paramref name="slotIndex"/>, or -1.
    /// A finished fish keeps the cell it cooked in until collected — the rack never re-flows.
    /// </summary>
    public int ReadyIndexForSlot(int slotIndex)
    {
        for (int i = 0; i < state.readyJars.Count; i++)
            if (state.readyJars[i] != null && state.readyJars[i].slotIndex == slotIndex) return i;
        return -1;
    }

    /// <summary>Bank one finished fish into the Pantry, freeing its cell.</summary>
    public bool TryCollect(int index)
    {
        if (index < 0 || index >= state.readyJars.Count) return false;
        if (PantryManager.Instance == null) return false;
        BankSmoked(state.readyJars[index].tier);
        state.readyJars.RemoveAt(index);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Bank every finished fish at once. Returns how many were collected.</summary>
    public int CollectAll()
    {
        int n = DrainFinishedToPantry();
        if (n > 0) OnChanged?.Invoke();
        return n;
    }

    // ── Load a fish into the smoker (spec §3) ────────────────────────────

    /// <summary>Pull one raw fish of a tier from the Pantry into the first empty slot, cooking now.</summary>
    public bool TryLoadFish(int tier)
    {
        if (!IsBuilt || PantryManager.Instance == null) return false;
        if (PantryManager.Instance.GetRaw(tier) <= 0) return false;

        // Skip cells still holding an uncollected fish — they look occupied to the player and
        // must not be silently overwritten.
        int idx = -1;
        for (int i = 0; i < state.slots.Length; i++)
            if (ProcessingMath.SlotIsEmpty(state.slots[i]) && ReadyIndexForSlot(i) < 0) { idx = i; break; }
        if (idx < 0) return false;

        if (!PantryManager.Instance.SpendRaw(tier)) return false;

        var s = state.slots[idx];
        int t = Mathf.Clamp(tier, 1, FishTiers.Count);
        s.cropId = "fish_" + t;
        s.cropName = FishTiers.SmokedName(t);
        s.tier = t;
        s.unitsRequired = 1;
        s.unitsLoaded = 1;                                   // one fish fills a slot immediately
        s.jarValue = SmokedValue(t);
        s.cookSecondsRemaining = SmokeHours(t) * 3600.0 / BarnProcessingSpeedMultiplier(); // cooking starts now
        OnChanged?.Invoke();
        return true;
    }

    // ── Fuel (identical to CanneryManager) ───────────────────────────────

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

    public int StokeToFinishCost()
        => Mathf.CeilToInt((float)ProcessingMath.WoodToFinishLoaded(state, baseBurnPerHour, EffectivePerSlotBurn()));

    public void StokeToFinish() => TryAddFuel(StokeToFinishCost());

    public void FillFurnace() => TryAddFuel(furnaceCapacity - Mathf.CeilToInt((float)state.fuelWood));

    // ── Slot purchase (in-building, spec §5) ─────────────────────────────

    private int NextSlotCostIndex() => SlotsOwned - startingSlots;

    /// <summary>Coin price of the slot at <paramref name="slotIndex"/>, so locked cells can each show
    /// their own price instead of only the next one. Clamps to the last entry past the table.</summary>
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
        if (!cm.SpendWood(woodCost)) { cm.AddCoins(coinCost); return false; }
        EnsureSlotArray(SlotsOwned + 1);
        Debug.Log($"[Smokehouse] Slot purchased → {SlotsOwned} slots.");
        OnChanged?.Invoke();
        return true;
    }

    // ── Selling (Gold only, spec §1) ─────────────────────────────────────

    public bool TrySellRaw(int tier)
    {
        var cm = CurrencyManager.Instance;
        if (cm == null || PantryManager.Instance == null) return false;
        if (!PantryManager.Instance.SpendRaw(tier)) return false;
        cm.AddCoins(RawValue(tier));
        OnChanged?.Invoke();
        return true;
    }

    public bool TrySellSmoked(int tier)
    {
        var cm = CurrencyManager.Instance;
        if (cm == null || PantryManager.Instance == null) return false;
        if (!PantryManager.Instance.SpendSmoked(tier)) return false;
        cm.AddCoins(SmokedValue(tier));
        OnChanged?.Invoke();
        return true;
    }

    // ── Save / load ──────────────────────────────────────────────────────

    public void CaptureTo(GameData d)
    {
        d.smokehouseFuelWood = state.fuelWood;
        d.smokehouseSlots = state.slots;
        d.smokehouseReadyJars = state.readyJars.ToArray(); // uncollected fish keep their cell
        d.smokehouseLastSimUtcTicks = lastSimUtcTicks != 0 ? lastSimUtcTicks : DateTime.UtcNow.Ticks;
    }

    public void LoadFrom(GameData d)
    {
        state.fuelWood = Math.Max(0, d.smokehouseFuelWood);
        state.slots = d.smokehouseSlots != null && d.smokehouseSlots.Length > 0 ? d.smokehouseSlots : new ProcessingSlot[0];
        for (int i = 0; i < state.slots.Length; i++)
            if (state.slots[i] == null) state.slots[i] = new ProcessingSlot();
        EnsureSlotArray(startingSlots);

        // Pre-shelf saves have no array; those runs already banked their fish into the Pantry.
        state.readyJars.Clear();
        if (d.smokehouseReadyJars != null)
            for (int i = 0; i < d.smokehouseReadyJars.Length; i++)
                if (d.smokehouseReadyJars[i] != null) state.readyJars.Add(d.smokehouseReadyJars[i]);

        long now = DateTime.UtcNow.Ticks;
        if (IsBuilt && d.smokehouseLastSimUtcTicks > 0 && now > d.smokehouseLastSimUtcTicks)
        {
            double elapsed = (now - d.smokehouseLastSimUtcTicks) / (double)TimeSpan.TicksPerSecond;
            int finished = ProcessingMath.Simulate(state, elapsed, baseBurnPerHour, EffectivePerSlotBurn());
            if (finished > 0)
                ToastManager.ShowCatch(readyIcon, $"{finished} fish finished smoking while you were away!");
        }
        lastSimUtcTicks = now;
        OnChanged?.Invoke();
    }
}
