using NUnit.Framework;
using UnityEngine;

public class FarmUpgradeMathTests
{
    // ───────── FarmUpgradeMath.Cost ─────────

    [Test]
    public void Cost_Level1_EqualsBase()
    {
        Assert.AreEqual(100L, FarmUpgradeMath.Cost(100, 1, 1.08f, 25, 2.0f));
    }

    [Test]
    public void Cost_Level0OrNegative_IsZero()
    {
        Assert.AreEqual(0L, FarmUpgradeMath.Cost(100, 0, 1.08f, 25, 2.0f));
        Assert.AreEqual(0L, FarmUpgradeMath.Cost(100, -3, 1.08f, 25, 2.0f));
    }

    [Test]
    public void Cost_GeometricGrowth_AppliesPerLevel()
    {
        // 100 * 1.10^1 = 110 (level 2, before any breakpoint)
        Assert.AreEqual(110L, FarmUpgradeMath.Cost(100, 2, 1.10f, 25, 2.0f));
    }

    [Test]
    public void Cost_Breakpoint_AppliesEveryNLevels()
    {
        // growth = 1.0 isolates the breakpoint multiplier.
        // level 25 -> floor(24/25)=0 breakpoints -> 100
        Assert.AreEqual(100L, FarmUpgradeMath.Cost(100, 25, 1.0f, 25, 2.0f));
        // level 26 -> floor(25/25)=1 breakpoint -> 200
        Assert.AreEqual(200L, FarmUpgradeMath.Cost(100, 26, 1.0f, 25, 2.0f));
        // level 51 -> floor(50/25)=2 breakpoints -> 400
        Assert.AreEqual(400L, FarmUpgradeMath.Cost(100, 51, 1.0f, 25, 2.0f));
    }

    [Test]
    public void Cost_NoBreakpoint_WhenEveryIsZero()
    {
        Assert.AreEqual(100L, FarmUpgradeMath.Cost(100, 60, 1.0f, 0, 2.0f));
    }

    [Test]
    public void Cost_HugeLevel_SaturatesToLongMax_NotOverflowGarbage()
    {
        long c = FarmUpgradeMath.Cost(1000, 100000, 1.5f, 25, 2.0f);
        Assert.AreEqual(long.MaxValue, c);
    }

    [Test]
    public void ClampToInt_SaturatesToIntMax()
    {
        Assert.AreEqual(int.MaxValue, FarmUpgradeMath.ClampToInt(long.MaxValue));
        Assert.AreEqual(0, FarmUpgradeMath.ClampToInt(-5));
        Assert.AreEqual(123, FarmUpgradeMath.ClampToInt(123));
    }

    [Test]
    public void Bonus_IsLevelTimesPerLevel_FlooredAtZero()
    {
        Assert.AreEqual(0.20f, FarmUpgradeMath.Bonus(10, 0.02f), 1e-5f);
        Assert.AreEqual(0f, FarmUpgradeMath.Bonus(-4, 0.02f), 1e-5f);
    }

    // ───────── FarmUpgradeData ─────────

    [Test]
    public void Data_CoinCost_UsesItsOwnCurveParams()
    {
        var d = ScriptableObject.CreateInstance<FarmUpgradeData>();
        d.baseCoinCost = 200;
        d.coinGrowthPerLevel = 1.0f;
        d.coinBreakpointEvery = 10;
        d.coinBreakpointMultiplier = 3.0f;

        Assert.AreEqual(200L, d.GetCoinCost(1));     // floor(0/10)=0
        Assert.AreEqual(200L, d.GetCoinCost(10));    // floor(9/10)=0
        Assert.AreEqual(600L, d.GetCoinCost(11));    // floor(10/10)=1 -> *3
        Object.DestroyImmediate(d);
    }

    [Test]
    public void Data_BonusText_FormatsPercentAsWholePercent()
    {
        var d = ScriptableObject.CreateInstance<FarmUpgradeData>();
        d.bonusPerLevel = 0.02f; // +2% per level
        d.bonusUnit = "%";
        Assert.AreEqual("+24%", d.GetBonusText(12)); // 12 * 0.02 = 0.24 -> 24%
        Object.DestroyImmediate(d);
    }

    [Test]
    public void Data_BonusText_FormatsNonPercentUnit()
    {
        var d = ScriptableObject.CreateInstance<FarmUpgradeData>();
        d.bonusPerLevel = 5f; // +5 pts per level
        d.bonusUnit = "pts";
        Assert.AreEqual("+40 pts", d.GetBonusText(8));
        Object.DestroyImmediate(d);
    }
}
