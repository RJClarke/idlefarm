using System;

/// <summary>
/// Forward-only wall-clock deltas. Lives in EconomyCore (no UnityEngine) so it is unit-testable.
///
/// Every offline / catch-up system in the game computes progress as (now - savedTimestamp) in
/// UTC ticks. If the device clock is set BACKWARD, that delta goes negative, which — depending
/// on the consumer — could grant nothing but freeze progress until real time catches back up, or
/// worse, accrue negative amounts. This helper collapses any non-positive gap to 0 so callers
/// can never see a negative elapsed. Clock-FORWARD is intentionally NOT defended against
/// (single-player, accepted). Callers that also want to HEAL a rolled-back anchor should, on a
/// zero result, re-anchor their stored timestamp to "now" so progress resumes immediately.
/// </summary>
public static class OfflineClock
{
    /// <summary>
    /// Elapsed seconds between two UTC tick timestamps, clamped to &gt;= 0. Returns 0 when the
    /// clock moved backward (<paramref name="nowUtcTicks"/> &lt;= <paramref name="lastUtcTicks"/>)
    /// or when <paramref name="lastUtcTicks"/> is unset (&lt;= 0).
    /// </summary>
    public static double ForwardGapSeconds(long lastUtcTicks, long nowUtcTicks)
    {
        if (lastUtcTicks <= 0) return 0.0;
        long delta = nowUtcTicks - lastUtcTicks;
        if (delta <= 0) return 0.0;
        return delta / (double)TimeSpan.TicksPerSecond;
    }
}
