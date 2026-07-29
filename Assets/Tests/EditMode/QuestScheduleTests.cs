using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Quest drop/week scheduling. These exist because the previous implementation asked the OS for a
/// named timezone ("Central Standard Time"), which resolves on Windows and throws on Android —
/// killing quest init on device. QuestSchedule must be pure arithmetic that behaves identically
/// everywhere, so every case here is platform-independent by construction.
/// </summary>
public class QuestScheduleTests
{
    // ── The device-breaking regression ───────────────────────────────────

    [Test]
    public void NextDropUtc_IsAlwaysStrictlyInTheFuture()
    {
        // The "Next drop in 0m" bug: a non-future next-drop makes the footer render a zero
        // countdown. Sweep a full day at minute resolution, including exact drop boundaries.
        DateTime start = new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc);
        for (int minute = 0; minute < 24 * 60; minute++)
        {
            DateTime now = start.AddMinutes(minute);
            Assert.Greater(QuestSchedule.NextDropUtc(now), now,
                $"next drop must be strictly after {now:O}");
        }
    }

    [Test]
    public void NextDropUtc_OnExactBoundary_ReturnsTheFollowingSlot()
    {
        DateTime boundary = new DateTime(2026, 7, 27, 6, 0, 0, DateTimeKind.Utc);
        Assert.AreEqual(boundary.AddHours(6), QuestSchedule.NextDropUtc(boundary));
    }

    [Test]
    public void NextDropUtc_NeverSkipsTheMidnightSlot()
    {
        // The old fallback jumped to "tomorrow 6am" after the last slot of the day, silently
        // skipping the 00:00 drop.
        DateTime lateEvening = new DateTime(2026, 7, 27, 23, 30, 0, DateTimeKind.Utc);
        DateTime next = QuestSchedule.NextDropUtc(lateEvening);
        Assert.AreEqual(new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc), next);
    }

    // ── Slot alignment ───────────────────────────────────────────────────

    [Test]
    public void DropSlots_LandOnSixHourUtcBoundaries()
    {
        // A -6h game offset with a 6h drop interval puts every drop on 00/06/12/18 UTC.
        DateTime start = new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc);
        for (int minute = 0; minute < 24 * 60; minute += 7)
        {
            DateTime slot = QuestSchedule.MostRecentDropUtc(start.AddMinutes(minute));
            Assert.AreEqual(0, slot.Minute);
            Assert.AreEqual(0, slot.Second);
            Assert.AreEqual(0, slot.Hour % QuestSchedule.DropIntervalHours);
        }
    }

    [Test]
    public void MostRecentDropUtc_IsNeverInTheFuture()
    {
        DateTime start = new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc); // US DST switch day
        for (int minute = 0; minute < 24 * 60; minute += 11)
        {
            DateTime now = start.AddMinutes(minute);
            Assert.LessOrEqual(QuestSchedule.MostRecentDropUtc(now), now);
        }
    }

    [Test]
    public void GameTimeRoundTrips()
    {
        DateTime utc = new DateTime(2026, 7, 27, 13, 45, 0, DateTimeKind.Utc);
        Assert.AreEqual(utc, QuestSchedule.ToUtc(QuestSchedule.ToGameTime(utc)));
    }

    // ── Catch-up window ──────────────────────────────────────────────────

    [Test]
    public void RecentDropsUtc_AreAscendingAndNeverFuture()
    {
        DateTime now = new DateTime(2026, 7, 27, 13, 5, 0, DateTimeKind.Utc);
        List<DateTime> drops = QuestSchedule.RecentDropsUtc(now);
        CollectionAssert.IsOrdered(drops);
        foreach (DateTime d in drops) Assert.LessOrEqual(d, now);
    }

    [Test]
    public void RecentDropsUtc_CoversTodayAndYesterdayOnly()
    {
        // Bounds the catch-up so a player returning after a week doesn't get a flood.
        DateTime now = new DateTime(2026, 7, 27, 23, 59, 0, DateTimeKind.Utc);
        List<DateTime> drops = QuestSchedule.RecentDropsUtc(now);
        Assert.LessOrEqual(drops.Count, 2 * QuestSchedule.DropsPerDay);
        Assert.GreaterOrEqual(drops.Count, QuestSchedule.DropsPerDay);
    }

    // ── Weekly milestone boundary ────────────────────────────────────────

    [Test]
    public void WeekStartUtc_IsSundayMidnightGameTime()
    {
        DateTime now = new DateTime(2026, 7, 27, 13, 0, 0, DateTimeKind.Utc);
        DateTime weekStart = QuestSchedule.WeekStartUtc(now);
        DateTime asGameTime = QuestSchedule.ToGameTime(weekStart);
        Assert.AreEqual(DayOfWeek.Sunday, asGameTime.DayOfWeek);
        Assert.AreEqual(TimeSpan.Zero, asGameTime.TimeOfDay);
    }

    [Test]
    public void WeekStartUtc_LandsAt0600Utc()
    {
        // Sunday 00:00 at a -6 offset is Sunday 06:00 UTC — still Sunday for every timezone from
        // the US Midwest eastward through Asia-Pacific, so the "resets Sun" label stays honest.
        DateTime weekStart = QuestSchedule.WeekStartUtc(new DateTime(2026, 7, 27, 13, 0, 0, DateTimeKind.Utc));
        Assert.AreEqual(DayOfWeek.Sunday, weekStart.DayOfWeek);
        Assert.AreEqual(6, weekStart.Hour);
    }

    [Test]
    public void WeekStartUtc_IsStableAcrossTheWholeWeek()
    {
        DateTime anchor = new DateTime(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc);
        DateTime expected = QuestSchedule.WeekStartUtc(anchor);
        for (int hour = 0; hour < 24 * 6; hour++)
            Assert.AreEqual(expected, QuestSchedule.WeekStartUtc(anchor.AddHours(hour)),
                "week start must not move until the next Sunday boundary");
    }

    [Test]
    public void WeekStartUtc_AdvancesExactlySevenDaysAcrossTheBoundary()
    {
        DateTime before = QuestSchedule.WeekStartUtc(new DateTime(2026, 7, 26, 5, 0, 0, DateTimeKind.Utc));
        DateTime after = QuestSchedule.WeekStartUtc(new DateTime(2026, 7, 26, 7, 0, 0, DateTimeKind.Utc));
        Assert.AreEqual(TimeSpan.FromDays(7), after - before);
    }
}
