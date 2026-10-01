using NUnit.Framework;
using UnityEngine;
using System.Linq;

public class LetterCatalogTests
{
    private LetterCatalogSO MakeCatalog()
    {
        var cat = ScriptableObject.CreateInstance<LetterCatalogSO>();
        cat.letters = new[]
        {
            new LetterDef { id = "welcome", subject = "Welcome" },
            new LetterDef { id = "scarecrow", triggerFeatureFlag = "scarecrow", subject = "Build it" },
            new LetterDef { id = "cow", triggerAnimalId = "cow", subject = "Moo" },
        };
        return cat;
    }

    [Test]
    public void Get_ReturnsMatchingDef_OrNull()
    {
        var cat = MakeCatalog();
        Assert.AreEqual("Welcome", cat.Get("welcome").subject);
        Assert.IsNull(cat.Get("nope"));
        Assert.IsNull(cat.Get(null));
        Object.DestroyImmediate(cat);
    }

    [Test]
    public void ByFeatureFlag_FiltersByTrigger()
    {
        var cat = MakeCatalog();
        var hits = cat.ByFeatureFlag("scarecrow").ToList();
        Assert.AreEqual(1, hits.Count);
        Assert.AreEqual("scarecrow", hits[0].id);
        Assert.IsEmpty(cat.ByFeatureFlag("welcome")); // welcome has no trigger flag
        Object.DestroyImmediate(cat);
    }

    [Test]
    public void ByAnimalId_FiltersByTrigger()
    {
        var cat = MakeCatalog();
        var hits = cat.ByAnimalId("cow").ToList();
        Assert.AreEqual(1, hits.Count);
        Assert.AreEqual("cow", hits[0].id);
        Object.DestroyImmediate(cat);
    }

    [Test]
    public void ByEvent_MatchesAnyPipeSeparatedTrigger()
    {
        var cat = ScriptableObject.CreateInstance<LetterCatalogSO>();
        cat.letters = new[]
        {
            new LetterDef { id = "pole", triggerEvent = "run_ended:3|axe_bought" },
            new LetterDef { id = "tree", triggerEvent = "tree_felled" },
            new LetterDef { id = "plain" },
        };
        Assert.AreEqual("pole", cat.ByEvent("axe_bought").Single().id);
        Assert.AreEqual("pole", cat.ByEvent("run_ended:3").Single().id);
        Assert.AreEqual("tree", cat.ByEvent("tree_felled").Single().id);
        Assert.IsEmpty(cat.ByEvent("run_ended:30")); // whole-token match, not prefix
        Assert.IsEmpty(cat.ByEvent(null));
        Object.DestroyImmediate(cat);
    }

    [Test]
    public void GetTip_And_GetCast_LookUpById()
    {
        var cat = ScriptableObject.CreateInstance<LetterCatalogSO>();
        cat.tips = new[] { new TipDef { id = "tip_lake", text = "Cast!" } };
        cat.cast = new[] { new CastMember { id = "finch", displayName = "Old Finch" } };
        Assert.AreEqual("Cast!", cat.GetTip("tip_lake").text);
        Assert.IsNull(cat.GetTip("nope"));
        Assert.AreEqual("Old Finch", cat.GetCast("finch").displayName);
        Assert.IsNull(cat.GetCast(null));
        Object.DestroyImmediate(cat);
    }
}
