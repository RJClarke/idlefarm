using NUnit.Framework;

public class FishTiersTests
{
    [Test]
    public void Count_IsThree() => Assert.AreEqual(3, FishTiers.Count);

    [Test]
    public void Name_MapsTiers_AndClamps()
    {
        Assert.AreEqual("Perch", FishTiers.Name(1));
        Assert.AreEqual("Bass", FishTiers.Name(2));
        Assert.AreEqual("Northern Pike", FishTiers.Name(3));
        Assert.AreEqual("Perch", FishTiers.Name(0));   // clamped up
        Assert.AreEqual("Northern Pike", FishTiers.Name(99)); // clamped down
    }

    [Test]
    public void SmokedName_PrefixesSmoked()
    {
        Assert.AreEqual("Smoked Perch", FishTiers.SmokedName(1));
        Assert.AreEqual("Smoked Northern Pike", FishTiers.SmokedName(3));
    }
}
