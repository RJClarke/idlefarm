using NUnit.Framework;

public class SeedEconomyTests
{
    [Test]
    public void BagCost_AtRunStart_EqualsBaseCost()
    {
        // runMinutes=0, no discount -> base cost
        Assert.AreEqual(50, SeedEconomy.BagCost(baseCost: 50, runMinutes: 0f, discountBonus: 0f));
    }

    [Test]
    public void BagCost_EscalatesWithRunTime()
    {
        // escalationPerMinute = 0.15 -> at 10 min, cost = 50 * (1 + 1.5) = 125
        Assert.AreEqual(125, SeedEconomy.BagCost(baseCost: 50, runMinutes: 10f, discountBonus: 0f));
    }

    [Test]
    public void BagCost_DiscountReducesCost_AndIsAppliedAfterEscalation()
    {
        // base 50, 10 min -> 125, then 20% discount -> 100
        Assert.AreEqual(100, SeedEconomy.BagCost(baseCost: 50, runMinutes: 10f, discountBonus: 0.20f));
    }

    [Test]
    public void BagCost_NeverBelowOne()
    {
        Assert.AreEqual(1, SeedEconomy.BagCost(baseCost: 50, runMinutes: 0f, discountBonus: 5f));
    }

    [Test]
    public void BagSize_ScalesWithBonus_AndFloors()
    {
        // 20 * (1 + 0.35) = 27.0 -> 27
        Assert.AreEqual(27, SeedEconomy.BagSize(baseSize: 20, sizeBonus: 0.35f));
    }

    [Test]
    public void BagSize_NeverBelowOne()
    {
        Assert.AreEqual(1, SeedEconomy.BagSize(baseSize: 20, sizeBonus: -5f));
    }
}
