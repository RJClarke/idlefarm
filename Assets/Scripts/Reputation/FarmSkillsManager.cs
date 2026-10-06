using System;
using UnityEngine;

/// <summary>
/// Owns the 7 Barn skill tracks: level-up (spends a ReputationManager point), per-point bonus
/// lookup, and tier-unlock checks. Spec: 2026-07-19-reputation-design.md §4.
/// </summary>
public class FarmSkillsManager : MonoBehaviour
{
    public static FarmSkillsManager Instance { get; private set; }

    [Header("Per-point bonus values (spec §4.2 defaults)")]
    [SerializeField] private float harvestingPerPoint = 0.04f;  // +4% Money from crop sales
    [SerializeField] private float plantingPerPoint = 0.02f;    // +2% growth speed
    [SerializeField] private float wateringPerPoint = 0.03f;    // +3% moisture duration
    [SerializeField] private float fishingPerPoint = 0.02f;     // -2% bite wait time
    [SerializeField] private float forestryPerPoint = 0.02f;    // +2% wood per chop
    [SerializeField] private float ranchingPerPoint = 0.03f;    // +3% egg value
    [SerializeField] private float processingPerPoint = 0.02f;  // +2% cook/smoke speed

    [Header("Milestone perks (levels 5/10/15/20 grow one perk; 25 is a capstone)")]
    [Tooltip("Chance added per 5/10/15/20 milestone for every chance-based growing perk (double harvest, free seed, double catch, double wood, double egg, double batch).")]
    [SerializeField] private float milestoneChanceStep = 0.05f;
    [Tooltip("Watering's growing perk: extra sprinkler reach per milestone (0.10 = 10% farther).")]
    [SerializeField] private float sprinklerReachStep = 0.10f;
    [Tooltip("Harvesting Lv 25: chance a harvest is golden.")]
    [SerializeField] private float goldenCropChance = 0.02f;
    [Tooltip("Harvesting Lv 25: money + coin multiplier on a golden harvest.")]
    [SerializeField] private int goldenCropMultiplier = 10;
    [Tooltip("Planting Lv 25: chance a newly planted seed skips straight to the sprout stage.")]
    [SerializeField] private float instantSproutChance = 0.10f;
    [Tooltip("Fishing Lv 25: multiplier on rare-fish (Bass, Pike) catch weights.")]
    [SerializeField] private float rareFishMultiplier = 2f;
    [Tooltip("Forestry Lv 25: tree regrow speed multiplier.")]
    [SerializeField] private float treeRegrowMultiplier = 2f;
    [Tooltip("Ranching Lv 25: dog movement speed multiplier.")]
    [SerializeField] private float dogSpeedMultiplier = 1.5f;
    [Tooltip("Processing Lv 25: extra cook/smoke speed on top of the per-level bonus.")]
    [SerializeField] private float processingCapstoneSpeed = 0.25f;

    private static readonly int[] TierLevels = { 5, 10, 15, 20, 25 };

    private readonly FarmSkillsCore core = new FarmSkillsCore();

    public event Action OnChanged;

    public int TotalLevels => core.TotalLevels;
    public int GetLevel(FarmSkillTrack track) => core.GetLevel(track);
    public static int[] TierMarkers => TierLevels;

    public bool IsTierUnlocked(FarmSkillTrack track, int tierLevel) => core.GetLevel(track) >= tierLevel;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>The bonus a single level adds (e.g. 0.04 = +4%). Exposed so the Barn UI reads the
    /// live tuning values instead of keeping a hand-copied duplicate.</summary>
    public float GetPerPoint(FarmSkillTrack track) => track switch
    {
        FarmSkillTrack.Harvesting => harvestingPerPoint,
        FarmSkillTrack.Planting => plantingPerPoint,
        FarmSkillTrack.Watering => wateringPerPoint,
        FarmSkillTrack.Fishing => fishingPerPoint,
        FarmSkillTrack.Forestry => forestryPerPoint,
        FarmSkillTrack.Ranching => ranchingPerPoint,
        FarmSkillTrack.Processing => processingPerPoint,
        _ => 0f,
    };

    public float GetBonus(FarmSkillTrack track) => core.GetLevel(track) * GetPerPoint(track);

    // ── Milestone perks ─────────────────────────────────────────────────
    // Every track's 5/10/15/20 milestones grow one perk; level 25 adds a capstone. Watering's
    // growing perk is a reach multiplier, the rest are chances. Gameplay reads these via the
    // static helpers below so call sites stay one-liners and are null-safe without a manager.

    public float MilestoneChanceStep => milestoneChanceStep;
    public float SprinklerReachStep => sprinklerReachStep;
    public float GoldenCropChanceValue => goldenCropChance;
    public int GoldenCropMultiplierValue => goldenCropMultiplier;
    public float InstantSproutChanceValue => instantSproutChance;
    public float RareFishMultiplierValue => rareFishMultiplier;
    public float TreeRegrowMultiplierValue => treeRegrowMultiplier;
    public float DogSpeedMultiplierValue => dogSpeedMultiplier;
    public float ProcessingCapstoneSpeedValue => processingCapstoneSpeed;

