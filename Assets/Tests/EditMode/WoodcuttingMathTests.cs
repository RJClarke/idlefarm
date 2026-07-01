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
}
