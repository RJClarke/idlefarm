using System;
using NUnit.Framework;

public class FreeGiftCoreTests
{
    private const string Day1 = "2026-10-04";
    private const string Day2 = "2026-10-05";
    private static readonly long T0 = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc).Ticks;
    private static long Min(double m) => (long)(m * TimeSpan.TicksPerMinute);

    private static FreeGiftCore NewCore() => new FreeGiftCore(new FreeGiftRules());

    [Test]
    public void FirstEverClaim_IsReadyAndFree()
    {
        var c = NewCore();
        Assert.AreEqual(GiftStatus.Ready, c.Status(true, T0, Day1));
        Assert.IsTrue(c.IsFreeChest(Day1));
        Assert.AreEqual(0, c.SecondsUntilReady(T0));
    }

    [Test]
    public void Locked_WhenNotUnlocked()
    {
        Assert.AreEqual(GiftStatus.Locked, NewCore().Status(false, T0, Day1));
    }

    [Test]
    public void AfterClaim_CooldownThirtyMinutes_ThenReady()
    {
        var c = NewCore();
        c.RecordClaim(T0, Day1, viaAd: false);
        Assert.AreEqual(GiftStatus.Cooldown, c.Status(true, T0 + Min(29), Day1));
        Assert.AreEqual(60, c.SecondsUntilReady(T0 + Min(29)), 0.001);
        Assert.AreEqual(GiftStatus.Ready, c.Status(true, T0 + Min(30), Day1));
    }

    [Test]
    public void SecondClaimOfDay_IsNotFree()
    {
        var c = NewCore();
        c.RecordClaim(T0, Day1, viaAd: false);
        Assert.IsFalse(c.IsFreeChest(Day1));
        Assert.AreEqual(1, c.ClaimsToday(Day1));
    }

    [Test]
    public void DailyCap_TenClaims_ThenCapped()
    {
        var c = NewCore();
        for (int i = 0; i < 10; i++) c.RecordClaim(T0 + Min(30 * i), Day1, viaAd: true);
        Assert.AreEqual(GiftStatus.Capped, c.Status(true, T0 + Min(30 * 10), Day1));
    }

    [Test]
    public void NewDay_ResetsCountAndFreeChest()
    {
        var c = NewCore();
        for (int i = 0; i < 10; i++) c.RecordClaim(T0 + Min(30 * i), Day1, viaAd: true);
        long nextDay = T0 + Min(60 * 14);
        Assert.AreEqual(GiftStatus.Ready, c.Status(true, nextDay, Day2));
        Assert.IsTrue(c.IsFreeChest(Day2));
        Assert.AreEqual(0, c.ClaimsToday(Day2));
        c.RecordClaim(nextDay, Day2, viaAd: false);
        Assert.AreEqual(1, c.ClaimsToday(Day2));
        Assert.AreEqual(0, c.ClaimsToday(Day1)); // the saved date moved on; old-day counts are gone
    }

    [Test]
    public void ClockRolledBack_CooldownNotShortened_AndHealReanchors()
    {
        var c = NewCore();
        c.RecordClaim(T0, Day1, viaAd: false);
        long earlier = T0 - Min(120);
        Assert.AreEqual(1800, c.SecondsUntilReady(earlier), 0.001); // full cooldown, never less
        c.HealClock(earlier);
        Assert.AreEqual(earlier, c.LastClaimUtcTicks);
        Assert.AreEqual(GiftStatus.Ready, c.Status(true, earlier + Min(30), Day1));
    }

    [Test]
    public void HealClock_NoOpWhenClockIsForward()
    {
        var c = NewCore();
        c.RecordClaim(T0, Day1, viaAd: false);
        c.HealClock(T0 + Min(5));
        Assert.AreEqual(T0, c.LastClaimUtcTicks);
    }

    [Test]
    public void Pitch_FiresOnceOnThirdAdClaim()
    {
        var c = NewCore();
        Assert.IsFalse(c.RecordClaim(T0, Day1, viaAd: true));
        Assert.IsFalse(c.RecordClaim(T0 + Min(30), Day1, viaAd: true));
        Assert.IsTrue(c.RecordClaim(T0 + Min(60), Day1, viaAd: true));
        Assert.IsTrue(c.PitchShown);
        Assert.IsFalse(c.RecordClaim(T0 + Min(90), Day1, viaAd: true));
    }

    [Test]
    public void Pitch_FreeAndPassClaimsDoNotCount()
    {
        var c = NewCore();
        for (int i = 0; i < 5; i++) Assert.IsFalse(c.RecordClaim(T0 + Min(30 * i), Day1, viaAd: false));
        Assert.AreEqual(0, c.LifetimeAdClaims);
        Assert.IsFalse(c.PitchShown);
    }

    [Test]
    public void ImportRoundTrip_RestoresState()
    {
        var c = NewCore();
        c.Import(T0, Day1, 4, 7, true);
        Assert.AreEqual(T0, c.LastClaimUtcTicks);
        Assert.AreEqual(4, c.ClaimsToday(Day1));
        Assert.AreEqual(7, c.LifetimeAdClaims);
        Assert.IsTrue(c.PitchShown);
    }

    [Test]
    public void Import_NullDateAndNegativeCounts_AreSafe()
    {
        var c = NewCore();
        c.Import(-5, null, -3, -1, false);
        Assert.AreEqual(0, c.ClaimsToday(Day1));
        Assert.AreEqual(0, c.LifetimeAdClaims);
        Assert.AreEqual(GiftStatus.Ready, c.Status(true, T0, Day1));
    }

    [TestCase(-5, 30)]
    [TestCase(0, 30)]
    [TestCase(5, 65)]
    [TestCase(10, 100)]
    [TestCase(17, 330)]   // 100 + 500 * 7/15 = 333.3 -> nearest 10
    [TestCase(25, 600)]
    [TestCase(50, 4000)]
    [TestCase(75, 5000)]
    [TestCase(175, 7500)]
    [TestCase(400, 7500)]
    public void CoinsForLevel_InterpolatesAndClamps(int level, int expected)
    {
        Assert.AreEqual(expected, FreeGiftCore.CoinsForLevel(FreeGiftRules.DefaultAnchors, level));
    }

    [Test]
    public void CoinsForLevel_EmptyAnchors_IsZero()
    {
        Assert.AreEqual(0, FreeGiftCore.CoinsForLevel(new GiftCoinAnchor[0], 10));
        Assert.AreEqual(0, FreeGiftCore.CoinsForLevel(null, 10));
    }

    [Test]
    public void LocalDateKey_IsInvariantIsoDate()
    {
        Assert.AreEqual("2026-01-09", FreeGiftCore.LocalDateKey(new DateTime(2026, 1, 9, 23, 59, 0)));
    }
}
