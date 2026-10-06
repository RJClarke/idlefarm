using System.Linq;
using NUnit.Framework;

public class SkinDefaultsTests
{
    [Test] public void Ids_AreUnique_AndTargetsValid()
    {
        var ids = SkinDefaults.All.Select(s => s.id).ToArray();
        Assert.AreEqual(ids.Length, ids.Distinct().Count());
        foreach (var s in SkinDefaults.All) CollectionAssert.Contains(SkinDefaults.Targets, s.target, s.id);
    }

    [Test] public void GemPrices_FollowTheTiers()
    {
        int[] animalTiers = { 1000, 1500 };
        foreach (var s in SkinDefaults.All)
        {
            if (s.IsSetOnly) { Assert.AreEqual(0, s.gemPrice, s.id); continue; }
            if (s.target == "farmhouse") Assert.That(s.gemPrice, Is.InRange(2000, 3500), s.id);
            else CollectionAssert.Contains(animalTiers, s.gemPrice, s.id);
        }
    }

    [Test] public void GemSkinTotal_IsTheSpecBudget()
    {
        Assert.AreEqual(28, SkinDefaults.All.Count(s => !s.IsSetOnly));
        Assert.AreEqual(46500, SkinDefaults.All.Where(s => !s.IsSetOnly).Sum(s => s.gemPrice));
    }

    [Test] public void AnimalSkins_LiveInTheirFamilyFolder_BuildingsInBuildings()
    {
        foreach (var s in SkinDefaults.All)
        {
            if (s.kind == SkinKind.BuildingSprite) { StringAssert.StartsWith("Assets/Sprites/Buildings/", s.assetPath, s.id); continue; }
            string folder = s.target switch
            {
                "chicken" => "Chickens_and_Roosters_32x32",
                "rooster" => "Chickens_and_Roosters_32x32",
                "cow" => "Cows_32x32",
                "pig" => "Pigs_32x32",
                "farm_dog" => "Dogs_32x32",
                _ => "??",
            };
            StringAssert.Contains("/" + folder + "/", s.assetPath, s.id);
        }
    }

    [Test] public void Barns_UseTheFullRoofedComposites_NotBareFronts()
    {
        foreach (var s in SkinDefaults.All.Where(s => s.assetPath.Contains("Hayloft")))
            StringAssert.Contains("/Hayloft/Full/Hayloft_", s.assetPath, s.id);
    }

    [Test] public void Classic_Helpers()
    {
        Assert.AreEqual("cow_classic", SkinDefaults.ClassicId("cow"));
        Assert.IsTrue(SkinDefaults.IsClassic("farm_dog_classic"));
        Assert.AreEqual("farm_dog", SkinDefaults.TargetOfClassic("farm_dog_classic"));
        Assert.IsFalse(SkinDefaults.IsClassic("cow_caramel"));
    }

    [Test] public void Copy_HasNoSurrogates()
    {
        foreach (var s in SkinDefaults.All) foreach (char ch in s.displayName) Assert.IsFalse(char.IsSurrogate(ch), s.id);
    }
}
