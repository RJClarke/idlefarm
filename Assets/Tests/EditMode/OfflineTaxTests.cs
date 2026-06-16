using NUnit.Framework;

public class OfflineTaxTests
{
    [Test] public void Coins_Taxed30Percent()  => Assert.AreEqual(98,  OfflineTax.Payout(140));
    [Test] public void Money_Taxed30Percent()   => Assert.AreEqual(574, OfflineTax.Payout(820));
    [Test] public void Floors_NotRounds()       => Assert.AreEqual(7,   OfflineTax.Payout(10)); // 10*0.7=7.0
    [Test] public void FloorsFraction()          => Assert.AreEqual(3,   OfflineTax.Payout(5));  // 5*0.7=3.5 -> 3
    [Test] public void Zero_StaysZero()          => Assert.AreEqual(0,   OfflineTax.Payout(0));
    [Test] public void BaseRateIsThirtyPercent() => Assert.AreEqual(0.30f, OfflineTax.BaseRate, 0.0001f);

    // offline_efficiency research reduces the tax
    [Test] public void Efficiency_ReducesTax()   => Assert.AreEqual(80,  OfflineTax.Payout(100, 0.10f)); // rate 0.20
    [Test] public void Efficiency_CanClearTax()  => Assert.AreEqual(100, OfflineTax.Payout(100, 0.50f)); // rate clamps to 0
    [Test] public void Efficiency_NegativeClamped() => Assert.AreEqual(70, OfflineTax.Payout(100, -1f)); // never exceeds base
}
