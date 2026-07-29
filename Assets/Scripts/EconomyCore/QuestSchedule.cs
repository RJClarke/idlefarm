using System;
using System.Collections.Generic;

/// <summary>
/// Pure scheduling math for daily quest drops and the weekly milestone week.
///
/// The game clock is UTC shifted by a FIXED offset (<see cref="GameUtcOffsetHours"/>) with no
/// daylight-saving adjustment. That is deliberate. The previous implementation asked the OS for a
/// named timezone — "Central Standard Time", a Windows-only ID, falling back to "America/Chicago"
/// — which resolves in the Windows Editor but throws on Android, taking quest initialisation down
/// with it (empty quest list, "Next drop in 0m"). Nothing here touches TimeZoneInfo, so it behaves
/// identically on every platform.
///
/// Dropping true DST costs nothing observable: no player-facing string ever shows an absolute
/// clock time. The quest popup shows a relative countdown ("Next drop in 3h 24m") and a bare day
/// name ("resets Sun"), so a one-hour seasonal drift is invisible. What the fixed offset buys is
/// that drops are the same real-world instant for everybody, which keeps a synchronised event
/// possible later and means changing the device timezone cannot manufacture extra drops.
///
/// A -6 offset against a 6-hour drop interval puts every drop on a 00/06/12/18 UTC boundary, and
/// the week boundary (Sunday 00:00 game time) on Sunday 06:00 UTC — still Sunday everywhere from
/// the US Midwest eastward through Asia-Pacific.
/// </summary>
public static class QuestSchedule
{
    /// <summary>Fixed offset from UTC, in hours. -6 = US Central Standard, never DST-adjusted.</summary>
    public const int GameUtcOffsetHours = -6;

    /// <summary>Hours between quest drops. Drops sit at game-time 00:00, 06:00, 12:00, 18:00.</summary>
    public const int DropIntervalHours = 6;

    public const int DropsPerDay = 24 / DropIntervalHours;

    /// <summary>Extra whole game-days of drops the catch-up sweep reaches back over, bounding how
    /// many quests a returning player receives at once.</summary>
    private const int LookbackDays = 1;

    public static DateTime ToGameTime(DateTime utc) => utc.AddHours(GameUtcOffsetHours);

    public static DateTime ToUtc(DateTime gameTime) => gameTime.AddHours(-GameUtcOffsetHours);

    /// <summary>The drop instant at or before <paramref name="nowUtc"/>.</summary>
    public static DateTime MostRecentDropUtc(DateTime nowUtc)
    {
        DateTime g = ToGameTime(nowUtc);
        int slotHour = (g.Hour / DropIntervalHours) * DropIntervalHours;
        return ToUtc(g.Date.AddHours(slotHour));
    }

    /// <summary>The next drop instant, always STRICTLY after <paramref name="nowUtc"/> — including
    /// when now sits exactly on a boundary. A non-future result is what rendered the footer's
    /// "Next drop in 0m".</summary>
    public static DateTime NextDropUtc(DateTime nowUtc)
        => MostRecentDropUtc(nowUtc).AddHours(DropIntervalHours);

    /// <summary>Every drop instant at or before <paramref name="nowUtc"/> within the current and
    /// previous game-day, ascending.</summary>
    public static List<DateTime> RecentDropsUtc(DateTime nowUtc)
    {
        var drops = new List<DateTime>((LookbackDays + 1) * DropsPerDay);
        DateTime firstDay = ToGameTime(nowUtc).Date.AddDays(-LookbackDays);
        for (int day = 0; day <= LookbackDays; day++)
        {
            DateTime date = firstDay.AddDays(day);
            for (int hour = 0; hour < 24; hour += DropIntervalHours)
            {
                DateTime dropUtc = ToUtc(date.AddHours(hour));
                if (dropUtc <= nowUtc) drops.Add(dropUtc);
            }
        }
        return drops; // generated in ascending order
    }

    /// <summary>Start of the milestone week containing <paramref name="nowUtc"/>: the most recent
    /// Sunday 00:00 game time.</summary>
    public static DateTime WeekStartUtc(DateTime nowUtc)
    {
        DateTime g = ToGameTime(nowUtc);
        return ToUtc(g.Date.AddDays(-(int)g.DayOfWeek));
    }
}
