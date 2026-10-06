using NUnit.Framework;

public class ResearchMathTests
{
    [Test]
    public void NoEarlyLevels_IsNeutral()
    {
        Assert.AreEqual(1f, ResearchMath.EarlyLevelMultiplier(1, 0, 0.1f));
        Assert.AreEqual(1f, ResearchMath.EarlyLevelMultiplier(10, 25, 1f));
    }

    [Test]
    public void EarlyLevels_AreDiscounted()
    {
        Assert.AreEqual(0.1f, ResearchMath.EarlyLevelMultiplier(1, 25, 0.1f), 1e-5f);
        Assert.AreEqual(0.1f, ResearchMath.EarlyLevelMultiplier(25, 25, 0.1f), 1e-5f);
    }

    [Test]
    public void Discount_EasesBackToFullPrice_WithoutACliff()
    {
        float at25 = ResearchMath.EarlyLevelMultiplier(25, 25, 0.1f);
        float at26 = ResearchMath.EarlyLevelMultiplier(26, 25, 0.1f);
        Assert.Greater(at26, at25);
        Assert.Less(at26 - at25, 0.05f); // small step, not a jump to 1
        Assert.AreEqual(0.55f, ResearchMath.EarlyLevelMultiplier(37, 25, 0.1f), 0.02f);
        Assert.AreEqual(1f, ResearchMath.EarlyLevelMultiplier(50, 25, 0.1f));
        Assert.AreEqual(1f, ResearchMath.EarlyLevelMultiplier(100, 25, 0.1f));
    }

    [Test]
    public void FormatBonus_PercentAndFlatUnits()
    {
        Assert.AreEqual("+0.5%", ResearchMath.FormatBonus(0.005f, "%"));
        Assert.AreEqual("+1%", ResearchMath.FormatBonus(0.01f, ""));
        Assert.AreEqual("+0.5s", ResearchMath.FormatBonus(0.5f, "s"));
    }
}
