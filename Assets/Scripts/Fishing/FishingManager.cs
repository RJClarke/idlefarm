using System;
using UnityEngine;

/// <summary>
/// Owns fishing meta-state: the pole, its tuning, and the single in-flight line's cast → wait →
/// bite cycle (spec §3a). Mirrors WoodcuttingManager. Bite timing is UtcNow-anchored so a line cast
/// before closing lands its bite offline. One line at a time; the fish waits forever once it bites.
/// Caught fish go to the PantryManager. All rarity/timing rolls run through FishingMath.
/// </summary>
public class FishingManager : MonoBehaviour
{
    public static FishingManager Instance { get; private set; }

    public enum CastState { Idle, Waiting, Bite }

    [Serializable]
    public class PoleTier
    {
        [Tooltip("Relative catch weights by fish tier: index 0 = Perch, 1 = Bass, 2 = Pike.")]
        public float[] weights = { 98f, 1.9f, 0.1f };
        [Tooltip("Average seconds to a bite at this pole level (spec §3a: ~20 min = 1200s at base).")]
        public float biteAvgSeconds = 1200f;
    }

    [Header("Pole Tiers (index = pole level; 0 = first pole)")]
    [Tooltip("Each level shifts rarity odds and bite rate. Rare fish stay possible at level 0.")]
    [SerializeField] private PoleTier[] poleTiers =
    {
        new PoleTier { weights = new[] { 98f,  1.9f, 0.1f }, biteAvgSeconds = 1200f },
        new PoleTier { weights = new[] { 95f,  4.5f, 0.5f }, biteAvgSeconds = 1050f },
        new PoleTier { weights = new[] { 90f,  8.5f, 1.5f }, biteAvgSeconds = 900f  },
        new PoleTier { weights = new[] { 82f, 15f,   3f   }, biteAvgSeconds = 780f  },
    };

    [Header("Pole Purchase / Upgrade (spec §3a)")]
    [Tooltip("Coins-only cost of the first pole (mirrors the first axe).")]
    [SerializeField] private int firstPoleCoinCost = 75;
    [Tooltip("Coin cost of the next pole level, indexed by current level (0 -> level 1, ...).")]
    [SerializeField] private int[] poleCoinCosts = { 300, 900, 2500 };
    [Tooltip("Wood cost of the next pole level, indexed by current level.")]
    [SerializeField] private int[] poleWoodCosts = { 50, 140, 320 };

    [Header("Hints")]
    [SerializeField] private string noPoleHintText = "You need to buy a fishing pole first.";

    private int poleLevel;
    private bool hasPole;
    private CastState state = CastState.Idle;
    private long castUtcTicks;
    private long biteReadyUtcTicks;
    private int pendingTier;
    private WorldHintPopup activeHint;

    public event Action OnChanged;             // durable: state/pole change, load
    public event Action<int> OnPoleLevelChanged; // Carpenter UI refresh (mirrors OnAxeLevelChanged)

