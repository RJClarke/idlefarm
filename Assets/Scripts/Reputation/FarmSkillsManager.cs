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

    public float GetBonus(FarmSkillTrack track)
    {
        float perPoint = track switch
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
        return core.GetLevel(track) * perPoint;
    }

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
    public void LoadFrom(GameData d)
    {
        core.Import(d.farmSkillLevels);
        OnChanged?.Invoke();
    }
}
