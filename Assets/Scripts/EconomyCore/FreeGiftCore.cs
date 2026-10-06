using System;
using System.Globalization;

/// <summary>One point on the Free Gift coin curve: at this Overall Farm Level the chest pays this many Coins.</summary>
[Serializable]
public struct GiftCoinAnchor
{
    public int level;
    public int coins;
    public GiftCoinAnchor(int level, int coins) { this.level = level; this.coins = coins; }
}

public enum GiftStatus { Locked, Ready, Cooldown, Capped }

/// <summary>Free Gift tunables as plain data, so FreeGiftCore stays testable. FreeGiftTuning (SO) builds one.</summary>
public sealed class FreeGiftRules
{
    public double cooldownSeconds = 1800;
    public int dailyCap = 10;
    public int gemsPerClaim = 10;
    public int pitchAfterAdClaims = 3;
    public GiftCoinAnchor[] coinAnchors = DefaultAnchors;

    /// <summary>~12% of an hour's Coins early, tapering to ~2% late (spec §3.1). Sorted by level.</summary>
    public static GiftCoinAnchor[] DefaultAnchors => new[]
    {
        new GiftCoinAnchor(0, 30), new GiftCoinAnchor(10, 100), new GiftCoinAnchor(25, 600),
        new GiftCoinAnchor(50, 4000), new GiftCoinAnchor(100, 6000), new GiftCoinAnchor(175, 7500),
    };
}

/// <summary>
/// The Free Gift chest's rules: 30-min cooldown from the last claim (one charge, no stacking),
/// a per-local-day cap, the day's first chest is free, and a one-time Farmer's Pass pitch after
/// the Nth ad-watched claim. Pure: callers pass UTC ticks and a local "yyyy-MM-dd" day key.
/// Clock-back never shortens the cooldown (OfflineClock); clock-forward is accepted (single-player).
/// </summary>
public sealed class FreeGiftCore
{
    private readonly FreeGiftRules rules;
    private int claimsOnDate;

    public FreeGiftRules Rules => rules;
    public long LastClaimUtcTicks { get; private set; }
    public string ClaimsDate { get; private set; } = "";
    public int ClaimsOnSavedDate => claimsOnDate;
    public int LifetimeAdClaims { get; private set; }
    public bool PitchShown { get; private set; }

    public FreeGiftCore(FreeGiftRules rules) { this.rules = rules ?? new FreeGiftRules(); }

    public static string LocalDateKey(DateTime local) => local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public int ClaimsToday(string today) => ClaimsDate == today ? claimsOnDate : 0;

    /// <summary>The first chest of each local day needs no ad (this also makes the welcome chest free).</summary>
    public bool IsFreeChest(string today) => ClaimsToday(today) == 0;

    public double SecondsUntilReady(long nowUtcTicks)
    {
        if (LastClaimUtcTicks <= 0) return 0;
        // Clock set back past the last claim -> ForwardGap is 0 -> the full cooldown applies.
        double elapsed = OfflineClock.ForwardGapSeconds(LastClaimUtcTicks, nowUtcTicks);
        return Math.Max(0, rules.cooldownSeconds - elapsed);
    }

    /// <summary>Clock moved back past the last claim: re-anchor so the cooldown runs from now.</summary>
    public void HealClock(long nowUtcTicks)
    {
        if (LastClaimUtcTicks > nowUtcTicks) LastClaimUtcTicks = nowUtcTicks;
    }

    public GiftStatus Status(bool unlocked, long nowUtcTicks, string today)
    {
        if (!unlocked) return GiftStatus.Locked;
        if (ClaimsToday(today) >= rules.dailyCap) return GiftStatus.Capped;
        return SecondsUntilReady(nowUtcTicks) > 0 ? GiftStatus.Cooldown : GiftStatus.Ready;
    }

    /// <summary>Records one claim. Returns true exactly once: when this ad claim should trigger the
    /// Farmer's Pass pitch (also marks it shown). Free and pass claims never count toward it.</summary>
    public bool RecordClaim(long nowUtcTicks, string today, bool viaAd)
    {
        if (ClaimsDate != today) { ClaimsDate = today ?? ""; claimsOnDate = 0; }
        claimsOnDate++;
        LastClaimUtcTicks = nowUtcTicks;
        if (!viaAd) return false;
        LifetimeAdClaims++;
        if (PitchShown || LifetimeAdClaims < rules.pitchAfterAdClaims) return false;
        PitchShown = true;
        return true;
    }

    public void MarkPitchShown() => PitchShown = true;

    public void Import(long lastClaimUtcTicks, string claimsDate, int claimsOnDate, int lifetimeAdClaims, bool pitchShown)
    {
        LastClaimUtcTicks = Math.Max(0, lastClaimUtcTicks);
        ClaimsDate = claimsDate ?? "";
        this.claimsOnDate = Math.Max(0, claimsOnDate);
        LifetimeAdClaims = Math.Max(0, lifetimeAdClaims);
        PitchShown = pitchShown;
    }

    /// <summary>Coins at an Overall Farm Level: linear between anchors (sorted by level), clamped
    /// at both ends, rounded to the nearest 10 above 100.</summary>
    public static int CoinsForLevel(GiftCoinAnchor[] anchors, int level)
    {
        if (anchors == null || anchors.Length == 0) return 0;
        if (level <= anchors[0].level) return anchors[0].coins;
        GiftCoinAnchor last = anchors[anchors.Length - 1];
        if (level >= last.level) return last.coins;
        for (int i = 1; i < anchors.Length; i++)
        {
            if (level > anchors[i].level) continue;
            GiftCoinAnchor a = anchors[i - 1], b = anchors[i];
            double t = (level - a.level) / (double)Math.Max(1, b.level - a.level);
            return RoundGift(a.coins + (b.coins - a.coins) * t);
        }
        return last.coins;
    }

    public static int RoundGift(double raw)
    {
        if (raw <= 100) return (int)Math.Round(raw, MidpointRounding.AwayFromZero);
        return (int)(Math.Round(raw / 10.0, MidpointRounding.AwayFromZero) * 10);
    }
}
