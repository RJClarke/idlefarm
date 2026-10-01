using NUnit.Framework;

public class StatLineTests
{
    [Test]
    public void NoBonuses_TotalEqualsBase()
    {
        var s = new StatLine("Health", 80, StatFormat.Number).Multiply("Research", 1f);
        Assert.AreEqual(80f, s.total);
        Assert.IsFalse(s.HasBonus);
    }

    [Test]
    public void Multiply_WithRounding_RecordsPerSourceDeltas()
    {
        // 25 -> x1.2 = 30 -> x1.25 = 37.5 -> RoundToInt (banker's, like gameplay) = 38
        var s = new StatLine("Money", 25, StatFormat.Money)
            .Multiply("Research", 1.2f, roundToInt: true)
            .Multiply("Farm upgrades", 1.25f, roundToInt: true);
        Assert.AreEqual(38f, s.total);
        Assert.AreEqual(2, s.Bonuses.Count);
        Assert.AreEqual("Research", s.Bonuses[0].source);
        Assert.AreEqual(5f, s.Bonuses[0].delta);
        Assert.AreEqual(8f, s.Bonuses[1].delta);
    }

    [Test]
    public void Divide_And_AtLeast()
    {
        var s = new StatLine("Cooldown", 60, StatFormat.Seconds).Add("Upgrades", -20).Divide("Research", 2f).AtLeast(25f);
        Assert.AreEqual(25f, s.total);        // 60-20=40, /2=20, floored at 25
        Assert.AreEqual(3, s.Bonuses.Count);  // the floor is recorded as its own line
        Assert.AreEqual("Minimum", s.Bonuses[2].source);
    }

    [Test]
    public void SameSourceTwice_MergesIntoOneLine()
    {
        var s = new StatLine("Money", 10, StatFormat.Money).Add("Research", 2).Add("Research", 3);
        Assert.AreEqual(1, s.Bonuses.Count);
        Assert.AreEqual(5f, s.Bonuses[0].delta);
    }

    [Test]
    public void Formatting()
    {
        Assert.AreEqual("$37", StatText.Value(37, StatFormat.Money));
        Assert.AreEqual("+$12", StatText.Delta(12, StatFormat.Money));
        Assert.AreEqual("5m 25s", StatText.Value(325, StatFormat.Seconds));
        Assert.AreEqual("45s", StatText.Value(45, StatFormat.Seconds));
        Assert.AreEqual("-40s", StatText.Delta(-40, StatFormat.Seconds));
        Assert.AreEqual("x1.25", StatText.Value(1.25f, StatFormat.Multiplier));
        Assert.AreEqual("40%", StatText.Value(0.4f, StatFormat.Percent));
        Assert.AreEqual("2.5 tiles", StatText.Value(2.5f, StatFormat.Tiles));
        Assert.AreEqual("12", StatText.Value(12, StatFormat.Number));
        Assert.AreEqual("3 Coins", StatText.Value(3, StatFormat.Coins));
    }

    [Test] public void Speed_ReadsInTilesPerSecond() => Assert.AreEqual("1.5 tiles/s", StatText.Value(1.5f, StatFormat.Speed));
    [Test] public void Speed_DeltaIsSigned() => Assert.AreEqual("+0.3", StatText.Delta(0.3f, StatFormat.Speed));
}
