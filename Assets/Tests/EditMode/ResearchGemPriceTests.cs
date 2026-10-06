using NUnit.Framework;

public class ResearchGemPriceTests
{
    [TestCase(0.0, 0)]
    [TestCase(-50.0, 0)]
    [TestCase(1.0, 1)]          // any time left costs at least 1
    [TestCase(180.0, 1)]        // 3 min at 20/h = 1
    [TestCase(181.0, 2)]        // ceil
    [TestCase(3600.0, 20)]
    [TestCase(86400.0, 480)]
    public void GemsToFinish_CeilsHoursTimesRate(double secs, int expected)
    {
        Assert.AreEqual(expected, ResearchGemPrice.GemsToFinish(secs, 20f));
    }

    [Test] public void GemsToFinish_ZeroRate_IsFree() => Assert.AreEqual(0, ResearchGemPrice.GemsToFinish(3600, 0f));
}
