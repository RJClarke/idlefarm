using UnityEngine;

/// <summary>The 7 Barn skill tracks (spec §4.2), fixed order — index matches array position.</summary>
public enum FarmSkillTrack { Harvesting, Planting, Watering, Fishing, Forestry, Ranching, Processing }

/// <summary>
/// Pure state for the 7 Barn skill tracks: per-track level (0-25 each, 175 total = "Renown").
/// No respec (spec §4.1) — TryLevelUp only ever increments. Points are spent from
/// ReputationManager's pool (Phase 2), not owned here.
/// </summary>
public class FarmSkillsCore
{
    public const int MaxLevel = 25;
    private static readonly int TrackCount = System.Enum.GetValues(typeof(FarmSkillTrack)).Length;

    private readonly int[] levels = new int[TrackCount];

    public int GetLevel(FarmSkillTrack track) => levels[(int)track];
    public int TotalLevels { get { int sum = 0; foreach (int l in levels) sum += l; return sum; } }

    public bool TryLevelUp(FarmSkillTrack track)
    {
        int i = (int)track;
        if (levels[i] >= MaxLevel) return false;
        levels[i]++;
        return true;
    }

    public void Import(int[] source)
    {
        for (int i = 0; i < levels.Length; i++)
            levels[i] = (source != null && i < source.Length) ? Mathf.Clamp(source[i], 0, MaxLevel) : 0;
    }

    public int[] Export() => (int[])levels.Clone();
}
