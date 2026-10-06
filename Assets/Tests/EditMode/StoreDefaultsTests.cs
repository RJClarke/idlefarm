using System.Linq;
using NUnit.Framework;

public class StoreDefaultsTests
{
    [Test]
    public void ProductIds_AreUnique()
    {
        var ids = StoreDefaults.Products.Select(p => p.id).ToArray();
        Assert.AreEqual(ids.Length, ids.Distinct().Count());
    }

    [Test]
    public void GemBundles_MatchSpecAndAscend()
    {
        var bundles = StoreDefaults.Products.Where(p => p.section == StoreSection.Gems).ToArray();
        CollectionAssert.AreEqual(new[] { "gems_handful", "gems_pouch", "gems_sack", "gems_chest", "gems_vault" }, bundles.Select(b => b.id).ToArray());
        CollectionAssert.AreEqual(new[] { 100, 550, 1200, 2600, 7000 }, bundles.Select(b => b.gems).ToArray());
        CollectionAssert.AreEqual(new[] { "$0.99", "$4.99", "$9.99", "$19.99", "$49.99" }, bundles.Select(b => b.fallbackPrice).ToArray());
    }

    [Test]
    public void Pass_Has500Gems_AndIsNonConsumable()
    {
        var pass = StoreDefaults.Products.Single(p => p.id == StoreDefaults.PassId);
        Assert.AreEqual(ProductKind.NonConsumable, pass.kind);
        Assert.AreEqual(500, pass.gems);
        Assert.AreEqual("$9.99", pass.fallbackPrice);
        Assert.AreEqual(StoreDefaults.PassDescription, pass.description);
    }

    [Test]
    public void NewProducts_MatchSpec()
    {
        var golden = StoreDefaults.Products.Single(p => p.id == SkinDefaults.GoldenSet);
        Assert.AreEqual("$4.99", golden.fallbackPrice);
        CollectionAssert.AreEquivalent(new[] { "chicken_golden", "rooster_golden", "house_yellow_barn" }, golden.grantsSkinIds);
        var puppy = StoreDefaults.Products.Single(p => p.id == SkinDefaults.PuppySet);
        Assert.AreEqual("$2.99", puppy.fallbackPrice);
        CollectionAssert.AreEquivalent(new[] { "dog_lab_brown", "dog_lab_dark", "dog_lab_white" }, puppy.grantsSkinIds);
        var starter = StoreDefaults.Products.Single(p => p.id == StoreDefaults.StarterId);
        Assert.AreEqual(800, starter.gems);
        CollectionAssert.AreEqual(new[] { "house_cottage" }, starter.grantsSkinIds);
        var blessing = StoreDefaults.Products.Single(p => p.id == StoreDefaults.HarvestBlessingId);
        Assert.AreEqual(StoreSection.Boosts, blessing.section);
        Assert.AreEqual("$9.99", blessing.fallbackPrice);
        Assert.AreEqual(1.25f, StoreDefaults.HarvestBlessingMultiplier);
        foreach (var p in new[] { golden, puppy, starter, blessing }) Assert.AreEqual(ProductKind.NonConsumable, p.kind, p.id);
    }

    [Test]
    public void EverySetOnlySkin_IsGrantedByItsSet()
    {
        foreach (var s in SkinDefaults.All.Where(s => s.IsSetOnly))
        {
            var set = StoreDefaults.Products.SingleOrDefault(p => p.id == s.setId);
            Assert.IsNotNull(set, s.id);
            CollectionAssert.Contains(set.grantsSkinIds, s.id);
        }
    }

    [Test]
    public void ComingSoon_IsFarmThemesOnly_AndRetiredTeasersGone()
    {
        var soon = StoreDefaults.Products.Where(p => p.section == StoreSection.ComingSoon).ToArray();
        CollectionAssert.AreEqual(new[] { "soon_farm_themes" }, soon.Select(p => p.id).ToArray());
        foreach (string retired in StoreDefaults.RetiredIds) Assert.IsFalse(StoreDefaults.Products.Any(p => p.id == retired), retired);
    }

    [Test]
    public void EnumValues_AreAppendOnly()
    {
        Assert.AreEqual(0, (int)StoreSection.Pass); Assert.AreEqual(1, (int)StoreSection.Gems);
        Assert.AreEqual(2, (int)StoreSection.ComingSoon); Assert.AreEqual(3, (int)StoreSection.Sets);
        Assert.AreEqual(4, (int)StoreSection.Boosts);
    }

    [Test]
    public void Copy_HasNoEmojiOrSurrogates()
    {
        foreach (var p in StoreDefaults.Products)
            foreach (string s in new[] { p.displayName, p.description, p.ribbon })
                if (s != null) foreach (char c in s) Assert.IsFalse(char.IsSurrogate(c), $"{p.id}: {s}");
    }
}
