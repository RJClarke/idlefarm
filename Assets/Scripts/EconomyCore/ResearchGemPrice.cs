using System;

/// <summary>Gem cost to finish the current research level now (spec 2026-10-05 §7): ceil(hours left x rate),
/// min 1 when any time is left, 0 when none (or the rate is 0). A repeatable, never-ending gem sink.</summary>
public static class ResearchGemPrice
{
    public static int GemsToFinish(double secondsLeft, float gemsPerHour)
    {
        if (secondsLeft <= 0 || gemsPerHour <= 0f) return 0;
        double gems = Math.Ceiling(secondsLeft / 3600.0 * gemsPerHour - 1e-9);
        return (int)Math.Max(1, gems);
    }
}
