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
        if (elapsed < 0) { lastSimUtcTicks = now; return; } // forward-only clock
        if (elapsed < 0.25) return;                          // throttle
        lastSimUtcTicks = now;
        int finished = ProcessingMath.Simulate(state, elapsed, baseBurnPerHour, perSlotBurnPerHour);
        if (finished > 0)
        {
            Debug.Log($"[Cannery] {finished} jar(s) finished.");
            ToastManager.Show("Preserves ready!", "Visit the Cannery to sell.");
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
        if (crop == null || !IsBuilt || !intakeOn) return false;
        int idx = ProcessingMath.FindIntakeSlot(state, crop.name);
        if (idx < 0) return false;

        var s = state.slots[idx];
        if (ProcessingMath.SlotIsEmpty(s))
        {
            int tier = Mathf.Clamp(crop.tier, 1, 3);
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
            s.cookSecondsRemaining = ProcessingMath.CookHoursForTier(s.tier) * 3600.0;
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
        if (!cm.SpendWood(woodCost)) { cm.AddCoins(coinCost); return false; } // refund like axe upgrade
        EnsureSlotArray(SlotsOwned + 1);
        Debug.Log($"[Cannery] Slot purchased → {SlotsOwned} slots.");
        OnChanged?.Invoke();
        return true;
    }

    // ── Selling (Gold only, spec §1) ─────────────────────────────────────

    public bool TrySellJar(int index)
    {
        var cm = CurrencyManager.Instance;
        if (cm == null || index < 0 || index >= state.readyJars.Count) return false;
        int value = state.readyJars[index].value;
        state.readyJars.RemoveAt(index);
        cm.AddCoins(value);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Sells every ready jar; returns total Coins gained.</summary>
    public int SellAllJars()
    {
        var cm = CurrencyManager.Instance;
        if (cm == null || state.readyJars.Count == 0) return 0;
        int total = 0;
        for (int i = 0; i < state.readyJars.Count; i++) total += state.readyJars[i].value;
        state.readyJars.Clear();
        cm.AddCoins(total);
        Debug.Log($"[Cannery] Sold all jars for {total} coins.");
        OnChanged?.Invoke();
        return total;
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

        // Offline catch-up: burn/cook through the away time, then re-anchor.
        long now = DateTime.UtcNow.Ticks;
        if (IsBuilt && d.canneryLastSimUtcTicks > 0 && now > d.canneryLastSimUtcTicks)
        {
            double elapsed = (now - d.canneryLastSimUtcTicks) / (double)TimeSpan.TicksPerSecond;
            int finished = ProcessingMath.Simulate(state, elapsed, baseBurnPerHour, perSlotBurnPerHour);
            if (finished > 0)
                ToastManager.Show($"{finished} jar(s) finished while you were away!", "Visit the Cannery to sell.");
        }
        lastSimUtcTicks = now;
        OnChanged?.Invoke();
    }
}