    public bool HasCapstone(FarmSkillTrack track) => FarmSkillsCore.HasCapstone(core.GetLevel(track));

    /// <summary>The track's growing-perk chance at its current level (0 before level 5).</summary>
    public float MilestoneChance(FarmSkillTrack track)
        => FarmSkillsCore.GrowingTiersReached(core.GetLevel(track)) * milestoneChanceStep;

    /// <summary>One roll against the track's growing-perk chance. False when no manager exists.</summary>
    public static bool RollMilestone(FarmSkillTrack track)
    {
        if (Instance == null) return false;
        float chance = Instance.MilestoneChance(track);
        return chance > 0f && UnityEngine.Random.value < chance;
    }

    /// <summary>Growing-perk chance for a track, 0 without a manager (for stacking onto another chance).</summary>
    public static float MilestoneChanceOf(FarmSkillTrack track)
        => Instance != null ? Instance.MilestoneChance(track) : 0f;

    /// <summary>Watering: sprinkler radius multiplier (1.0 before level 5, 1.4 at level 20+).</summary>
    public static float SprinklerReachMultiplier
        => Instance != null
            ? 1f + FarmSkillsCore.GrowingTiersReached(Instance.core.GetLevel(FarmSkillTrack.Watering)) * Instance.sprinklerReachStep
            : 1f;

    public static bool Capstone(FarmSkillTrack track) => Instance != null && Instance.HasCapstone(track);

    /// <summary>Harvesting Lv 25: 1 normally, the golden multiplier on a lucky roll.</summary>
    /// <summary>Average payout multiplier from golden harvests (away-runs use the average, not a roll).</summary>
    public static float GoldenExpectedMultiplier
        => Capstone(FarmSkillTrack.Harvesting)
            ? 1f + Instance.goldenCropChance * (Mathf.Max(1, Instance.goldenCropMultiplier) - 1)
            : 1f;

    public static int RollGoldenMultiplier()
        => Capstone(FarmSkillTrack.Harvesting) && UnityEngine.Random.value < Instance.goldenCropChance
            ? Mathf.Max(1, Instance.goldenCropMultiplier) : 1;

    /// <summary>Planting Lv 25: true when a new seed should skip straight to sprout.</summary>
    public static bool RollInstantSprout()
        => Capstone(FarmSkillTrack.Planting) && UnityEngine.Random.value < Instance.instantSproutChance;

    public static float RareFishMultiplier => Capstone(FarmSkillTrack.Fishing) ? Instance.rareFishMultiplier : 1f;
    public static float TreeRegrowMultiplier => Capstone(FarmSkillTrack.Forestry) ? Mathf.Max(0.01f, Instance.treeRegrowMultiplier) : 1f;
    public static float DogSpeedMultiplier => Capstone(FarmSkillTrack.Ranching) ? Instance.dogSpeedMultiplier : 1f;

    /// <summary>Processing: full cook-speed multiplier — per-level bonus plus the Lv 25 capstone.</summary>
    public static float ProcessingSpeedMultiplier
        => Instance != null
            ? 1f + Instance.GetBonus(FarmSkillTrack.Processing)
                 + (Instance.HasCapstone(FarmSkillTrack.Processing) ? Instance.processingCapstoneSpeed : 0f)
            : 1f;

    public bool TryLevelUp(FarmSkillTrack track)
    {
        if (ReputationManager.Instance == null || !ReputationManager.Instance.TrySpendPoint()) return false;
        core.TryLevelUp(track); // TrySpendPoint already guaranteed room to spend; level cap is separately enforced by the UI disabling "+" at 25
        Debug.Log($"[FarmSkills] {track} -> level {core.GetLevel(track)}");
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Dev-tools only: wipes all 7 tracks and refunds the spent points back to the unspent pool.</summary>
    public void DevResetAllSkills()
    {
        int refund = core.ResetAll();
        if (refund > 0 && ReputationManager.Instance != null) ReputationManager.Instance.DevAddPoints(refund);
        Debug.Log($"[FarmSkills] Dev reset — refunded {refund} points.");
        OnChanged?.Invoke();
    }

    public void CaptureTo(GameData d) => d.farmSkillLevels = core.Export();
    /// <summary>Dev/testing (balance bench): every Farm Level skill at this fraction of its max.</summary>
    public void DevSetAll(float fraction)
    {
        var max = new int[System.Enum.GetValues(typeof(FarmSkillTrack)).Length];
        for (int i = 0; i < max.Length; i++) max[i] = Mathf.CeilToInt(FarmSkillsCore.MaxLevel * Mathf.Clamp01(fraction));
        core.Import(max);
        OnChanged?.Invoke();
    }

    public void LoadFrom(GameData d)
    {
        core.Import(d.farmSkillLevels);
        OnChanged?.Invoke();
    }
}
