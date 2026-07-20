using NUnit.Framework;

public class ReputationMathTests
{
    [Test]
    public void PointCost_MatchesSpecNumbers()
    {
        Assert.AreEqual(100, ReputationMath.PointCost(1));
        Assert.AreEqual(460, ReputationMath.PointCost(25));
        Assert.AreEqual(2335, ReputationMath.PointCost(150));
    }

    [Test]
    public void ApplyGain_ExactBoundaryAwardsOnePointAndZeroesBar()
    {
        ReputationMath.ApplyGain(0, 0, 100, out int barAfter, out int awarded);
        Assert.AreEqual(0, barAfter);
        Assert.AreEqual(1, awarded);
    }

    [Test]
    public void ApplyGain_LargeGainAwardsMultiplePoints()
    {
        // point1=100, point2=115, point3=130 -> 345 exactly clears 3 points
        ReputationMath.ApplyGain(0, 0, 345, out int barAfter, out int awarded);
        Assert.AreEqual(0, barAfter);
        Assert.AreEqual(3, awarded);
    }

    [Test]
    public void ApplyGain_PartialGainAwardsNoPoints()
    {
        ReputationMath.ApplyGain(0, 0, 60, out int barAfter, out int awarded);
        Assert.AreEqual(60, barAfter);
        Assert.AreEqual(0, awarded);
    }

    [Test]
    public void ApplyGain_ContinuesFromExistingProgress()
    {
        // Already 90 rep into point 1 (needs 100). +20 crosses it with 10 left over.
        ReputationMath.ApplyGain(90, 0, 20, out int barAfter, out int awarded);
        Assert.AreEqual(10, barAfter);
        Assert.AreEqual(1, awarded);
    }

    [Test]
    public void SkipCost_FollowsLadderAndCaps()
    {
        Assert.AreEqual(25, ReputationMath.SkipCost(0));
        Assert.AreEqual(50, ReputationMath.SkipCost(1));
        Assert.AreEqual(100, ReputationMath.SkipCost(2));
        Assert.AreEqual(150, ReputationMath.SkipCost(3));
        Assert.AreEqual(250, ReputationMath.SkipCost(4));
        Assert.AreEqual(250, ReputationMath.SkipCost(9)); // capped
    }
}
