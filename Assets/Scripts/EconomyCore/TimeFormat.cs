using UnityEngine;

/// <summary>
/// Pure time formatting helpers. Lives in EconomyCore so it is unit-testable and shared by
/// the run-stats / welcome-back UI and the offline simulator. No Unity scene dependency.
/// </summary>
public static class TimeFormat
{
    /// <summary>
    /// Adaptive "h m s": drops empty leading units, but keeps middle units once a larger one
    /// is shown. e.g. 30789 -> "8h 33m 9s", 1989 -> "33m 9s", 45 -> "45s", 0 -> "0s".
    /// </summary>
    public static string Hms(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int h = total / 3600;
        int m = (total % 3600) / 60;
        int s = total % 60;

        if (h > 0) return $"{h}h {m}m {s}s";
        if (m > 0) return $"{m}m {s}s";
        return $"{s}s";
    }
}
