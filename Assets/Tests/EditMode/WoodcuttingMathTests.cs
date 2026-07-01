using NUnit.Framework;
using StackMode = WoodcuttingMath.StackMode;

public class WoodcuttingMathTests
{
    [Test]
    public void EffectiveHitsToFell_ReducesPerAxeLevel_ClampedToMin()
    {
        // base 5 hits, no axe → 5
        Assert.AreEqual(5, WoodcuttingMath.EffectiveHitsToFell(5, 0, 1));
        // axe level 2, reduction 1/level → 3
        Assert.AreEqual(3, WoodcuttingMath.EffectiveHitsToFell(5, 2, 1));
        // never drops below minHits (default 1)
        Assert.AreEqual(1, WoodcuttingMath.EffectiveHitsToFell(5, 99, 1));
        // explicit minHits respected
        Assert.AreEqual(2, WoodcuttingMath.EffectiveHitsToFell(5, 99, 1, 2));
    }

    [Test]
    public void CanFell_RequiresAxeLevelAtOrAboveTreeRequirement()
    {
        Assert.IsTrue(WoodcuttingMath.CanFell(0, 0));   // softwood, bare hands
        Assert.IsFalse(WoodcuttingMath.CanFell(1, 0));  // hardwood, no axe
        Assert.IsTrue(WoodcuttingMath.CanFell(1, 1));   // hardwood, axe lvl 1
    }

    [Test]
    public void RegrowFraction_ZeroToOne_Clamped()
    {
        Assert.AreEqual(0f, WoodcuttingMath.RegrowFraction(0, 10f), 1e-4f);
        Assert.AreEqual(0.5f, WoodcuttingMath.RegrowFraction(5, 10f), 1e-4f);
        Assert.AreEqual(1f, WoodcuttingMath.RegrowFraction(20, 10f), 1e-4f);
    }

    [Test]
    public void RegrowFraction_ZeroDuration_IsImmediatelyFull()
    {
        Assert.AreEqual(1f, WoodcuttingMath.RegrowFraction(0, 0f), 1e-4f);
    }

    [Test]
    public void IsRegrown_TrueAtOrPastDuration()
    {
        Assert.IsFalse(WoodcuttingMath.IsRegrown(9.9, 10f));
        Assert.IsTrue(WoodcuttingMath.IsRegrown(10, 10f));
        Assert.IsTrue(WoodcuttingMath.IsRegrown(50, 10f));
    }

    [Test]
    public void ResolveStackAmount_ClampsToAvailable()
    {
        Assert.AreEqual(1, WoodcuttingMath.ResolveStackAmount(StackMode.One, 50));
        Assert.AreEqual(10, WoodcuttingMath.ResolveStackAmount(StackMode.Ten, 50));
        Assert.AreEqual(5, WoodcuttingMath.ResolveStackAmount(StackMode.Ten, 5)); // fewer than 10
        Assert.AreEqual(50, WoodcuttingMath.ResolveStackAmount(StackMode.All, 50));
        Assert.AreEqual(0, WoodcuttingMath.ResolveStackAmount(StackMode.All, 0));
    }

    [Test]
    public void SellValue_MultipliesAmountByPrice_NeverNegative()
    {
        Assert.AreEqual(0, WoodcuttingMath.SellValue(0, 5));
        Assert.AreEqual(50, WoodcuttingMath.SellValue(10, 5));
        Assert.AreEqual(0, WoodcuttingMath.SellValue(-3, 5));
    }

    [Test]
    public void CanUpgradeAxe_RequiresUnderMaxAndAffordBoth()
    {
        // under max, can afford both → true
        Assert.IsTrue(WoodcuttingMath.CanUpgradeAxe(0, 3, coins: 100, coinCost: 100, wood: 50, woodCost: 50));
        // at max → false
        Assert.IsFalse(WoodcuttingMath.CanUpgradeAxe(3, 3, 1000, 100, 1000, 50));
        // not enough coins → false
        Assert.IsFalse(WoodcuttingMath.CanUpgradeAxe(0, 3, 99, 100, 1000, 50));
        // not enough wood → false
        Assert.IsFalse(WoodcuttingMath.CanUpgradeAxe(0, 3, 1000, 100, 49, 50));
    }

    [Test]
    public void StageIndex_PartitionsGrowthIntoStages_Clamped()
    {
        Assert.AreEqual(0, WoodcuttingMath.StageIndex(0f, 5));    // sapling
        Assert.AreEqual(0, WoodcuttingMath.StageIndex(0.19f, 5)); // still stage 0
        Assert.AreEqual(2, WoodcuttingMath.StageIndex(0.5f, 5));  // middle
        Assert.AreEqual(4, WoodcuttingMath.StageIndex(1f, 5));    // full (clamped, not 5)
        Assert.AreEqual(4, WoodcuttingMath.StageIndex(2f, 5));    // over-clamped
    }

    [Test]
    public void StageYield_PartialWhenEarly_FullAtLastStage()
    {
        // 5 stages, full yield 100 → stage 0 = 20, stage 2 = 60, stage 4 = 100
        Assert.AreEqual(20, WoodcuttingMath.StageYield(100, 0, 5));
        Assert.AreEqual(60, WoodcuttingMath.StageYield(100, 2, 5));
        Assert.AreEqual(100, WoodcuttingMath.StageYield(100, 4, 5));
    }

    [Test]
    public void StageHits_ScalesWithStage_MinimumOne()
    {
        // 10 hits full, 5 stages → stage 0 = 2, stage 4 = 10
        Assert.AreEqual(2, WoodcuttingMath.StageHits(10, 0, 5));
        Assert.AreEqual(10, WoodcuttingMath.StageHits(10, 4, 5));
        // never below 1 even for a tiny full count at an early stage
        Assert.AreEqual(1, WoodcuttingMath.StageHits(1, 0, 5));
    }
}
