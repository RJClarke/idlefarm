using NUnit.Framework;

public class FishingMathTests
{
    [Test]
    public void RollBiteSeconds_SpreadsAroundAverage()
    {
        Assert.AreEqual(600.0,  FishingMath.RollBiteSeconds(1200, 0f),   1e-6); // 0.5x
        Assert.AreEqual(1200.0, FishingMath.RollBiteSeconds(1200, 0.5f), 1e-6); // 1.0x
        Assert.AreEqual(1800.0, FishingMath.RollBiteSeconds(1200, 1f),   1e-6); // 1.5x
        // rand clamps
        Assert.AreEqual(600.0,  FishingMath.RollBiteSeconds(1200, -3f),  1e-6);
        Assert.AreEqual(1800.0, FishingMath.RollBiteSeconds(1200, 9f),   1e-6);
    }

    [Test]
    public void RollFishTier_PicksByCumulativeWeight()
    {
        // base pole odds: 98 / 1.9 / 0.1  → cumulative 0.98, 0.999, 1.0
        var w = new[] { 98f, 1.9f, 0.1f };
        Assert.AreEqual(1, FishingMath.RollFishTier(w, 0f));      // deep in perch
        Assert.AreEqual(1, FishingMath.RollFishTier(w, 0.5f));    // still perch
        Assert.AreEqual(2, FishingMath.RollFishTier(w, 0.985f));  // into bass band
        Assert.AreEqual(3, FishingMath.RollFishTier(w, 0.9995f)); // pike
        Assert.AreEqual(3, FishingMath.RollFishTier(w, 1f));      // top → last tier
    }

    [Test]
    public void RollFishTier_HandlesNullOrEmpty()
    {
        Assert.AreEqual(1, FishingMath.RollFishTier(null, 0.5f));
        Assert.AreEqual(1, FishingMath.RollFishTier(new float[0], 0.5f));
    }

    [Test]
    public void PoleGating_MirrorsAxe()
    {
        Assert.IsTrue(FishingMath.CanBuyPole(false, 80, 75));
        Assert.IsFalse(FishingMath.CanBuyPole(true, 999, 75));  // already owned
        Assert.IsFalse(FishingMath.CanBuyPole(false, 74, 75));  // short coins

        Assert.IsTrue(FishingMath.CanUpgradePole(0, 3, 300, 300, 50, 50));
        Assert.IsFalse(FishingMath.CanUpgradePole(3, 3, 9999, 300, 9999, 50)); // at max
        Assert.IsFalse(FishingMath.CanUpgradePole(0, 3, 299, 300, 9999, 50));  // short coins
        Assert.IsFalse(FishingMath.CanUpgradePole(0, 3, 9999, 300, 49, 50));   // short wood
    }
}
