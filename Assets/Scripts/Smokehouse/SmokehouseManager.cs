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
    [SerializeField] private float baseBurnPerHour = 5f;
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

    public int RawValue(int tier) => rawValue[TierIdx(tier)];
    public int SmokedValue(int tier) => smokedValue[TierIdx(tier)];
    public int SmokeHours(int tier) => smokeHours[TierIdx(tier)];

    private static int TierIdx(int tier) => Mathf.Clamp(tier, 1, FishTiers.Count) - 1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureSlotArray(startingSlots);
    }

    private void Update()
    {
        if (!IsBuilt) return;
        long now = DateTime.UtcNow.Ticks;
        if (lastSimUtcTicks == 0) { lastSimUtcTicks = now; return; }
        double elapsed = (now - lastSimUtcTicks) / (double)TimeSpan.TicksPerSecond;
        if (elapsed < 0) { lastSimUtcTicks = now; return; }
        if (elapsed < 0.25) return;
        lastSimUtcTicks = now;
        ProcessingMath.Simulate(state, elapsed, baseBurnPerHour, perSlotBurnPerHour);
        int drained = DrainFinishedToPantry();
        if (drained > 0)
        {
            Debug.Log($"[Smokehouse] {drained} fish finished smoking.");
            ToastManager.Show("Smoked fish ready!", "Visit the Smokehouse to sell.");
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

    /// <summary>Move every finished good out of the scratch shelf into Pantry smoked counts.</summary>
    private int DrainFinishedToPantry()
    {
        if (state.readyJars.Count == 0) return 0;
        int n = state.readyJars.Count;
        if (PantryManager.Instance != null)
            for (int i = 0; i < n; i++)
                PantryManager.Instance.AddSmoked(state.readyJars[i].tier);
        state.readyJars.Clear();
        return n;
    }

    // ── Load a fish into the smoker (spec §3) ────────────────────────────

    /// <summary>Pull one raw fish of a tier from the Pantry into the first empty slot, cooking now.</summary>
    public bool TryLoadFish(int tier)
    {
        if (!IsBuilt || PantryManager.Instance == null) return false;
        if (PantryManager.Instance.GetRaw(tier) <= 0) return false;

        int idx = -1;
        for (int i = 0; i < state.slots.Length; i++)
            if (ProcessingMath.SlotIsEmpty(state.slots[i])) { idx = i; break; }
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
        s.cookSecondsRemaining = SmokeHours(t) * 3600.0;     // cooking starts now
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
        => Mathf.CeilToInt((float)ProcessingMath.WoodToFinishLoaded(state, baseBurnPerHour, perSlotBurnPerHour));

    public void StokeToFinish() => TryAddFuel(StokeToFinishCost());

    public void FillFurnace() => TryAddFuel(furnaceCapacity - Mathf.CeilToInt((float)state.fuelWood));

    // ── Slot purchase (in-building, spec §5) ─────────────────────────────

    private int NextSlotCostIndex() => SlotsOwned - startingSlots;

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
        return ProcessingMath.CanBuySlot(SlotsOwned, maxPurchasableSlots,
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
        DrainFinishedToPantry(); // never persist the transient shelf
        d.smokehouseFuelWood = state.fuelWood;
        d.smokehouseSlots = state.slots;
        d.smokehouseLastSimUtcTicks = lastSimUtcTicks != 0 ? lastSimUtcTicks : DateTime.UtcNow.Ticks;
    }

    public void LoadFrom(GameData d)
    {
        state.fuelWood = Math.Max(0, d.smokehouseFuelWood);
        state.slots = d.smokehouseSlots != null && d.smokehouseSlots.Length > 0 ? d.smokehouseSlots : new ProcessingSlot[0];
        for (int i = 0; i < state.slots.Length; i++)
            if (state.slots[i] == null) state.slots[i] = new ProcessingSlot();
        EnsureSlotArray(startingSlots);
        state.readyJars.Clear();

        long now = DateTime.UtcNow.Ticks;
        if (IsBuilt && d.smokehouseLastSimUtcTicks > 0 && now > d.smokehouseLastSimUtcTicks)
        {
            double elapsed = (now - d.smokehouseLastSimUtcTicks) / (double)TimeSpan.TicksPerSecond;
            ProcessingMath.Simulate(state, elapsed, baseBurnPerHour, perSlotBurnPerHour);
            int drained = DrainFinishedToPantry();
            if (drained > 0)
                ToastManager.Show($"{drained} fish finished smoking while you were away!", "Visit the Smokehouse to sell.");
        }
        lastSimUtcTicks = now;
        OnChanged?.Invoke();
    }
}
