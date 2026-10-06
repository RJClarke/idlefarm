using NUnit.Framework;

public class SkinOwnershipCoreTests
{
    private static SkinDef D(string id, string target, int price, string set = "") =>
        new SkinDef { id = id, displayName = id, target = target, gemPrice = price, setId = set };

    private static SkinOwnershipCore Core() => new SkinOwnershipCore(new[]
    {
        D("chicken_brown", "chicken", 1000),
        D("chicken_golden", "chicken", 0, "set_golden_farm"),
        D("house_cottage", "farmhouse", 2000),
    });

    [Test] public void Classic_IsAlwaysOwned_AndEquippedByDefault()
    {
        var c = Core();
        Assert.IsTrue(c.IsOwned("chicken_classic"));
        Assert.AreEqual("chicken_classic", c.EquippedFor("chicken"));
        Assert.IsTrue(c.IsEquipped("chicken_classic"));
    }

    [Test] public void CheckBuy_Covers_AllRefusals()
    {
        var c = Core();
        Assert.AreEqual(SkinBuyResult.NotEnoughGems, c.CheckBuy("chicken_brown", 999));
        Assert.AreEqual(SkinBuyResult.Ok, c.CheckBuy("chicken_brown", 1000));
        Assert.AreEqual(SkinBuyResult.SetOnly, c.CheckBuy("chicken_golden", 99999));
        Assert.AreEqual(SkinBuyResult.Unknown, c.CheckBuy("nope", 99999));
        Assert.AreEqual(SkinBuyResult.AlreadyOwned, c.CheckBuy("chicken_classic", 99999));
        c.MarkBought("chicken_brown");
        Assert.AreEqual(SkinBuyResult.AlreadyOwned, c.CheckBuy("chicken_brown", 99999));
    }

    [Test] public void MarkBought_OwnsAndEquips()
    {
        var c = Core();
        c.MarkBought("chicken_brown");
        Assert.IsTrue(c.IsOwned("chicken_brown"));
        Assert.AreEqual("chicken_brown", c.EquippedFor("chicken"));
        Assert.IsFalse(c.IsEquipped("chicken_classic"));
    }

    [Test] public void Equip_RequiresOwnership_ClassicAlwaysWorks()
    {
        var c = Core();
        Assert.IsFalse(c.Equip("house_cottage"));
        Assert.AreEqual("farmhouse_classic", c.EquippedFor("farmhouse"));
        c.Grant(new[] { "house_cottage" });
        Assert.IsTrue(c.Equip("house_cottage"));
        Assert.IsTrue(c.Equip("farmhouse_classic"));
        Assert.AreEqual("farmhouse_classic", c.EquippedFor("farmhouse"));
    }

    [Test] public void Grant_IgnoresUnknownAndClassic()
    {
        var c = Core();
        c.Grant(new[] { "chicken_golden", "bogus", "chicken_classic", null });
        CollectionAssert.AreEqual(new[] { "chicken_golden" }, c.ExportOwned());
    }

    [Test] public void ExportImport_RoundTrips_AndDropsBadData()
    {
        var c = Core();
        c.MarkBought("chicken_brown");
        c.Grant(new[] { "house_cottage" });
        var copy = Core();
        copy.Import(c.ExportOwned(), c.ExportEquipped());
        Assert.IsTrue(copy.IsOwned("house_cottage"));
        Assert.AreEqual("chicken_brown", copy.EquippedFor("chicken"));

        var bad = Core();
        bad.Import(new[] { "bogus", null, "chicken_brown" }, new[] { "chicken=bogus", "farmhouse=house_cottage", "garbage", null, "cow=cow_classic" });
        CollectionAssert.AreEqual(new[] { "chicken_brown" }, bad.ExportOwned());
        Assert.AreEqual("chicken_classic", bad.EquippedFor("chicken"));      // unknown skin dropped
        Assert.AreEqual("farmhouse_classic", bad.EquippedFor("farmhouse"));  // not owned -> dropped
    }

    [Test] public void PriceOf_ReturnsGemPrice_OrZero()
    {
        var c = Core();
        Assert.AreEqual(2000, c.PriceOf("house_cottage"));
        Assert.AreEqual(0, c.PriceOf("nope"));
    }
}
