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

    [Header("Cast / Reel (spec 2026-07-12)")]
    [Tooltip("Reel taps for the shortest cast.")]
    [SerializeField] private int minReelTaps = 3;
    [Tooltip("Reel taps for a full-power cast.")]
    [SerializeField] private int maxReelTaps = 10;

    [Header("Whirlpool")]
    [Tooltip("Average seconds to a bite while the bobber sits inside a whirlpool (fast, not instant).")]
    [SerializeField] private float hotspotBiteAvgSeconds = 20f;

    private int poleLevel;
    private bool hasPole;
    private CastState state = CastState.Idle;
    private long castUtcTicks;
    private long biteReadyUtcTicks;
    private int pendingTier;
    private WorldHintPopup activeHint;

    private float castPower01;
    private Vector2 castDir = Vector2.up;
    private int reelTapsTotal;
    private int reelTapsRemaining;
    private bool inHotspot;
    private bool caughtFromHotspot;

    public event Action OnChanged;             // durable: state/pole change, load
    public event Action<int> OnPoleLevelChanged; // Carpenter UI refresh (mirrors OnAxeLevelChanged)

    public bool HasPole => hasPole;
    public int PoleLevel => poleLevel;
    public int MaxPoleLevel => Mathf.Max(0, poleTiers.Length - 1);
    public int FirstPoleCoinCost => firstPoleCoinCost;
    public CastState State => state;
    public bool HasBite => state == CastState.Bite;
    public int PendingTier => pendingTier;
    public float CastPower01 => castPower01;
    public Vector2 CastDir => castDir;
    public int ReelTapsRemaining => reelTapsRemaining;
    public int ReelTapsTotal => reelTapsTotal;
    public float ReelProgress01 => reelTapsTotal > 0 ? (float)reelTapsRemaining / reelTapsTotal : 0f;
    public bool CaughtFromHotspot => caughtFromHotspot;

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
        caughtFromHotspot = inHotspot;
        state = CastState.Bite;
        Debug.Log($"[Fishing] Bite: {FishTiers.Name(pendingTier)} on the line.");
        OnChanged?.Invoke();
    }

    // ── Cast / reel / collect (spec §3a + 2026-07-12) ────────────────────

    /// <summary>Cast the line if idle and a pole is owned. Power sets reel effort + landing distance;
    /// dir is the aim direction (unit). Rolls a baseline bite time (UtcNow-anchored).</summary>
    public bool Cast(float power01, Vector2 dir)
    {
        if (!hasPole || state != CastState.Idle) return false;
        castPower01 = Mathf.Clamp01(power01);
        castDir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.up;
        reelTapsTotal = FishingMath.ReelTapsForPower(minReelTaps, maxReelTaps, castPower01);
        reelTapsRemaining = reelTapsTotal;
        inHotspot = false;
        caughtFromHotspot = false;
        RollBaselineBite();
        state = CastState.Waiting;
        Debug.Log($"[Fishing] Cast power={castPower01:0.00} taps={reelTapsTotal}.");
        OnChanged?.Invoke();
        return true;
    }

    private void RollBaselineBite()
    {
        long now = DateTime.UtcNow.Ticks;
        double secs = FishingMath.RollBiteSeconds(CurrentTier().biteAvgSeconds, UnityEngine.Random.value);
        castUtcTicks = now;
        biteReadyUtcTicks = now + (long)(secs * TimeSpan.TicksPerSecond);
    }

    private void RollHotspotBite()
    {
        long now = DateTime.UtcNow.Ticks;
        double secs = FishingMath.RollBiteSeconds(hotspotBiteAvgSeconds, UnityEngine.Random.value);
        biteReadyUtcTicks = now + (long)(secs * TimeSpan.TicksPerSecond);
    }

    /// <summary>Bobber entered/left a live whirlpool (called by LakeNode). Re-anchors the bite:
    /// entering grants a fast bite; leaving before biting reverts to baseline. No-op once biting.</summary>
    public void SetInHotspot(bool inside)
    {
        if (state != CastState.Waiting || inside == inHotspot) return;
        inHotspot = inside;
        if (inside) RollHotspotBite(); else RollBaselineBite();
        OnChanged?.Invoke();
    }

    /// <summary>Pull the bobber one step toward shore. Valid only while Waiting/Bite. Reaching shore
    /// lands a biting fish (Collect) or retrieves an empty line. Returns true if a step was consumed.</summary>
    public bool Reel()
    {
        if (state != CastState.Waiting && state != CastState.Bite) return false;
        if (reelTapsRemaining > 0) reelTapsRemaining--;
        if (reelTapsRemaining > 0) { OnChanged?.Invoke(); return true; }
        if (state == CastState.Bite) Collect(); else RetrieveEmpty();
        return true;
    }

    private void RetrieveEmpty()
    {
        ClearLine();
        state = CastState.Idle;
        Debug.Log("[Fishing] Line retrieved (no fish).");
        OnChanged?.Invoke();
    }

    private void ClearLine()
    {
        castUtcTicks = 0; biteReadyUtcTicks = 0; pendingTier = 0;
        castPower01 = 0f; castDir = Vector2.up; reelTapsTotal = 0; reelTapsRemaining = 0;
        inHotspot = false; caughtFromHotspot = false;
    }

    /// <summary>Collect a fish on the line into the Pantry. Returns the caught tier (0 if none).</summary>
    public int Collect()
    {
        if (state != CastState.Bite) return 0;
        int tier = pendingTier;
        if (PantryManager.Instance != null) PantryManager.Instance.AddRaw(tier);
        ClearLine();
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
        d.fishingCastPower01 = castPower01;
        d.fishingCastDirX = castDir.x;
        d.fishingCastDirY = castDir.y;
        d.fishingReelTapsTotal = reelTapsTotal;
        d.fishingReelTapsRemaining = reelTapsRemaining;
    }

    public void LoadFrom(GameData d)
    {
        poleLevel = Mathf.Clamp(d.poleLevel, 0, MaxPoleLevel);
        hasPole = d.hasPole;
        state = (CastState)Mathf.Clamp(d.fishingState, 0, 2);
        castUtcTicks = d.fishingCastUtcTicks;
        biteReadyUtcTicks = d.fishingBiteReadyUtcTicks;
        pendingTier = d.fishingPendingTier;
        castPower01 = Mathf.Clamp01(d.fishingCastPower01);
        castDir = new Vector2(d.fishingCastDirX, d.fishingCastDirY);
        if (castDir.sqrMagnitude < 1e-6f) castDir = Vector2.up; else castDir.Normalize();
        reelTapsTotal = Mathf.Max(0, d.fishingReelTapsTotal);
        reelTapsRemaining = Mathf.Clamp(d.fishingReelTapsRemaining, 0, Mathf.Max(0, reelTapsTotal));

        // Offline catch-up: a line left waiting whose bite time has passed is now biting.
        if (state == CastState.Waiting && biteReadyUtcTicks > 0 && DateTime.UtcNow.Ticks >= biteReadyUtcTicks)
            TransitionToBite();

        OnPoleLevelChanged?.Invoke(poleLevel);
        OnChanged?.Invoke();
    }
}
