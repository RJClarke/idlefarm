using NUnit.Framework;

public class RequestGenerationMathTests
{
    [Test]
    public void TryRoll_EmptyPool_ReturnsFalse()
    {
        bool ok = RequestGenerationMath.TryRoll(new RequestItemOption[0], new System.Random(1), out _, out _);
        Assert.IsFalse(ok);
    }

    [Test]
    public void TryRoll_SingleOption_AlwaysPicksIt()
    {
        var pool = new[] { new RequestItemOption { itemId = "Blueberry", minCount = 10, maxCount = 20, weight = 1 } };
        bool ok = RequestGenerationMath.TryRoll(pool, new System.Random(42), out string id, out int count);
        Assert.IsTrue(ok);
        Assert.AreEqual("Blueberry", id);
        Assert.GreaterOrEqual(count, 10);
        Assert.LessOrEqual(count, 20);
    }

    [Test]
    public void TryRoll_ZeroWeightItem_NeverSelected()
    {
        var pool = new[]
        {
            new RequestItemOption { itemId = "Never", minCount = 1, maxCount = 1, weight = 0 },
            new RequestItemOption { itemId = "Always", minCount = 1, maxCount = 1, weight = 10 },
        };
        for (int seed = 0; seed < 200; seed++)
        {
            RequestGenerationMath.TryRoll(pool, new System.Random(seed), out string id, out _);
            Assert.AreEqual("Always", id);
        }
    }

    [Test]
    public void TryRoll_SameSeed_IsDeterministic()
    {
        var pool = new[]
        {
            new RequestItemOption { itemId = "A", minCount = 1, maxCount = 100, weight = 1 },
            new RequestItemOption { itemId = "B", minCount = 1, maxCount = 100, weight = 1 },
            new RequestItemOption { itemId = "C", minCount = 1, maxCount = 100, weight = 1 },
        };
        RequestGenerationMath.TryRoll(pool, new System.Random(7), out string id1, out int c1);
        RequestGenerationMath.TryRoll(pool, new System.Random(7), out string id2, out int c2);
        Assert.AreEqual(id1, id2);
        Assert.AreEqual(c1, c2);
    }

    [Test]
    public void TryRoll_CountNeverBelowMin()
    {
        var pool = new[] { new RequestItemOption { itemId = "X", minCount = 5, maxCount = 5, weight = 1 } };
        RequestGenerationMath.TryRoll(pool, new System.Random(3), out _, out int count);
        Assert.AreEqual(5, count);
    }
}
