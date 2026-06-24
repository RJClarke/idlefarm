using System;

/// <summary>Farm-name rules: required, 3–30 characters (trimmed).</summary>
public static class FarmName
{
    public const int Min = 3;
    public const int Max = 30;

    public static bool IsValid(string name)
    {
        if (name == null) return false;
        int len = name.Trim().Length;
        return len >= Min && len <= Max;
    }

    public static string Sanitize(string name)
    {
        if (name == null) return "";
        string t = name.Trim();
        return t.Length > Max ? t.Substring(0, Max) : t;
    }
}

/// <summary>Curated playful farm names used to prefill the first-run field and the
/// dice re-roll. Every entry MUST satisfy FarmName.IsValid.</summary>
public static class FarmNameSuggestions
{
    public static readonly string[] All =
    {
        "Sunny Acres", "Maple Hollow", "Clover Creek", "Golden Meadow", "Whisker Farm",
        "Dewdrop Dell", "Pebble Patch", "Honey Hill", "Willow Brook", "Bramble Barn",
        "Cricket Field", "Marigold Ranch", "Pumpkin Patch", "Cozy Corner", "Fern Gully",
        "Berry Bend", "Thistle Down", "Robin Roost", "Dandelion Den", "Buttercup Bay",
    };

    public static int Count => All.Length;

    public static string At(int index)
    {
        int i = ((index % All.Length) + All.Length) % All.Length;
        return All[i];
    }

    public static string Random(Random rng)
    {
        if (rng == null) rng = new Random();
        return All[rng.Next(All.Length)];
    }
}
