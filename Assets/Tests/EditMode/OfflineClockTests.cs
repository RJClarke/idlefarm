using System;
using NUnit.Framework;

public class OfflineClockTests
{
    private const long TicksPerSecond = TimeSpan.TicksPerSecond;

    [Test]
    public void ForwardGapSeconds_NormalGap_ReturnsElapsedSeconds()
    {
        long last = new DateTime(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc).Ticks;
        long now = last + 300 * TicksPerSecond; // 5 minutes later

        double gap = OfflineClock.ForwardGapSeconds(last, now);

        Assert.AreEqual(300.0, gap, 1e-6);
    }

    [Test]
    public void ForwardGapSeconds_ZeroGap_ReturnsZero()
    {
        long t = new DateTime(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc).Ticks;

        Assert.AreEqual(0.0, OfflineClock.ForwardGapSeconds(t, t), 1e-9);
    }

    [Test]
    public void ForwardGapSeconds_ClockRolledBack_ReturnsZero()
    {
        long last = new DateTime(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc).Ticks;
        long now = last - 3600 * TicksPerSecond; // clock moved back an hour

        Assert.AreEqual(0.0, OfflineClock.ForwardGapSeconds(last, now), 1e-9);
    }

    [Test]
    public void ForwardGapSeconds_LastUnset_ReturnsZero()
    {
        long now = new DateTime(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc).Ticks;

        Assert.AreEqual(0.0, OfflineClock.ForwardGapSeconds(0L, now), 1e-9);
    }

    [Test]
    public void ForwardGapSeconds_LastNegative_ReturnsZero()
    {
        long now = new DateTime(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc).Ticks;

        Assert.AreEqual(0.0, OfflineClock.ForwardGapSeconds(-500L, now), 1e-9);
    }

    [Test]
    public void ForwardGapSeconds_SubSecondGap_ReturnsFractionalSeconds()
    {
        long last = new DateTime(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc).Ticks;
        long now = last + TicksPerSecond / 2; // half a second

        Assert.AreEqual(0.5, OfflineClock.ForwardGapSeconds(last, now), 1e-6);
    }
}