    public bool HasPole => hasPole;
    public int PoleLevel => poleLevel;
    public int MaxPoleLevel => Mathf.Max(0, poleTiers.Length - 1);
    public int FirstPoleCoinCost => firstPoleCoinCost;
    public CastState State => state;
    public bool HasBite => state == CastState.Bite;
    public int PendingTier => pendingTier;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (state != CastState.Waiting) return;
        long now = DateTime.UtcNow.Ticks;
        if (biteReadyUtcTicks > 0 && now >= biteReadyUtcTicks)
            TransitionToBite();
    }

    private PoleTier CurrentTier()
    {
        if (poleTiers == null || poleTiers.Length == 0) return new PoleTier();
        return poleTiers[Mathf.Clamp(poleLevel, 0, poleTiers.Length - 1)];
    }

    private void TransitionToBite()
    {
        pendingTier = FishingMath.RollFishTier(CurrentTier().weights, UnityEngine.Random.value);
        state = CastState.Bite;
        Debug.Log($"[Fishing] Bite: {FishTiers.Name(pendingTier)} on the line.");
        OnChanged?.Invoke();
    }

    // ── Cast / collect (spec §3a) ────────────────────────────────────────

    /// <summary>Cast the line if idle and a pole is owned. Rolls the bite time now (UtcNow-anchored).</summary>
    public bool Cast()
    {
        if (!hasPole || state != CastState.Idle) return false;
        long now = DateTime.UtcNow.Ticks;
        double biteSecs = FishingMath.RollBiteSeconds(CurrentTier().biteAvgSeconds, UnityEngine.Random.value);
        castUtcTicks = now;
        biteReadyUtcTicks = now + (long)(biteSecs * TimeSpan.TicksPerSecond);
        state = CastState.Waiting;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Collect a fish on the line into the Pantry. Returns the caught tier (0 if none).</summary>
    public int Collect()
    {
        if (state != CastState.Bite) return 0;
        int tier = pendingTier;
        if (PantryManager.Instance != null) PantryManager.Instance.AddRaw(tier);
        pendingTier = 0;
        castUtcTicks = 0;
        biteReadyUtcTicks = 0;
        state = CastState.Idle;
        Debug.Log($"[Fishing] Collected {FishTiers.Name(tier)}.");
        OnChanged?.Invoke();
        return tier;
    }

    public void ShowNoPoleHint(Vector3 worldPos)
    {
        if (activeHint != null) Destroy(activeHint.gameObject);
        activeHint = WorldHintPopup.Create(worldPos, noPoleHintText);
    }

    // ── First pole (Coins only) ──────────────────────────────────────────

    public bool CanBuyPole()
    {
        var cm = CurrencyManager.Instance;
        return cm != null && FishingMath.CanBuyPole(hasPole, cm.Coins, firstPoleCoinCost);
    }

    public bool TryBuyPole()
    {
        if (!CanBuyPole()) return false;
        var cm = CurrencyManager.Instance;
        if (!cm.SpendCoins(firstPoleCoinCost)) return false;
        hasPole = true;
        Debug.Log("[Fishing] First pole purchased.");
        OnPoleLevelChanged?.Invoke(poleLevel);
        OnChanged?.Invoke();
        return true;
    }

    // ── Pole upgrade (Coins + Wood) ──────────────────────────────────────

    public int NextUpgradeCoinCost()
    {
        if (poleLevel >= MaxPoleLevel || poleCoinCosts.Length == 0) return int.MaxValue;
        return poleCoinCosts[Mathf.Min(poleLevel, poleCoinCosts.Length - 1)];
    }

    public int NextUpgradeWoodCost()
    {
        if (poleLevel >= MaxPoleLevel || poleWoodCosts.Length == 0) return int.MaxValue;
        return poleWoodCosts[Mathf.Min(poleLevel, poleWoodCosts.Length - 1)];
    }

    public bool CanUpgradePole()
    {
        var cm = CurrencyManager.Instance;
        if (cm == null || !hasPole) return false;
        return FishingMath.CanUpgradePole(poleLevel, MaxPoleLevel, cm.Coins, NextUpgradeCoinCost(), cm.Wood, NextUpgradeWoodCost());
    }

    public bool TryUpgradePole()
    {
        if (!CanUpgradePole()) return false;
        var cm = CurrencyManager.Instance;
        int coinCost = NextUpgradeCoinCost();
        int woodCost = NextUpgradeWoodCost();
        if (!cm.SpendCoins(coinCost)) return false;
        if (!cm.SpendWood(woodCost)) { cm.AddCoins(coinCost); return false; } // refund like axe upgrade
        poleLevel++;
        OnPoleLevelChanged?.Invoke(poleLevel);
        OnChanged?.Invoke();
        return true;
    }

    public void SetHasPole(bool value) { hasPole = value; OnPoleLevelChanged?.Invoke(poleLevel); }
    public void SetPoleLevel(int level) { poleLevel = Mathf.Clamp(level, 0, MaxPoleLevel); OnPoleLevelChanged?.Invoke(poleLevel); }

    // ── Save / load ──────────────────────────────────────────────────────

    public void CaptureTo(GameData d)
    {
        d.poleLevel = poleLevel;
        d.hasPole = hasPole;
        d.fishingState = (int)state;
        d.fishingCastUtcTicks = castUtcTicks;
        d.fishingBiteReadyUtcTicks = biteReadyUtcTicks;
        d.fishingPendingTier = pendingTier;
    }

    public void LoadFrom(GameData d)
    {
        poleLevel = Mathf.Clamp(d.poleLevel, 0, MaxPoleLevel);
        hasPole = d.hasPole;
        state = (CastState)Mathf.Clamp(d.fishingState, 0, 2);
        castUtcTicks = d.fishingCastUtcTicks;
        biteReadyUtcTicks = d.fishingBiteReadyUtcTicks;
        pendingTier = d.fishingPendingTier;

        // Offline catch-up: a line left waiting whose bite time has passed is now biting.
        if (state == CastState.Waiting && biteReadyUtcTicks > 0 && DateTime.UtcNow.Ticks >= biteReadyUtcTicks)
            TransitionToBite();

        OnPoleLevelChanged?.Invoke(poleLevel);
        OnChanged?.Invoke();
    }
}
