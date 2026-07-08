using UnityEngine;

/// <summary>
/// Pure metadata for the three fish tiers (spec §3). No Unity object dependencies. Economic
/// values (odds, bite rate, raw/smoked value, smoke hours) are inspector knobs on the managers;
/// only the stable count + display names live here so every consumer agrees on them.
/// </summary>
public static class FishTiers
{
    public const int Count = 3;

    private static readonly string[] Names = { "Perch", "Bass", "Northern Pike" };

    /// <summary>Display name for a 1-based tier, clamped to the valid range.</summary>
    public static string Name(int tier) => Names[Mathf.Clamp(tier, 1, Count) - 1];

    /// <summary>Display name of the smoked product for a 1-based tier.</summary>
    public static string SmokedName(int tier) => "Smoked " + Name(tier);
}
