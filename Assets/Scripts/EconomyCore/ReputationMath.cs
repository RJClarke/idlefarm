using UnityEngine;

/// <summary>
/// Pure reputation math (Reputation Phase 2, spec §3.4/§3.2): the lifetime point-cost curve and
/// the consecutive-skip cost ladder. No Unity scene dependency.
/// </summary>
public static class ReputationMath
{
    private const int BaseCost = 85;
    private const int CostPerPoint = 15;

    /// <summary>Rep cost of the Nth lifetime point (1-based).</summary>
    public static int PointCost(int pointNumber) => BaseCost + CostPerPoint * Mathf.Max(1, pointNumber);

    /// <summary>Rep that exactly completes the next Barn point — the Welcome basket's reward, so a
    /// new player's first delivery always ends with a point to spend.</summary>
    public static int WelcomeBasketReward(int barProgress, int pointsEarned) =>
        Mathf.Max(1, PointCost(pointsEarned + 1) - Mathf.Max(0, barProgress));

    /// <summary>
    /// Applies a rep gain to the bar, awarding as many points as it covers. Extra progress
    /// beyond the last point carries forward as <paramref name="barAfter"/>.
    /// </summary>
    public static void ApplyGain(int barBefore, int pointsBefore, int gain, out int barAfter, out int pointsAwarded)
    {
        int bar = Mathf.Max(0, barBefore) + Mathf.Max(0, gain);
        int points = Mathf.Max(0, pointsBefore);
        int awarded = 0;
        int cost = PointCost(points + 1);
        while (bar >= cost)
        {
            bar -= cost;
            points++;
            awarded++;
            cost = PointCost(points + 1);
        }
        barAfter = bar;
        pointsAwarded = awarded;
    }

    private static readonly int[] SkipLadder = { 25, 50, 100, 150, 250 };

    /// <summary>Gem cost of the next skip, given how many skips have happened since the last fulfill.</summary>
    public static int SkipCost(int consecutiveSkips)
        => SkipLadder[Mathf.Clamp(consecutiveSkips, 0, SkipLadder.Length - 1)];
}
